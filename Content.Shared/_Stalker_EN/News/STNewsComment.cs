using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// Lightweight comment DTO sent to clients for article detail view.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsComment
{
    public readonly int CommentId;
    public readonly int ArticleId;
    public readonly string Author;
    public readonly string Content;
    public readonly int RoundId;
    public readonly TimeSpan PostedTime;
    public readonly string? AuthorFaction;

    public STNewsComment(
        int commentId,
        int articleId,
        string author,
        string content,
        int roundId,
        TimeSpan postedTime,
        string? authorFaction = null)
    {
        CommentId = commentId;
        ArticleId = articleId;
        Author = author;
        Content = content;
        RoundId = roundId;
        PostedTime = postedTime;
        AuthorFaction = authorFaction;
    }
}
