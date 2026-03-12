using Robust.Shared.Configuration;

namespace Content.Shared._Stalker_EN.CCVar;

public sealed partial class STCCVars
{
    /// <summary>
    ///     Discord webhook URL for Stalker News article notifications.
    /// </summary>
    public static readonly CVarDef<string> NewsWebhook =
        CVarDef.Create("stalkeren.news.discord_webhook", string.Empty,
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Per-player cooldown in seconds between article publishes. Default 60.
    /// </summary>
    public static readonly CVarDef<int> NewsPublishCooldownSeconds =
        CVarDef.Create("stalkeren.news.publish_cooldown_seconds", 60, CVar.SERVERONLY);

    /// <summary>
    ///     Maximum number of articles kept in the in-memory cache. Default 200.
    /// </summary>
    public static readonly CVarDef<int> NewsMaxCachedArticles =
        CVarDef.Create("stalkeren.news.max_cached_articles", 200, CVar.SERVERONLY);

    /// <summary>
    ///     Per-user cooldown in seconds between posting comments. Default 5.
    /// </summary>
    public static readonly CVarDef<int> NewsCommentCooldownSeconds =
        CVarDef.Create("stalkeren.news.comment_cooldown_seconds", 5, CVar.SERVERONLY);

    /// <summary>
    ///     Maximum number of comments allowed per article. Default 200.
    /// </summary>
    public static readonly CVarDef<int> NewsMaxCommentsPerArticle =
        CVarDef.Create("stalkeren.news.max_comments_per_article", 200, CVar.SERVERONLY);

    /// <summary>
    ///     Per-cartridge cooldown in milliseconds between reaction toggles. Default 500.
    /// </summary>
    public static readonly CVarDef<int> NewsReactionCooldownMs =
        CVarDef.Create("stalkeren.news.reaction_cooldown_ms", 500, CVar.SERVERONLY);

    /// <summary>
    ///     Maximum number of distinct reaction types allowed per article. Default 10.
    /// </summary>
    public static readonly CVarDef<int> NewsMaxDistinctReactions =
        CVarDef.Create("stalkeren.news.max_distinct_reactions", 10, CVar.SERVERONLY);
}
