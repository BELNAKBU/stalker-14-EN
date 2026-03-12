using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker_EN.ZoneAnomaly.Devices;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.ZoneAnomaly.Devices;

/// <summary>
/// Drives visual proximity and direction indicators on anomaly detectors.
/// Runs alongside the upstream <c>ZoneAnomalyDetectorSystem</c> (which handles audio)
/// without modifying it.
/// </summary>
public sealed class STZoneAnomalyDetectorVisualIndicatorSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float ProximityUpdateInterval = 0.25f;
    private const float DirectionUpdateInterval = 0.1f;

    /// <summary>Direction state: indicator is off (detector disabled).</summary>
    private const int DirectionOff = -1;

    /// <summary>Direction state: standing on top of anomaly.</summary>
    private const int DirectionCenter = 8;

    /// <summary>Direction state: searching (enabled, no target found).</summary>
    private const int DirectionSearching = 9;

    private TimeSpan _nextProximityUpdate;
    private TimeSpan _nextDirectionUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime >= _nextProximityUpdate)
        {
            _nextProximityUpdate = curTime + TimeSpan.FromSeconds(ProximityUpdateInterval);
            UpdateProximity();
        }

        if (curTime >= _nextDirectionUpdate)
        {
            _nextDirectionUpdate = curTime + TimeSpan.FromSeconds(DirectionUpdateInterval);
            UpdateDirection();
        }
    }

    private void UpdateProximity()
    {
        var xformQuery = GetEntityQuery<TransformComponent>();

        var query = EntityQueryEnumerator<STZoneAnomalyDetectorProximityIndicatorComponent, ZoneAnomalyDetectorComponent>();
        while (query.MoveNext(out var uid, out var indicator, out var detector))
        {
            if (!detector.Enabled)
            {
                _appearance.SetData(uid, STZoneAnomalyDetectorProximityVisuals.Level, 0);
                continue;
            }

            if (!xformQuery.TryGetComponent(uid, out var xform))
                continue;

            var coords = _transform.GetMapCoordinates(xform);
            var detectorPos = _transform.GetWorldPosition(xform, xformQuery);

            float? closestDistance = null;
            foreach (var ent in _entityLookup.GetEntitiesInRange<ZoneAnomalyComponent>(coords, detector.Distance))
            {
                if (!ent.Comp.Detected || ent.Comp.DetectedLevel > detector.Level)
                    continue;

                var dist = (detectorPos - _transform.GetWorldPosition(ent, xformQuery)).Length();
                if (dist < (closestDistance ?? float.MaxValue))
                    closestDistance = dist;
            }

            if (closestDistance is not { } distance)
            {
                // On, no target in range
                _appearance.SetData(uid, STZoneAnomalyDetectorProximityVisuals.Level, 1);
                continue;
            }

            // Scale distance to proximity level: closer = higher level
            // Level 2 through (proximityStates + 1)
            var fraction = 1f - distance / detector.Distance;
            var level = (int)(fraction * indicator.ProximityStates) + 2;
            level = Math.Clamp(level, 2, indicator.ProximityStates + 1);

            _appearance.SetData(uid, STZoneAnomalyDetectorProximityVisuals.Level, level);
        }
    }

    private void UpdateDirection()
    {
        var xformQuery = GetEntityQuery<TransformComponent>();

        var query = EntityQueryEnumerator<STZoneAnomalyDetectorDirectionIndicatorComponent, ZoneAnomalyDetectorComponent>();
        while (query.MoveNext(out var uid, out var indicator, out var detector))
        {
            if (!detector.Enabled)
            {
                _appearance.SetData(uid, STZoneAnomalyDetectorDirectionVisuals.Layer, DirectionOff);
                continue;
            }

            if (!xformQuery.TryGetComponent(uid, out var xform))
                continue;

            var coords = _transform.GetMapCoordinates(xform);
            var detectorPos = _transform.GetWorldPosition(xform, xformQuery);

            float? closestDistance = null;
            EntityUid? closestEntity = null;
            foreach (var ent in _entityLookup.GetEntitiesInRange<ZoneAnomalyComponent>(coords, detector.Distance))
            {
                if (!ent.Comp.Detected || ent.Comp.DetectedLevel > detector.Level)
                    continue;

                var dist = (detectorPos - _transform.GetWorldPosition(ent, xformQuery)).Length();
                if (dist < (closestDistance ?? float.MaxValue))
                {
                    closestDistance = dist;
                    closestEntity = ent;
                }
            }

            if (closestDistance is not { } distance || closestEntity is not { } target)
            {
                _appearance.SetData(uid, STZoneAnomalyDetectorDirectionVisuals.Layer, DirectionSearching);
                continue;
            }

            if (distance <= indicator.CenterDistance)
            {
                _appearance.SetData(uid, STZoneAnomalyDetectorDirectionVisuals.Layer, DirectionCenter);
                continue;
            }

            var direction = _transform.GetWorldPosition(target) - detectorPos;
            _appearance.SetData(uid, STZoneAnomalyDetectorDirectionVisuals.Layer, (int) direction.ToAngle().GetDir());
        }
    }
}
