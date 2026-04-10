using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Leaderboard;

/// <summary>
/// Marker applied to artifacts found via detector.
/// Used to grant +1 point to the player who picks it up.
/// Removed immediately after pickup to prevent farming via buyback/storage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class STFoundArtifactComponent : Component
{
}
