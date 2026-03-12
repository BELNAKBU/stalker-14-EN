using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.News;
using Robust.Shared.Player;

namespace Content.Server._Stalker_EN.News;

/// <summary>
/// Partial class handling all reaction logic for the Stalker News system.
/// </summary>
public sealed partial class STNewsSystem
{
    /// <summary>Global counts: (TargetType, TargetId) -> { reactionId -> count }</summary>
    private readonly Dictionary<(STReactionTargetType, int), Dictionary<string, int>> _reactionCounts = new();

    /// <summary>Per-user reactions: (TargetType, TargetId) -> { userId -> set of reactionIds }</summary>
    private readonly Dictionary<(STReactionTargetType, int), Dictionary<Guid, HashSet<string>>> _userReactions = new();

    private bool _reactionBroadcastPending;
    private TimeSpan _nextReactionBroadcast;
    /// <summary>
    /// Debounce interval for reaction broadcasts. Prevents rapid-fire UI updates
    /// when multiple players toggle reactions in quick succession.
    /// </summary>
    private static readonly TimeSpan ReactionBroadcastInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Loads reactions from DB for all cached article IDs. Called during startup after articles are loaded.
    /// </summary>
    private async void LoadReactionsFromDatabaseAsync()
    {
        try
        {
            if (_articles.Count == 0)
                return;

            var articleIds = new List<int>(_articles.Count);
            foreach (var a in _articles)
                articleIds.Add(a.Id);

            var dbReactions = await _dbManager.GetStalkerNewsReactionsAsync(
                (int) STReactionTargetType.Article, articleIds);

            foreach (var r in dbReactions)
            {
                var key = (STReactionTargetType.Article, r.TargetId);

                // Populate counts
                if (!_reactionCounts.TryGetValue(key, out var counts))
                {
                    counts = new Dictionary<string, int>();
                    _reactionCounts[key] = counts;
                }
                counts.TryGetValue(r.ReactionId, out var current);
                counts[r.ReactionId] = current + 1;

                // Populate user reactions
                if (!_userReactions.TryGetValue(key, out var users))
                {
                    users = new Dictionary<Guid, HashSet<string>>();
                    _userReactions[key] = users;
                }
                if (!users.TryGetValue(r.UserId, out var reactions))
                {
                    reactions = new HashSet<string>();
                    users[r.UserId] = reactions;
                }
                reactions.Add(r.ReactionId);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load news reactions from database: {e}");
        }
    }

    private void OnToggleReaction(
        EntityUid uid,
        STNewsCartridgeComponent comp,
        STNewsToggleReactionEvent toggle,
        CartridgeMessageEvent args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        var reactionId = toggle.ReactionId;
        if (!STReactionDefinitions.Available.Contains(reactionId))
            return;

        if (FindArticleById(toggle.ArticleId) == null)
            return;

        // Per-cartridge cooldown
        var cooldownMs = _config.GetCVar(STCCVars.NewsReactionCooldownMs);
        if (_timing.CurTime < comp.NextReactionTime)
            return;

        comp.NextReactionTime = _timing.CurTime + TimeSpan.FromMilliseconds(cooldownMs);

        var userId = actor.PlayerSession.UserId.UserId;
        var articleId = toggle.ArticleId;
        var key = (STReactionTargetType.Article, articleId);

        // Determine if toggling on or off
        var isAdding = true;
        if (_userReactions.TryGetValue(key, out var users)
            && users.TryGetValue(userId, out var existing)
            && existing.Contains(reactionId))
        {
            isAdding = false;
        }

        // Enforce max distinct reaction types per article
        if (isAdding)
        {
            var maxDistinct = _config.GetCVar(STCCVars.NewsMaxDistinctReactions);
            if (_reactionCounts.TryGetValue(key, out var existingCounts)
                && existingCounts.Count >= maxDistinct
                && !existingCounts.ContainsKey(reactionId))
            {
                return;
            }
        }

        // Update in-memory caches
        if (isAdding)
        {
            if (!_reactionCounts.TryGetValue(key, out var counts))
            {
                counts = new Dictionary<string, int>();
                _reactionCounts[key] = counts;
            }
            counts.TryGetValue(reactionId, out var c);
            counts[reactionId] = c + 1;

            if (!_userReactions.TryGetValue(key, out var usersMap))
            {
                usersMap = new Dictionary<Guid, HashSet<string>>();
                _userReactions[key] = usersMap;
            }
            if (!usersMap.TryGetValue(userId, out var userSet))
            {
                userSet = new HashSet<string>();
                usersMap[userId] = userSet;
            }
            userSet.Add(reactionId);
        }
        else
        {
            if (_reactionCounts.TryGetValue(key, out var counts))
            {
                counts.TryGetValue(reactionId, out var c);
                counts[reactionId] = Math.Max(0, c - 1);
                if (counts[reactionId] == 0)
                    counts.Remove(reactionId);
                if (counts.Count == 0)
                    _reactionCounts.Remove(key);
            }

            if (_userReactions.TryGetValue(key, out var usersMap)
                && usersMap.TryGetValue(userId, out var userSet))
            {
                userSet.Remove(reactionId);
                if (userSet.Count == 0)
                    usersMap.Remove(userId);
                if (usersMap.Count == 0)
                    _userReactions.Remove(key);
            }
        }

        // Fire-and-forget DB toggle
        ToggleReactionDbAsync(articleId, userId, reactionId);

        // Schedule debounced broadcast
        _reactionBroadcastPending = true;
        _nextReactionBroadcast = _timing.CurTime + ReactionBroadcastInterval;

        _adminLogger.Add(
            LogType.STNews,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} {(isAdding ? "added" : "removed")} reaction '{reactionId}' on article #{articleId}");
    }

    private async void ToggleReactionDbAsync(int articleId, Guid userId, string reactionId)
    {
        try
        {
            await _dbManager.ToggleStalkerNewsReactionAsync(
                (int) STReactionTargetType.Article, articleId, userId, reactionId);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to toggle reaction for article #{articleId}: {e}");
        }
    }

    /// <summary>
    /// Builds reaction summaries for a single article, personalized for the viewing user.
    /// </summary>
    private List<STReactionSummary> BuildArticleReactionSummaries(int articleId, Guid viewerUserId)
    {
        var key = (STReactionTargetType.Article, articleId);
        if (!_reactionCounts.TryGetValue(key, out var counts) || counts.Count == 0)
            return new List<STReactionSummary>();

        HashSet<string>? viewerReactions = null;
        if (_userReactions.TryGetValue(key, out var users))
            users.TryGetValue(viewerUserId, out viewerReactions);

        var summaries = new List<STReactionSummary>(counts.Count);
        foreach (var (reactionId, count) in counts)
        {
            if (count <= 0)
                continue;

            var userReacted = viewerReactions?.Contains(reactionId) ?? false;
            summaries.Add(new STReactionSummary(reactionId, count, userReacted));
        }

        return summaries;
    }

    /// <summary>
    /// Builds the ArticleReactions dictionary for UI state, personalized for the viewer.
    /// </summary>
    private Dictionary<int, List<STReactionSummary>> BuildAllArticleReactions(Guid viewerUserId)
    {
        var result = new Dictionary<int, List<STReactionSummary>>();
        foreach (var article in _articles)
        {
            var summaries = BuildArticleReactionSummaries(article.Id, viewerUserId);
            if (summaries.Count > 0)
                result[article.Id] = summaries;
        }

        return result;
    }

    /// <summary>
    /// Removes all cached reaction data for a specific article.
    /// </summary>
    private void RemoveReactionCacheForArticle(int articleId)
    {
        var key = (STReactionTargetType.Article, articleId);
        _reactionCounts.Remove(key);
        _userReactions.Remove(key);
    }

    /// <summary>
    /// Fire-and-forget deletion of all reactions for an article from the database.
    /// </summary>
    private async void DeleteReactionsForArticleAsync(int articleId)
    {
        try
        {
            await _dbManager.DeleteStalkerNewsReactionsByTargetAsync(
                (int) STReactionTargetType.Article, articleId);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to delete reactions for article #{articleId}: {e}");
        }
    }
}
