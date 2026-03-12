namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// Marker component for the Stalker News PDA cartridge program.
/// </summary>
[RegisterComponent]
public sealed partial class STNewsCartridgeComponent : Component
{
    /// <summary>
    /// Set by <see cref="STOpenNewsArticleEvent"/>, consumed and cleared when building next UI state.
    /// </summary>
    [ViewVariables]
    public int? PendingArticleId;

    /// <summary>Newest article ID this user has seen. Articles with Id > this are "new".</summary>
    [ViewVariables]
    public int LastSeenArticleId;

    /// <summary>Earliest time this cartridge can publish again (server-side cooldown).</summary>
    [ViewVariables]
    public TimeSpan NextPublishTime;

    /// <summary>Article ID currently being viewed in detail, or null if on list view.</summary>
    [ViewVariables]
    public int? ViewingArticleId;

    /// <summary>Earliest time this cartridge can post another comment (server-side cooldown).</summary>
    [ViewVariables]
    public TimeSpan NextCommentTime;

    /// <summary>Last-seen comment count per article ID, for "new comments" badge.</summary>
    [ViewVariables]
    public Dictionary<int, int> LastSeenCommentCounts = new();

    /// <summary>Server-side cooldown for reaction toggling.</summary>
    [ViewVariables]
    public TimeSpan NextReactionTime;
}
