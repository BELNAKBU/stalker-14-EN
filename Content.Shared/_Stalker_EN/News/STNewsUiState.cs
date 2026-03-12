using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// UI state for the Stalker News cartridge program.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsUiState : BoundUserInterfaceState
{
    /// <summary>Article summaries, newest first.</summary>
    public readonly List<STNewsArticleSummary> Articles;

    /// <summary>Whether the current user has journalist access (can publish).</summary>
    public readonly bool CanWrite;

    /// <summary>Article ID to navigate to (from news link click).</summary>
    public readonly int? OpenArticleId;

    /// <summary>Full article content (when detail view is requested).</summary>
    public readonly STNewsArticle? OpenArticle;

    /// <summary>Article IDs that are new (unread) for this user.</summary>
    public readonly HashSet<int> NewArticleIds;

    /// <summary>Whether the current user can delete the open article.</summary>
    public readonly bool CanDeleteOpenArticle;

    /// <summary>Comments for the currently open article.</summary>
    public readonly List<STNewsComment>? OpenArticleComments;

    /// <summary>Article IDs the current user can delete (own articles, current round, journalist access).</summary>
    public readonly HashSet<int> DeletableArticleIds;

    /// <summary>Article IDs with comments the user hasn't seen yet.</summary>
    public readonly HashSet<int> NewCommentArticleIds;

    /// <summary>Reaction summaries per article. Only includes articles with at least one reaction.</summary>
    public readonly Dictionary<int, List<STReactionSummary>> ArticleReactions;

    public STNewsUiState(
        List<STNewsArticleSummary> articles,
        bool canWrite,
        int? openArticleId = null,
        STNewsArticle? openArticle = null,
        HashSet<int>? newArticleIds = null,
        bool canDeleteOpenArticle = false,
        List<STNewsComment>? openArticleComments = null,
        HashSet<int>? deletableArticleIds = null,
        HashSet<int>? newCommentArticleIds = null,
        Dictionary<int, List<STReactionSummary>>? articleReactions = null)
    {
        Articles = articles;
        CanWrite = canWrite;
        OpenArticleId = openArticleId;
        OpenArticle = openArticle;
        NewArticleIds = newArticleIds ?? new HashSet<int>();
        CanDeleteOpenArticle = canDeleteOpenArticle;
        OpenArticleComments = openArticleComments;
        DeletableArticleIds = deletableArticleIds ?? new HashSet<int>();
        NewCommentArticleIds = newCommentArticleIds ?? new HashSet<int>();
        ArticleReactions = articleReactions ?? new Dictionary<int, List<STReactionSummary>>();
    }
}
