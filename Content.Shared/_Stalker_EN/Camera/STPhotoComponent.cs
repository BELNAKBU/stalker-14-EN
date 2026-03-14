using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Camera;

/// <summary>
/// Component for a photograph entity containing captured image data.
/// ImageData is NOT auto-networked; it is transferred on demand via STPhotoResponseEvent.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STPhotoComponent : Component
{
    /// <summary>
    /// Unique identifier for this photo's image data.
    /// Used for client-side texture caching and on-demand retrieval.
    /// </summary>
    [AutoNetworkedField]
    public Guid PhotoId = Guid.Empty;

    /// <summary>
    /// JPEG-encoded image data. Server-only storage, NOT auto-networked.
    /// Transferred on demand via STPhotoResponseEvent.
    /// </summary>
    [DataField]
    public byte[] ImageData = Array.Empty<byte>();

    /// <summary>
    /// Name of the player who took the photo.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string PhotographerName = string.Empty;

    /// <summary>
    /// In-game time when the photo was taken.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TimeTaken;
}
