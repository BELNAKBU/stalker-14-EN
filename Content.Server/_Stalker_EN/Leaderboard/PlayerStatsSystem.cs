using System.Linq;
using Content.Server.KillTracking;
using Content.Shared._Stalker.ZoneArtifact.Components;
using Content.Shared._Stalker.ZoneArtifact.Events;
using Content.Shared._Stalker_EN.Leaderboard;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Stalker_EN.Leaderboard;

/// <summary>
/// Tracks player statistics for the leaderboard: mutant kills and artifact finds.
/// Integrates with KillTrackingSystem for accurate kill attribution.
/// Prevents artifact farming by removing the marker component immediately after pickup.
/// </summary>
public sealed class PlayerStatsSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>
    /// Prototype IDs that identify mutant mobs.
    /// </summary>
    private static readonly HashSet<string> MutantPrototypePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mutant", "MobMutant", "MobZombie", "MobSnork", "MobFlesh", "MobBloodsucker",
        "MobController", "MobPseudogiant", "MobBurer", "MobChimera", "MobCatDog",
        "MobTushkano", "MobRat", "MobBoar", "MobBear", "MobCrows", "MobSpider",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ZoneArtifactComponent, ZoneArtifactActivatedEvent>(OnArtifactDetected);
        SubscribeLocalEvent<STFoundArtifactComponent, EntGotInsertedIntoContainerMessage>(OnArtifactInserted);
        SubscribeLocalEvent<KillTrackerComponent, KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<MobStateComponent, MapInitEvent>(OnMobMapInit);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (args.Player.AttachedEntity is { } mob)
            EnsureComp<PlayerStatsComponent>(mob);
    }

    private void OnMobMapInit(Entity<MobStateComponent> ent, ref MapInitEvent args)
    {
        if (IsMutant(ent.Owner))
            EnsureComp<KillTrackerComponent>(ent.Owner);
    }

    private void OnKillReported(Entity<KillTrackerComponent> ent, ref KillReportedEvent args)
    {
        if (!IsMutant(ent.Owner))
            return;

        if (args.Primary is not KillPlayerSource playerSource || args.Suicide)
            return;

        if (!_playerManager.TryGetSessionById(playerSource.PlayerId, out var session) || session.AttachedEntity is not { } playerMob)
            return;

        if (IsMutant(playerMob))
            return;

        if (TryComp<PlayerStatsComponent>(playerMob, out var stats))
        {
            stats.MutantsKilled++;
            Dirty(playerMob, stats);
        }
    }

    private void OnArtifactDetected(Entity<ZoneArtifactComponent> artifact, ref ZoneArtifactActivatedEvent args)
    {
        if (!HasComp<STFoundArtifactComponent>(artifact))
            AddComp<STFoundArtifactComponent>(artifact);
    }

    private void OnArtifactInserted(Entity<STFoundArtifactComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var artifact = ent.Owner;
        var containerOwner = args.Container.Owner;

        if (!_playerManager.TryGetSessionByEntity(containerOwner, out var session) || session.AttachedEntity is not { } playerMob)
            return;

        if (playerMob != containerOwner)
            return;

        if (TryComp<PlayerStatsComponent>(playerMob, out var stats))
        {
            stats.ArtifactsFound++;
            Dirty(playerMob, stats);
        }

        RemCompDeferred<STFoundArtifactComponent>(artifact);
    }

    private bool IsMutant(EntityUid uid)
    {
        var protoId = MetaData(uid).EntityPrototype?.ID;
        if (string.IsNullOrEmpty(protoId))
            return false;

        foreach (var prefix in MutantPrototypePrefixes)
        {
            if (protoId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
