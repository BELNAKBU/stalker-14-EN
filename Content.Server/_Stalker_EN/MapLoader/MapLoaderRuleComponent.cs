using Content.Shared._Stalker.Teleport;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker_EN.MapLoader;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class MapLoaderRuleComponent : Component
{
    public ProtoId<MapLoaderPrototype> prototype = "StalkerBaseMaps";
}
