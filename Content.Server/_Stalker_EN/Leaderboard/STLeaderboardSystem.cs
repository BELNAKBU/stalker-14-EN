using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.CartridgeLoader;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.CharacterRank;
using Content.Shared._Stalker_EN.Leaderboard;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers;
using Content.Shared.Ghost;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.Leaderboard;

/// <summary>
/// Server-side system that manages the Stalker Leaderboard cartridge.
/// Uses BandsComponent for faction display, NpcFactionSystem for relations.
/// Supports multiple characters per player (keyed by UserId + CharacterName).
/// </summary>
public sealed partial class STLeaderboardSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConsoleHost _consoleHost = default!;

    /// <summary>
    /// Cache of all known stalkers. Key includes both UserId and CharacterName.
    /// Stats are read live from PlayerStatsComponent, not cached.
    /// </summary>
    private readonly Dictionary<StalkerKey, (string Name, string? Band, string? BandIcon, string? FactionId, int RankIndex, string? RankName, EntityUid Mob)> _knownStalkers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<STLeaderboardCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);

        _consoleHost.RegisterCommand("leaderboard-clear",
            "Clears all entries from the stalker leaderboard.",
            "Usage: leaderboard-clear",
            LeaderboardClearCommand);

        _consoleHost.RegisterCommand("leaderboard-list",
            "Lists all entries in the stalker leaderboard.",
            "Usage: leaderboard-list",
            LeaderboardListCommand);

        _consoleHost.RegisterCommand("leaderboard-remove",
            "Removes entries matching a character name from the stalker leaderboard.",
            "Usage: leaderboard-remove <name>",
            LeaderboardRemoveCommand);
    }

    /// <summary>
    /// Returns true if the entity is a real player MobHuman.
    /// All doll/NPC prototypes use different IDs, so this strictly filters real players.
    /// </summary>
    private bool IsPlayerMob(EntityUid mob) => MetaData(mob).EntityPrototype?.ID == "MobHuman";

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var session = args.Player;

        // Skip admins
        if (_adminManager.GetAdminData(session) != null)
            return;

        if (session.AttachedEntity is not { } mob)
            return;

        if (HasComp<GhostComponent>(mob))
            return;

        // Only real player mobs (MobHuman)
        if (!IsPlayerMob(mob))
            return;

        UpdateStalkerEntry(session);
        BroadcastUiState();
    }

    /// <summary>
    /// Handles UI messages from the client (e.g. Refresh button).
    /// </summary>
    private void OnUiMessage(EntityUid uid, STLeaderboardCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not STLeaderboardUiMessage msg || msg.Action != STLeaderboardUiAction.Refresh)
            return;

        CollectOnlineEntries();
        BroadcastUiState();
    }

    /// <summary>
    /// Called when the cartridge UI is first opened.
    /// </summary>
    private void OnUiReady(EntityUid uid, STLeaderboardCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        CollectOnlineEntries();
        SendUiState(args.Loader);
    }

    /// <summary>
    /// Gets the primary NpcFaction ID for relation checks.
    /// Prefers specific factions over generic tags like Mutated/Hostile.
    /// </summary>
    private string? GetPrimaryFaction(EntityUid mob)
    {
        if (!TryComp<NpcFactionMemberComponent>(mob, out var factionComp) || factionComp.Factions.Count == 0)
            return null;

        foreach (var f in factionComp.Factions)
        {
            if (SpecificFactions.Contains(f))
                return f;
        }

        return factionComp.Factions.FirstOrDefault();
    }

    /// <summary>
    /// Updates or creates a leaderboard entry for a specific player using their band and faction data.
    /// </summary>
    private void UpdateStalkerEntry(ICommonSession session)
    {
        if (session.AttachedEntity is not { } mob)
            return;

        var characterName = MetaData(mob).EntityName;
        if (string.IsNullOrEmpty(characterName))
            return;

        var key = new StalkerKey(session.UserId, characterName);

        // Get band (faction) from BandsComponent
        string? bandName = null;
        string? bandIcon = null;
        if (TryComp<BandsComponent>(mob, out var bands))
        {
            bandName = bands.BandName;
            bandIcon = bands.BandStatusIcon;
        }

        // Get the primary NpcFaction for relation checks
        var factionId = GetPrimaryFaction(mob);

        // Get rank
        string? rankName = null;
        int rankIndex = 0;
        if (TryComp<STCharacterRankComponent>(mob, out var rankComp))
        {
            rankName = rankComp.RankName;
            rankIndex = rankComp.RankIndex;
        }

        _knownStalkers[key] = (characterName, bandName, bandIcon, factionId, rankIndex, rankName, mob);
    }

    /// <summary>
    /// Faction IDs that represent specific, displayable factions (not generic behavior tags).
    /// </summary>
    private static readonly HashSet<string> SpecificFactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stalker", "Sci", "Military", "Bandit", "Seraphims", "Freedom", "Duty",
        "MilitaryStalker", "Evolvers", "ClearSky", "Police", "Dolg", "Mercenaries",
        "Sin", "Deserters", "Traders", "Scientists", "SSU", "Project", "Pilgrims",
        "Covenant", "Poisk", "Monolith", "MutatedHunter",
    };

    /// <summary>
    /// Collects entries from all currently connected valid players.
    /// </summary>
    private void CollectOnlineEntries()
    {
        foreach (var session in _playerManager.Sessions)
        {
            // Skip admins
            if (_adminManager.GetAdminData(session) != null)
                continue;

            if (session.AttachedEntity is not { } mob)
                continue;

            if (HasComp<GhostComponent>(mob))
                continue;

            if (!IsPlayerMob(mob))
                continue;

            UpdateStalkerEntry(session);
        }
    }

    /// <summary>
    /// Computes the relation type. Distinguishes mutual hostility (War) from one-way hostility (Hostile).
    /// </summary>
    private STLeaderboardFactionRelation GetRelation(string? viewerFaction, string? targetFaction)
    {
        if (string.IsNullOrEmpty(viewerFaction) || string.IsNullOrEmpty(targetFaction))
            return STLeaderboardFactionRelation.Neutral;

        if (viewerFaction == targetFaction)
            return STLeaderboardFactionRelation.Same;

        // Mutual friendly → green
        if (_npcFaction.IsFactionFriendly(viewerFaction, targetFaction))
            return STLeaderboardFactionRelation.Alliance;

        // Mutual hostile → red (War)
        if (_npcFaction.IsFactionHostile(viewerFaction, targetFaction))
            return STLeaderboardFactionRelation.War;

        // One-way hostile → orange (Hostile)
        if (_proto.TryIndex<NpcFactionPrototype>(viewerFaction, out var vProto) &&
            _proto.TryIndex<NpcFactionPrototype>(targetFaction, out var tProto))
        {
            if (vProto.Hostile.Contains(targetFaction) || tProto.Hostile.Contains(viewerFaction))
                return STLeaderboardFactionRelation.Hostile;
        }

        // Default → yellow
        return STLeaderboardFactionRelation.Neutral;
    }

    /// <summary>
    /// Broadcasts personalized leaderboard state to all open cartridges.
    /// Each viewer gets colors relative to their own faction.
    /// </summary>
    private void BroadcastUiState()
    {
        var viewerMap = new Dictionary<EntityUid, ICommonSession>();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;

            // Check if mob itself is the loader (PDA attached directly)
            if (TryComp<CartridgeLoaderComponent>(mob, out var loader) &&
                TryComp<STLeaderboardCartridgeComponent>(loader.ActiveProgram, out _))
            {
                viewerMap[mob] = session;
            }

            // Check PDAs in the mob's containers (hands, belt, pockets, etc.)
            if (TryComp<ContainerManagerComponent>(mob, out var contMan))
            {
                foreach (var cont in contMan.GetAllContainers())
                {
                    foreach (var item in cont.ContainedEntities)
                    {
                        if (TryComp<CartridgeLoaderComponent>(item, out var itemLoader) &&
                            TryComp<STLeaderboardCartridgeComponent>(itemLoader.ActiveProgram, out _))
                        {
                            viewerMap[item] = session;
                        }
                    }
                }
            }
        }

        var query = AllEntityQuery<STLeaderboardCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out _, out var cartridgeComp))
        {
            if (cartridgeComp.LoaderUid is not { } loaderUid)
                continue;

            viewerMap.TryGetValue(loaderUid, out var viewerSession);
            SendUiState(loaderUid, viewerSession);
        }
    }

    /// <summary>
    /// Sends a personalized leaderboard state via the cartridge UI.
    /// Colors are computed relative to the viewer's faction.
    /// The viewer's own entry is marked with IsMe=true for client-side pinning.
    /// </summary>
    private void SendUiState(EntityUid loaderUid, ICommonSession? viewerSession = null)
    {
        string? viewerFaction = null;
        string? viewerName = null;

        if (viewerSession?.AttachedEntity is { } viewerMob)
        {
            viewerFaction = GetPrimaryFaction(viewerMob);
            viewerName = MetaData(viewerMob).EntityName;
        }

        var entries = _knownStalkers.Values
            .Where(v => v.Mob.IsValid())
            .Select(v =>
            {
                int kills = 0;
                int arts = 0;
                if (TryComp<PlayerStatsComponent>(v.Mob, out var stats))
                {
                    kills = stats.MutantsKilled;
                    arts = stats.ArtifactsFound;
                }

                return new STLeaderboardEntry(
                    v.Name,
                    v.Band,
                    v.RankIndex,
                    v.RankName,
                    GetRelation(viewerFaction, v.FactionId),
                    IsMe: viewerName != null && v.Name == viewerName,
                    MutantsKilled: kills,
                    ArtifactsFound: arts);
            })
            .OrderByDescending(e => e.RankIndex)
            .ThenBy(e => e.CharacterName)
            .ToList();

        var state = new STLeaderboardUiState(entries);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    /// <summary>
    /// Clears all entries from the leaderboard (admin use).
    /// </summary>
    public void ClearLeaderboard()
    {
        _knownStalkers.Clear();
        BroadcastUiState();
    }

    /// <summary>
    /// Removes a specific stalker from the leaderboard.
    /// </summary>
    public bool RemoveStalker(StalkerKey key)
    {
        if (_knownStalkers.Remove(key))
        {
            BroadcastUiState();
            return true;
        }
        return false;
    }
}
