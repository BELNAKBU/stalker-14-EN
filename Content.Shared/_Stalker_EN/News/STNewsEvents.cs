using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// Client requests publishing a new article. Sent via CartridgeMessageEvent.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsPublishEvent : CartridgeMessageEvent
{
    public readonly string Title;
    public readonly string Content;
    public readonly int EmbedColor;

    public STNewsPublishEvent(string title, string content, int embedColor)
    {
        Title = title;
        Content = content;
        EmbedColor = embedColor;
    }
}

/// <summary>
/// Client requests the full content of a specific article for detail view.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsRequestArticleEvent : CartridgeMessageEvent
{
    public readonly int ArticleId;

    public STNewsRequestArticleEvent(int articleId)
    {
        ArticleId = articleId;
    }
}

/// <summary>
/// Client requests deletion of own article (current round only).
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsDeleteArticleEvent : CartridgeMessageEvent
{
    public readonly int ArticleId;

    public STNewsDeleteArticleEvent(int articleId)
    {
        ArticleId = articleId;
    }
}

/// <summary>
/// Client posts a flat comment on an article.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsPostCommentEvent : CartridgeMessageEvent
{
    public readonly int ArticleId;
    public readonly string Content;

    public STNewsPostCommentEvent(int articleId, string content)
    {
        ArticleId = articleId;
        Content = content;
    }
}

/// <summary>
/// Client→Server: Toggle a reaction on a news article.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsToggleReactionEvent : CartridgeMessageEvent
{
    public readonly int ArticleId;
    public readonly string ReactionId;

    public STNewsToggleReactionEvent(int articleId, string reactionId)
    {
        ArticleId = articleId;
        ReactionId = reactionId;
    }
}

/// <summary>
/// Client signals it has navigated back from article detail to the list view.
/// Used to clear ViewingArticleId so broadcast updates stop including article detail.
/// </summary>
[Serializable, NetSerializable]
public sealed class STNewsCloseArticleEvent : CartridgeMessageEvent { }

/// <summary>
/// Local by-ref event raised on <see cref="STNewsCartridgeComponent"/> entities to request
/// opening a specific article. Decouples messenger -> news cartridge dependency.
/// </summary>
[ByRefEvent]
public record struct STOpenNewsArticleEvent(EntityUid LoaderUid, int ArticleId)
{
    /// <summary>Set to true by the handler that activates its program, preventing duplicate activation.</summary>
    public bool Handled;
}
