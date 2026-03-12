using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.News;

/// <summary>
/// Categorizes what content a reaction targets. Extensible for future content types.
/// </summary>
[Serializable, NetSerializable]
public enum STReactionTargetType : byte
{
    Article = 0,
    Comment = 1,
}

/// <summary>
/// Aggregated reaction data for a single reaction type on a single target.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct STReactionSummary(string ReactionId, int Count, bool UserReacted);

/// <summary>
/// Static registry of available reaction types — faction band patch names.
/// IDs correspond to keys in <c>STFactionPatchIcons.PatchStates</c> on the client.
/// </summary>
public static class STReactionDefinitions
{
    public const string Loners = "Loners";
    public const string Freedom = "Freedom";
    public const string Bandits = "Bandits";
    public const string Duty = "Duty";
    public const string Ecologist = "Ecologist";
    public const string Neutrals = "Neutrals";
    public const string Mercenaries = "Mercenaries";
    public const string Military = "Military";
    public const string Monolith = "Monolith";
    public const string ClearSky = "ClearSky";
    public const string Renegades = "Renegades";
    public const string Rookies = "Rookies";
    public const string Journalists = "Journalists";
    public const string UN = "UN";

    public static readonly HashSet<string> Available = new()
    {
        Loners,
        Freedom,
        Bandits,
        Duty,
        Ecologist,
        Neutrals,
        Mercenaries,
        Military,
        Monolith,
        ClearSky,
        Renegades,
        Rookies,
        Journalists,
        UN,
    };

    /// <summary>
    /// Maps reaction IDs to their kebab-case locale keys.
    /// </summary>
    public static readonly Dictionary<string, string> LocKeys = new()
    {
        [Loners] = "st-news-reaction-loners",
        [Freedom] = "st-news-reaction-freedom",
        [Bandits] = "st-news-reaction-bandits",
        [Duty] = "st-news-reaction-duty",
        [Ecologist] = "st-news-reaction-ecologist",
        [Neutrals] = "st-news-reaction-neutrals",
        [Mercenaries] = "st-news-reaction-mercenaries",
        [Military] = "st-news-reaction-military",
        [Monolith] = "st-news-reaction-monolith",
        [ClearSky] = "st-news-reaction-clear-sky",
        [Renegades] = "st-news-reaction-renegades",
        [Rookies] = "st-news-reaction-rookies",
        [Journalists] = "st-news-reaction-journalists",
        [UN] = "st-news-reaction-un",
    };
}
