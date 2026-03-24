using Robust.Shared.GameStates;


namespace Content.Shared.Clothing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlotBlockOverrideComponent : Component
{
    [DataField]
    public string Tag = "BlockMask";

    [DataField, AutoNetworkedField]
    public bool Overridden;
}
