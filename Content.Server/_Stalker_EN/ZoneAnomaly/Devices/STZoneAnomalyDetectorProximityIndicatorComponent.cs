using Content.Shared._Stalker.ZoneAnomaly.Components;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Devices;

/// <summary>
/// Drives visual proximity states on anomaly detectors based on distance
/// to the nearest detectable anomaly. Attach alongside <see cref="ZoneAnomalyDetectorComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class STZoneAnomalyDetectorProximityIndicatorComponent : Component
{
    /// <summary>
    /// Number of graduated proximity states beyond off/on.
    /// 2 = binary (detected / not), 4+ = graduated (far to danger).
    /// </summary>
    [DataField]
    public int ProximityStates = 2;
}
