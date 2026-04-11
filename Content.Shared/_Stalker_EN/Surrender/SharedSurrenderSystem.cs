using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker_EN.Surrender;

public sealed class SharedSurrenderSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly TimeSpan SurrenderDuration = TimeSpan.FromSeconds(6);
    private static readonly string EmoteId = "SurrenderEmote";

    private readonly Dictionary<EntityUid, (TimeSpan RemoveTime, bool WasPacified)> _surrenderRemovals = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MetaDataComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<MetaDataComponent> ent, ref EmoteEvent args)
    {
        if (args.Emote.ID != EmoteId)
            return;

        if (HasComp<SurrenderedComponent>(ent))
            return;

        EnsureComp<SurrenderedComponent>(ent);

        var wasPacified = HasComp<PacifiedComponent>(ent);
        if (!wasPacified)
            EnsureComp<PacifiedComponent>(ent);

        _surrenderRemovals[ent.Owner] = (_timing.CurTime + SurrenderDuration, wasPacified);

        // Show floating text above entity with name
        var message = Loc.GetString("surrender-chat-message", ("entity", Identity.Entity(ent.Owner, EntityManager)));
        _popup.PopupEntity(message, ent, ent);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();

        foreach (var (uid, data) in _surrenderRemovals)
        {
            var (removeTime, wasPacified) = data;
            if (curTime < removeTime)
                continue;

            if (!TerminatingOrDeleted(uid))
            {
                RemComp<SurrenderedComponent>(uid);
                if (!wasPacified)
                    RemComp<PacifiedComponent>(uid);
            }

            toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            _surrenderRemovals.Remove(uid);
    }

    public bool IsSurrendering(EntityUid uid)
    {
        return HasComp<SurrenderedComponent>(uid);
    }
}
