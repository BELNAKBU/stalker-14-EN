namespace Content.Server._Stalker_EN.Teleportation;

/// <summary>
/// This is used for Linking portals via a common string
/// </summary>
[RegisterComponent]
public sealed partial class LinkByStringComponent : Component
{
    [DataField]
    public string? LinkString;

    /// <summary>
    /// Whether the prototype ID should be used as a fallback if a string isn't explicitly set
    /// </summary>
    [DataField]
    public bool FallbackId = true;
}
