using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.Camera;

/// <summary>
/// Component for a camera item that can capture viewport screenshots as in-game photographs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class STCameraComponent : Component
{
    /// <summary>
    /// ItemSlot ID for the film slot.
    /// </summary>
    public const string FilmSlotId = "film_slot";

    /// <summary>
    /// Duration of the "taking photo" DoAfter.
    /// </summary>
    [DataField]
    public TimeSpan CaptureDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Cooldown between photo captures.
    /// </summary>
    [DataField]
    public TimeSpan CaptureCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Next time the camera can be used. Paused when entity is on a paused map.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextCaptureTime;

    /// <summary>
    /// Prototype ID of the photo entity to spawn.
    /// </summary>
    [DataField]
    public EntProtoId PhotoPrototype = "STPhoto";

    /// <summary>
    /// Maximum JPEG byte size accepted from client.
    /// </summary>
    [DataField]
    public int MaxImageBytes = 150 * 1024;

    /// <summary>
    /// Sound played when taking a photo.
    /// </summary>
    [DataField]
    public SoundSpecifier CaptureSound = new SoundPathSpecifier("/Audio/Items/snap.ogg");

    /// <summary>
    /// Visual effect applied to photos taken with this camera.
    /// </summary>
    [DataField, AutoNetworkedField]
    public STPhotoEffect Effect = STPhotoEffect.None;
}
