using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.Surrender;

/// <summary>
/// Marker component applied to entities that are currently surrendering.
/// Used by the client to display the overhead icon.
/// The actual pacification behavior is handled by PacifiedComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurrenderedComponent : Component
{
}
