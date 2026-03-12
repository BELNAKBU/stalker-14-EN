using Content.Shared._Stalker.ZoneAnomaly.Components;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Devices;

/// <summary>
/// Drives an 8-way directional compass on anomaly detectors showing direction
/// to the nearest detectable anomaly. Attach alongside <see cref="ZoneAnomalyDetectorComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class STZoneAnomalyDetectorDirectionIndicatorComponent : Component
{
    /// <summary>
    /// Distance at which direction snaps to "center" (standing on top of anomaly).
    /// </summary>
    [DataField]
    public float CenterDistance = 1f;
}
