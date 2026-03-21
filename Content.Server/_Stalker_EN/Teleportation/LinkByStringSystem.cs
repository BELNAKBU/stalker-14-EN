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
        if (ent.Comp.FallbackId &&
            ent.Comp.LinkString == null &&
            MetaData(ent.Owner).EntityPrototype != null)
        {
            ent.Comp.LinkString = MetaData(ent.Owner).EntityPrototype?.ID;
        }
        TryLink(ent);
    }

    private void OnHandleState(Entity<LinkByStringComponent> ent, ref ComponentHandleState args)
    {
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
