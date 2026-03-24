using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.Clothing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHelmetVisorSystem))]
public sealed partial class HelmetVisorComponent : Component
{
    /// <summary>
    /// Action prototype ID for toggling the visor.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ToggleHelmetVisorEvent";

    /// <summary>
    /// RSI state name when visor is up.
    /// </summary>
    [DataField]
    public string? IconStateUp;

    /// <summary>
    /// Entity UID of the toggle action (runtime).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;


    [DataField, AutoNetworkedField]
    public bool IsUp;


    [DataField, AutoNetworkedField]
    public string? EquippedPrefixUp;


    [DataField, AutoNetworkedField]
    public bool IsToggleable = true;

    [DataField]
    public float ToggleDelay = 1.5f;

    [DataField]
    public float LastToggleTime;

    /// <summary>
    /// Damage modifiers applied when visor is up.
    /// </summary>
    [DataField]
    public DamageModifierSet? VisorUpModifiers;

    [DataField]
    public DamageModifierSet? DefaultModifiers;

    [DataField]
    public SoundSpecifier SoundVisorUp = new SoundPathSpecifier("/Audio/_Stalker_EN/Clothing/Hats/vityaz_up.ogg");

    [DataField]
    public SoundSpecifier SoundVisorDown = new SoundPathSpecifier("/Audio/_Stalker_EN/Clothing/Hats/vityaz_down.ogg");

    /// <summary>
    /// Reflection probability when visor is up.
    /// </summary>
    [DataField]
    public float? VisorUpReflectProb;

    /// <summary>
    /// Default reflection probability (stored on init, not networked).
    /// </summary>
    [DataField]
    public float DefaultReflectProb;
}
