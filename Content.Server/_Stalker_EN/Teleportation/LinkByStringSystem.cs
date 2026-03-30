using Content.Server.SubFloor;
using Content.Shared.Random.Rules;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using NetCord;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;

namespace Content.Server._Stalker_EN.Teleportation;

/// <summary>
/// This is used for Linking portals via a common string
/// </summary>
public sealed class LinkByStringSystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LinkByStringComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LinkByStringComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnStartup(Entity<LinkByStringComponent> ent, ref ComponentStartup args)
    {
        // Don't modify the component data - just use the fallback ID for linking
        if (ent.Comp.FallbackId && ent.Comp.LinkString == null)
        {
            var prototypeId = MetaData(ent.Owner).EntityPrototype?.ID;
            if (prototypeId != null)
            {
                TryLinkWithFallback(ent, prototypeId);
                return;
            }
        }
        TryLink(ent);
    }

    private void TryLinkWithFallback(Entity<LinkByStringComponent> ent, string fallbackString)
    {
        var query = EntityQueryEnumerator<LinkByStringComponent>();

        while (query.MoveNext(out var uid, out var link))
        {
            if (ent.Owner == uid)
                continue;

            var otherString = link.LinkString;
            if (otherString == null && link.FallbackId)
                otherString = MetaData(uid).EntityPrototype?.ID;

            if (fallbackString != otherString)
                continue;

            _link.TryLink(ent.Owner, uid);
        }
    }

    private void OnHandleState(Entity<LinkByStringComponent> ent, ref ComponentHandleState args)
    {
        if (ent.Comp.FallbackId && ent.Comp.LinkString == null)
        {
            var prototypeId = MetaData(ent.Owner).EntityPrototype?.ID;
            if (prototypeId != null)
            {
                TryLinkWithFallback(ent, prototypeId);
                return;
            }
        }
        TryLink(ent);
    }

    private void TryLink(Entity<LinkByStringComponent> ent)
    {
        var query = EntityQueryEnumerator<LinkByStringComponent>();

        while (query.MoveNext(out var uid, out var link))
        {
            if (ent.Comp.LinkString != link.LinkString || ent.Owner == uid)
                continue;
            _link.TryLink(ent.Owner, uid);
        }
    }
}
