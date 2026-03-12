using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.ZoneAnomaly.Devices;

/// <summary>
/// Appearance keys for anomaly detector visual proximity indicator.
/// Used by <c>GenericVisualizer</c> on the client to drive sprite layer states.
/// </summary>
[Serializable, NetSerializable]
public enum STZoneAnomalyDetectorProximityVisuals : byte
{
    /// <summary>
    /// Proximity level mapped to a sprite layer.
    /// 0 = off, 1 = on/no target, 2+ = proximity graduated states or binary "detected".
    /// </summary>
    Level,
}

/// <summary>
/// Appearance keys for anomaly detector visual direction indicator.
/// Used by <c>GenericVisualizer</c> on the client to drive a compass sprite layer.
/// </summary>
[Serializable, NetSerializable]
public enum STZoneAnomalyDetectorDirectionVisuals : byte
{
    /// <summary>
    /// Direction state mapped to a sprite layer.
    /// -1 = off, 0-7 = 8-way compass, 8 = center, 9 = searching.
    /// </summary>
    Layer,
}
