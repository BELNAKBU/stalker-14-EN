using Content.Shared.Dataset;
using Content.Shared.Decals;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.AnonymousAlias;

/// <summary>
/// Shared base for the anonymous alias system. Provides access control for
/// <see cref="STAnonymousAliasComponent"/> and shared utilities.
/// </summary>
public abstract class SharedSTAnonymousAliasSystem : EntitySystem
{
    public static readonly ProtoId<LocalizedDatasetPrototype> AdjDatasetId = "STAliasAdjectives";
    public static readonly ProtoId<LocalizedDatasetPrototype> NounDatasetId = "STAliasNouns";
    public static readonly ProtoId<ColorPalettePrototype> ColorPaletteId = "STAliasColors";
}
