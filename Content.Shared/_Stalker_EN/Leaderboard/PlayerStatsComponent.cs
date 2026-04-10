using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Leaderboard;

/// <summary>
/// Tracks per-session player statistics for the leaderboard.
/// Attached to player mobs on spawn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlayerStatsComponent : Component
{
    /// <summary>
    /// Number of mutants killed by this player in the current session.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MutantsKilled;

    /// <summary>
    /// Number of artifacts found by this player in the current session.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ArtifactsFound;
}
