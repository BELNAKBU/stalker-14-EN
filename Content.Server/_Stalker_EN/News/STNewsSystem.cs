using System.Text.RegularExpressions;
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Server.PDA.Ringer;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.PDA.Ringer;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.CCVar;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared._Stalker_EN.News;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker_EN.News;

/// <summary>
/// Server system for the Stalker News PDA cartridge program.
/// Manages article publishing, deletion, comments, database persistence, Discord webhook, and broadcast updates.
/// </summary>
public sealed partial class STNewsSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<AccessLevelPrototype> JournalistAccess = "Journalist";
    private static readonly ProtoId<STBandPrototype> ClearSkyBandId = "STClearSkyBand";

    /// <summary>In-memory article cache, newest first.</summary>
    private readonly List<STNewsArticle> _articles = new();

    /// <summary>In-memory comment cache, keyed by article ID, chronological order.</summary>
    private readonly Dictionary<int, List<STNewsComment>> _comments = new();

    /// <summary>Loaders (PDAs) with the news cartridge currently active.</summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    /// <summary>All known news cartridge loader UIDs, for sending notifications without a global query.</summary>
    private readonly HashSet<EntityUid> _allLoaders = new();

    /// <summary>Cached summary list, invalidated on publish/delete/comment. Avoids re-running regex strip on every UI open.</summary>
    private List<STNewsArticleSummary>? _cachedSummaries;

    private WebhookIdentifier? _webhookId;
    private bool _cacheReady;

    private static readonly Regex MarkupTagRegex = new(@"\[/?[^\]]+\]", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<STNewsCartridgeComponent, STOpenNewsArticleEvent>(OnOpenArticle);
        SubscribeLocalEvent<STNewsCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        SubscribeLocalEvent<STNewsCartridgeComponent, EntityTerminatingEvent>(OnCartridgeTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        LoadFromDatabaseAsync();

        _config.OnValueChanged(STCCVars.NewsWebhook, OnWebhookChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _config.UnsubValueChanged(STCCVars.NewsWebhook, OnWebhookChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_reactionBroadcastPending)
            return;

        if (_timing.CurTime < _nextReactionBroadcast)
            return;

        _reactionBroadcastPending = false;
        BroadcastUiUpdate();
    }

    private void OnWebhookChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _discord.GetWebhook(value, data => _webhookId = data.ToIdentifier());
        else
            _webhookId = null;
    }

    #region Database Loading

    private async void LoadFromDatabaseAsync()
    {
        try
        {
            var maxCached = _config.GetCVar(STCCVars.NewsMaxCachedArticles);
            var dbArticles = await _dbManager.GetRecentStalkerNewsArticlesAsync(maxCached);

            foreach (var dbArticle in dbArticles)
            {
                _articles.Add(new STNewsArticle(
                    dbArticle.Id,
                    dbArticle.Title,
                    dbArticle.Content,
                    dbArticle.Author,
                    dbArticle.RoundId,
                    TimeSpan.FromTicks(dbArticle.PublishTimeTicks),
                    dbArticle.EmbedColor));
            }

            // Load comments for all cached articles
            if (_articles.Count > 0)
            {
                var articleIds = new List<int>(_articles.Count);
                foreach (var a in _articles)
                    articleIds.Add(a.Id);
                var dbComments = await _dbManager.GetStalkerNewsCommentsAsync(articleIds);

                foreach (var dbComment in dbComments)
                {
                    var comment = new STNewsComment(
                        dbComment.Id,
                        dbComment.ArticleId,
                        dbComment.Author,
                        dbComment.Content,
                        dbComment.RoundId,
                        TimeSpan.FromTicks(dbComment.PostedTimeTicks),
                        dbComment.AuthorFaction);

                    if (!_comments.TryGetValue(dbComment.ArticleId, out var list))
                    {
                        list = new List<STNewsComment>();
                        _comments[dbComment.ArticleId] = list;
                    }

                    list.Add(comment);
                }
            }

            _cachedSummaries = null;

            // Load reactions for cached articles
            LoadReactionsFromDatabaseAsync();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load news articles from database: {e}");
        }
        finally
        {
            _cacheReady = true;
        }
    }

    #endregion

    #region Cartridge Events

    private void OnUiReady(EntityUid uid, STNewsCartridgeComponent comp, CartridgeUiReadyEvent args)
    {
        if (!_cacheReady)
            return;

        // Compute new article IDs BEFORE updating LastSeenArticleId so the first view shows them
        var newIds = GetNewArticleIds(comp);

        if (_articles.Count > 0)
            comp.LastSeenArticleId = _articles[0].Id;

        // Initialize comment counts so existing comments don't show as "NEW" on first open
        if (comp.LastSeenCommentCounts.Count == 0)
        {
            foreach (var article in _articles)
            {
                if (_comments.TryGetValue(article.Id, out var list) && list.Count > 0)
                    comp.LastSeenCommentCounts[article.Id] = list.Count;
            }
        }

        if (TryComp<CartridgeComponent>(uid, out var cartComp) && cartComp.HasNotification)
        {
            cartComp.HasNotification = false;
            Dirty(uid, cartComp);
        }

        int? openArticleId = null;
        STNewsArticle? openArticle = null;
        var canDelete = false;
        List<STNewsComment>? comments = null;

        // Consume pending article navigation
        if (comp.PendingArticleId is { } pendingId)
        {
            comp.PendingArticleId = null;
            comp.ViewingArticleId = pendingId;
            openArticleId = pendingId;
            openArticle = FindArticleById(pendingId);

            if (openArticle != null)
            {
                canDelete = CanDeleteArticleForLoader(openArticle, args.Loader);
                _comments.TryGetValue(pendingId, out var rawComments);
                comments = rawComments != null ? new List<STNewsComment>(rawComments) : null;

                var commentCount = rawComments?.Count ?? 0;
                comp.LastSeenCommentCounts[pendingId] = commentCount;
            }
        }

        var canWrite = HasJournalistAccessForLoader(args.Loader);
        var deletableIds = GetDeletableArticleIdsForLoader(args.Loader);
        var newCommentIds = GetNewCommentArticleIds(comp);
        var viewerUserId = ResolveViewerUserId(args.Loader);
        var articleReactions = BuildAllArticleReactions(viewerUserId);
        var state = new STNewsUiState(
            GetCachedSummaries(), canWrite, openArticleId, openArticle, newIds,
            canDelete, comments, deletableIds, newCommentIds, articleReactions);
        _cartridgeLoader.UpdateCartridgeUiState(args.Loader, state);
    }

    private void OnCartridgeAdded(EntityUid uid, STNewsCartridgeComponent comp, CartridgeAddedEvent args)
    {
        _allLoaders.Add(args.Loader);
    }

    private void OnCartridgeActivated(EntityUid uid, STNewsCartridgeComponent comp, CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);
    }

    private void OnCartridgeDeactivated(EntityUid uid, STNewsCartridgeComponent comp, CartridgeDeactivatedEvent args)
    {
        // Engine bug workaround: DeactivateProgram passes programUid as args.Loader
        if (TryComp<CartridgeComponent>(uid, out var cartridge) && cartridge.LoaderUid is { } loaderUid)
            _activeLoaders.Remove(loaderUid);
        else
            _activeLoaders.Remove(args.Loader);
    }

    private void OnCartridgeRemoved(Entity<STNewsCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        _activeLoaders.Remove(args.Loader);
        _allLoaders.Remove(args.Loader);
    }

    private void OnCartridgeTerminating(EntityUid uid, STNewsCartridgeComponent comp, ref EntityTerminatingEvent args)
    {
        if (TryComp<CartridgeComponent>(uid, out var cartridge) && cartridge.LoaderUid is { } loaderUid)
        {
            _activeLoaders.Remove(loaderUid);
            _allLoaders.Remove(loaderUid);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        // _articles intentionally NOT cleared — articles persist across rounds (loaded once from DB)
        _activeLoaders.Clear();
        _allLoaders.Clear();
        _cachedSummaries = null;
        _reactionBroadcastPending = false;
    }

    #endregion

    #region Message Dispatch

    private void OnMessage(EntityUid uid, STNewsCartridgeComponent comp, CartridgeMessageEvent args)
    {
        switch (args)
        {
            case STNewsPublishEvent publish:
                OnPublish(uid, comp, publish, args);
                break;
            case STNewsRequestArticleEvent request:
                OnRequestArticle(comp, request, args);
                break;
            case STNewsDeleteArticleEvent delete:
                OnDelete(delete, args);
                break;
            case STNewsPostCommentEvent comment:
                OnPostComment(comment, args);
                break;
            case STNewsToggleReactionEvent reaction:
                OnToggleReaction(uid, comp, reaction, args);
                break;
            case STNewsCloseArticleEvent:
                OnCloseArticle(uid, comp);
                break;
        }
    }

    private void OnPublish(
        EntityUid uid,
        STNewsCartridgeComponent comp,
        STNewsPublishEvent publish,
        CartridgeMessageEvent args)
    {
        if (!HasJournalistAccess(args.Actor))
            return;

        if (_timing.CurTime < comp.NextPublishTime)
            return;

        comp.NextPublishTime = _timing.CurTime + TimeSpan.FromSeconds(
            _config.GetCVar(STCCVars.NewsPublishCooldownSeconds));

        var title = publish.Title.Trim();
        var content = publish.Content.Trim();

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
            return;

        if (title.Length > STNewsConstants.MaxTitleLength)
            title = title[..STNewsConstants.MaxTitleLength];

        if (content.Length > STNewsConstants.MaxContentLength)
            content = content[..STNewsConstants.MaxContentLength];

        var author = MetaData(args.Actor).EntityName;

        _adminLogger.Add(
            LogType.STNews,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} published news article: \"{title}\"");

        PublishArticleAsync(title, content, author, publish.EmbedColor);
    }

    private void OnRequestArticle(
        STNewsCartridgeComponent comp,
        STNewsRequestArticleEvent request,
        CartridgeMessageEvent args)
    {
        comp.ViewingArticleId = request.ArticleId;

        var article = FindArticleById(request.ArticleId);
        var canWrite = HasJournalistAccess(args.Actor);
        var loaderUid = GetEntity(args.LoaderUid);
        var newIds = GetNewArticleIds(comp);

        var canDelete = false;
        List<STNewsComment>? comments = null;

        if (article != null)
        {
            canDelete = CanDeleteArticle(article, args.Actor);
            _comments.TryGetValue(request.ArticleId, out var rawComments);
            comments = rawComments != null ? new List<STNewsComment>(rawComments) : null;

            var commentCount = rawComments?.Count ?? 0;
            comp.LastSeenCommentCounts[request.ArticleId] = commentCount;
        }

        var deletableIds = GetDeletableArticleIds(args.Actor);
        var newCommentIds = GetNewCommentArticleIds(comp);
        var viewerUserId = ResolveViewerUserId(loaderUid);
        var articleReactions = BuildAllArticleReactions(viewerUserId);
        var state = new STNewsUiState(
            GetCachedSummaries(), canWrite, request.ArticleId, article, newIds,
            canDelete, comments, deletableIds, newCommentIds, articleReactions);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private void OnDelete(STNewsDeleteArticleEvent delete, CartridgeMessageEvent args)
    {
        if (!HasJournalistAccess(args.Actor))
            return;

        var article = FindArticleById(delete.ArticleId);
        if (article == null)
            return;

        // Only allow deletion of own articles from the current round
        if (article.RoundId != _gameTicker.RoundId)
            return;

        var actorName = MetaData(args.Actor).EntityName;
        if (article.Author != actorName)
            return;

        for (var i = 0; i < _articles.Count; i++)
        {
            if (_articles[i].Id == delete.ArticleId)
            {
                _articles.RemoveAt(i);
                break;
            }
        }
        _comments.Remove(delete.ArticleId);
        RemoveReactionCacheForArticle(delete.ArticleId);
        _cachedSummaries = null;

        _adminLogger.Add(
            LogType.STNews,
            LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} deleted news article #{delete.ArticleId}: \"{article.Title}\"");

        DeleteArticleAsync(delete.ArticleId);
        DeleteReactionsForArticleAsync(delete.ArticleId);

        BroadcastUiUpdate();
    }

    private void OnPostComment(STNewsPostCommentEvent comment, CartridgeMessageEvent args)
    {
        var article = FindArticleById(comment.ArticleId);
        if (article == null)
            return;

        var loaderUid = GetEntity(args.LoaderUid);
        if (!_cartridgeLoader.TryGetProgram<STNewsCartridgeComponent>(loaderUid, out _, out var newsComp))
            return;

        var content = comment.Content.Trim();
        if (string.IsNullOrEmpty(content))
            return;

        if (content.Length > STNewsConstants.MaxCommentLength)
            content = content[..STNewsConstants.MaxCommentLength];

        var maxComments = _config.GetCVar(STCCVars.NewsMaxCommentsPerArticle);
        if (_comments.TryGetValue(comment.ArticleId, out var existing) && existing.Count >= maxComments)
            return;

        var cooldownSeconds = _config.GetCVar(STCCVars.NewsCommentCooldownSeconds);
        if (_timing.CurTime < newsComp.NextCommentTime)
            return;

        newsComp.NextCommentTime = _timing.CurTime + TimeSpan.FromSeconds(cooldownSeconds);

        var author = MetaData(args.Actor).EntityName;
        var faction = ResolveFaction(args.Actor);

        _adminLogger.Add(
            LogType.STNews,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} commented on news article #{comment.ArticleId}");

        PostCommentAsync(comment.ArticleId, author, content, faction);
    }

    #endregion

    #region Cross-Cartridge Navigation

    private void OnOpenArticle(EntityUid uid, STNewsCartridgeComponent comp, ref STOpenNewsArticleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        comp.PendingArticleId = args.ArticleId;
        _cartridgeLoader.ActivateProgram(args.LoaderUid, uid);
    }

    private void OnCloseArticle(EntityUid uid, STNewsCartridgeComponent comp)
    {
        comp.ViewingArticleId = null;
    }

    #endregion

    #region Broadcast

    private void BroadcastUiUpdate()
    {
        if (_activeLoaders.Count == 0)
            return;

        var summaries = GetCachedSummaries();
        var query = EntityQueryEnumerator<STNewsCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out var newsComp, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loaderUid)
                continue;

            if (!_activeLoaders.Contains(loaderUid))
                continue;

            if (!Exists(loaderUid))
                continue;

            var canWrite = HasJournalistAccessForLoader(loaderUid);
            var newIds = GetNewArticleIds(newsComp);

            int? openArticleId = null;
            STNewsArticle? openArticle = null;
            var canDelete = false;
            List<STNewsComment>? openComments = null;

            if (newsComp.ViewingArticleId is { } viewingId)
            {
                openArticle = FindArticleById(viewingId);
                if (openArticle != null)
                {
                    openArticleId = viewingId;
                    canDelete = CanDeleteArticleForLoader(openArticle, loaderUid);
                    _comments.TryGetValue(viewingId, out var rawComments);
                    openComments = rawComments != null ? new List<STNewsComment>(rawComments) : null;

                    var commentCount = rawComments?.Count ?? 0;
                    newsComp.LastSeenCommentCounts[viewingId] = commentCount;
                }
            }

            var deletableIds = GetDeletableArticleIdsForLoader(loaderUid);
            var newCommentIds = GetNewCommentArticleIds(newsComp);
            var viewerUserId = ResolveViewerUserId(loaderUid);
            var articleReactions = BuildAllArticleReactions(viewerUserId);
            var state = new STNewsUiState(
                summaries, canWrite, openArticleId, openArticle, newIds,
                canDelete, openComments, deletableIds, newCommentIds, articleReactions);
            _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
        }
    }

    private void SendNotifications(STNewsArticle article)
    {
        foreach (var loaderUid in _allLoaders)
        {
            if (!_cartridgeLoader.TryGetProgram<STNewsCartridgeComponent>(loaderUid, out var progUid, out _))
                continue;

            if (!TryComp<CartridgeComponent>(progUid, out var cartridge))
                continue;

            cartridge.HasNotification = true;
            Dirty(progUid.Value, cartridge);

            if (TryComp<RingerComponent>(loaderUid, out var ringer))
                _ringer.RingerPlayRingtone((loaderUid, ringer));
        }
    }

    #endregion

    #region Database Persistence

    /// <summary>
    /// Saves the article to DB, then inserts into cache and broadcasts.
    /// Awaiting the DB save ensures clients receive the correct DB-assigned ID.
    /// </summary>
    private async void PublishArticleAsync(
        string title,
        string content,
        string author,
        int embedColor)
    {
        try
        {
            var dbArticle = new StalkerNewsArticle
            {
                Title = title,
                Content = content,
                Author = author,
                RoundId = _gameTicker.RoundId,
                PublishTimeTicks = _gameTicker.RoundDuration().Ticks,
                EmbedColor = embedColor,
                CreatedAt = DateTime.UtcNow,
            };

            var dbId = await _dbManager.AddStalkerNewsArticleAsync(dbArticle);

            var article = new STNewsArticle(
                dbId,
                title,
                content,
                author,
                dbArticle.RoundId,
                TimeSpan.FromTicks(dbArticle.PublishTimeTicks),
                embedColor);

            var maxCached = _config.GetCVar(STCCVars.NewsMaxCachedArticles);
            _articles.Insert(0, article);
            if (_articles.Count > maxCached)
            {
                var evicted = _articles[_articles.Count - 1];
                _articles.RemoveAt(_articles.Count - 1);
                _comments.Remove(evicted.Id);
                RemoveReactionCacheForArticle(evicted.Id);
            }

            _cachedSummaries = null;

            SendDiscordArticle(article);
            BroadcastUiUpdate();
            SendNotifications(article);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to publish news article: {e}");
        }
    }

    private async void DeleteArticleAsync(int articleId)
    {
        try
        {
            await _dbManager.DeleteStalkerNewsArticleAsync(articleId);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to delete news article #{articleId} from database: {e}");
        }
    }

    private async void PostCommentAsync(int articleId, string author, string content, string? faction)
    {
        try
        {
            var dbComment = new StalkerNewsComment
            {
                ArticleId = articleId,
                Author = author,
                Content = content,
                RoundId = _gameTicker.RoundId,
                PostedTimeTicks = _gameTicker.RoundDuration().Ticks,
                CreatedAt = DateTime.UtcNow,
                AuthorFaction = faction,
            };

            var dbId = await _dbManager.AddStalkerNewsCommentAsync(dbComment);

            var comment = new STNewsComment(
                dbId,
                articleId,
                author,
                content,
                dbComment.RoundId,
                TimeSpan.FromTicks(dbComment.PostedTimeTicks),
                faction);

            if (!_comments.TryGetValue(articleId, out var list))
            {
                list = new List<STNewsComment>();
                _comments[articleId] = list;
            }

            list.Add(comment);
            _cachedSummaries = null; // Comment count changed

            // Broadcast updates all active viewers, including those viewing this article
            BroadcastUiUpdate();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to post comment on news article #{articleId}: {e}");
        }
    }

    #endregion

    #region Discord

    private async void SendDiscordArticle(STNewsArticle article)
    {
        if (_webhookId is null)
            return;

        try
        {
            var embed = new WebhookEmbed
            {
                Title = article.Title,
                Description = FormattedMessage.RemoveMarkupPermissive(article.Content),
                Color = article.EmbedColor & 0xFFFFFF,
                Footer = new WebhookEmbedFooter
                {
                    Text = Loc.GetString("st-news-discord-footer",
                        ("author", article.Author),
                        ("round", article.RoundId),
                        ("time", article.PublishTime.ToString(@"hh\:mm\:ss"))),
                },
            };

            var payload = new WebhookPayload { Embeds = new List<WebhookEmbed> { embed } };
            await _discord.CreateMessage(_webhookId.Value, payload);
        }
        catch (Exception e)
        {
            Log.Error($"Error sending news article to Discord: {e}");
        }
    }

    #endregion

    #region Helpers

    private bool HasJournalistAccess(EntityUid actor)
    {
        var tags = _accessReader.FindAccessTags(actor);
        return tags.Contains(JournalistAccess);
    }

    private bool HasJournalistAccessForLoader(EntityUid loaderUid)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return false;

        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return false;

        return HasJournalistAccess(holder);
    }

    /// <summary>
    /// Checks whether the given actor can delete the given article.
    /// Must be journalist, same author name, and current round.
    /// </summary>
    private bool CanDeleteArticle(STNewsArticle article, EntityUid actor)
    {
        if (!HasJournalistAccess(actor))
            return false;

        if (article.RoundId != _gameTicker.RoundId)
            return false;

        var actorName = MetaData(actor).EntityName;
        return article.Author == actorName;
    }

    /// <summary>
    /// Checks whether the holder of a loader (PDA) can delete the given article.
    /// </summary>
    private bool CanDeleteArticleForLoader(STNewsArticle article, EntityUid loaderUid)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return false;

        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return false;

        return CanDeleteArticle(article, holder);
    }

    /// <summary>
    /// Returns the cached summary list, rebuilding it only when invalidated.
    /// </summary>
    private List<STNewsArticleSummary> GetCachedSummaries()
    {
        if (_cachedSummaries != null)
            return _cachedSummaries;

        var summaries = new List<STNewsArticleSummary>(_articles.Count);
        foreach (var article in _articles)
        {
            var commentCount = _comments.TryGetValue(article.Id, out var comments) ? comments.Count : 0;
            summaries.Add(new STNewsArticleSummary(
                article.Id,
                article.Title,
                StripAndTruncate(article.Content, STNewsConstants.PreviewLength),
                article.Author,
                article.RoundId,
                article.PublishTime,
                article.EmbedColor,
                commentCount));
        }

        _cachedSummaries = summaries;
        return summaries;
    }

    private STNewsArticle? FindArticleById(int id)
    {
        foreach (var article in _articles)
        {
            if (article.Id == id)
                return article;
        }

        return null;
    }

    private HashSet<int> GetNewArticleIds(STNewsCartridgeComponent comp)
    {
        var ids = new HashSet<int>();
        foreach (var article in _articles)
        {
            if (article.Id > comp.LastSeenArticleId)
                ids.Add(article.Id);
            else
                break; // articles are newest-first, so we can stop early
        }

        return ids;
    }

    private HashSet<int> GetDeletableArticleIds(EntityUid actor)
    {
        if (!HasJournalistAccess(actor))
            return new HashSet<int>();

        var actorName = MetaData(actor).EntityName;
        var ids = new HashSet<int>();
        foreach (var article in _articles)
        {
            if (article.RoundId == _gameTicker.RoundId && article.Author == actorName)
                ids.Add(article.Id);
        }

        return ids;
    }

    private HashSet<int> GetDeletableArticleIdsForLoader(EntityUid loaderUid)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return new HashSet<int>();

        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return new HashSet<int>();

        return GetDeletableArticleIds(holder);
    }

    private HashSet<int> GetNewCommentArticleIds(STNewsCartridgeComponent comp)
    {
        var ids = new HashSet<int>();
        foreach (var article in _articles)
        {
            var currentCount = _comments.TryGetValue(article.Id, out var list) ? list.Count : 0;
            if (currentCount == 0)
                continue;

            comp.LastSeenCommentCounts.TryGetValue(article.Id, out var lastSeen);
            if (currentCount > lastSeen)
                ids.Add(article.Id);
        }

        return ids;
    }

    /// <summary>
    /// Resolves the persistent UserId for the holder of a loader (PDA).
    /// Returns Guid.Empty if the holder has no ActorComponent.
    /// </summary>
    private Guid ResolveViewerUserId(EntityUid loaderUid)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return Guid.Empty;

        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return Guid.Empty;

        if (!TryComp<ActorComponent>(holder, out var actor))
            return Guid.Empty;

        return actor.PlayerSession.UserId.UserId;
    }

    private static string StripAndTruncate(string content, int maxLength)
    {
        var stripped = MarkupTagRegex.Replace(content, string.Empty);
        if (stripped.Length > maxLength)
            stripped = stripped[..maxLength] + "...";
        return stripped;
    }

    /// <summary>
    /// Resolves the faction name for an entity via BandsComponent.
    /// </summary>
    private string? ResolveFaction(EntityUid uid)
    {
        if (!TryComp<BandsComponent>(uid, out var bands))
            return null;

        // Only Clear Sky is disguised as Loners on PDA
        if (bands.BandProto == ClearSkyBandId)
            return _factionResolution.GetBandFactionName(bands.BandName);

        if (bands.BandProto is not { } bandProtoId)
            return null;

        if (!_protoManager.TryIndex(bandProtoId, out var bandProto))
            return null;

        return _factionResolution.GetBandFactionName(bandProto.Name);
    }

    #endregion
}
