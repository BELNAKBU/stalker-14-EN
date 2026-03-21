using Robust.Shared.GameStates;

namespace Content.Shared._Stalker_EN.AnonymousAlias;

/// <summary>
/// Stores a player's anonymous alias and optional name color, displayed when
/// their identity is blocked by a mask or helmet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSTAnonymousAliasSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class STAnonymousAliasComponent : Component
{
    /// <summary>
    /// The composed alias string, e.g. "Scarred Stalker".
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Alias = string.Empty;

    /// <summary>
    /// Player-chosen name color. Null means use default chat color behavior.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? AliasColor;
}
