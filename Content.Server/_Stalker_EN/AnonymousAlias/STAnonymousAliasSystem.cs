using Content.Shared._Stalker_EN.AnonymousAlias;
using Content.Shared.Dataset;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stalker_EN.AnonymousAlias;

/// <summary>
/// Server-side system that populates <see cref="STAnonymousAliasComponent"/> on player spawn
/// using profile data, with server-side allowlist validation against localized datasets.
/// </summary>
public sealed class STAnonymousAliasSystem : SharedSTAnonymousAliasSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var profile = args.Profile;
        var mob = args.Mob;

        var adjDataset = _proto.Index(AdjDatasetId);
        var nounDataset = _proto.Index(NounDatasetId);
        var palette = _proto.Index(ColorPaletteId);

        var adjective = ValidateAgainstDataset(profile.STAliasAdjective, adjDataset);
        var noun = ValidateAgainstDataset(profile.STAliasNoun, nounDataset);

        if (string.IsNullOrEmpty(adjective))
            adjective = _random.Pick(adjDataset);
        if (string.IsNullOrEmpty(noun))
            noun = _random.Pick(nounDataset);

        Color? aliasColor = null;
        if (!string.IsNullOrEmpty(profile.STAliasColor))
        {
            foreach (var (_, paletteColor) in palette.Colors)
            {
                if (paletteColor.ToHex() == profile.STAliasColor)
                {
                    aliasColor = paletteColor;
                    break;
                }
            }
        }

        var comp = EnsureComp<STAnonymousAliasComponent>(mob);
        comp.Alias = $"{adjective} {noun}";
        comp.AliasColor = aliasColor;
        Dirty(mob, comp);
    }

    /// <summary>
    /// Returns the localized value if it exists in the dataset, otherwise null.
    /// </summary>
    private string? ValidateAgainstDataset(string value, LocalizedDatasetPrototype dataset)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        foreach (var locId in dataset.Values)
        {
            if (Loc.GetString(locId) == value)
                return value;
        }

        return null;
    }
}
