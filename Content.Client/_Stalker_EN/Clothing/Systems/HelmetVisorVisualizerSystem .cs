using Content.Shared._Stalker_EN.Clothing;
using Content.Shared._Stalker_EN.Clothing.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client._Stalker_EN.Clothing;

public sealed class HelmetVisorVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _itemSys = default!;
    [Dependency] private readonly SpriteSystem _spriteSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HelmetVisorComponent, HelmetVisorVisualsChangedEvent>(OnVisualsChanged);
    }

    private void OnVisualsChanged(EntityUid uid, HelmetVisorComponent comp, HelmetVisorVisualsChangedEvent args)
    {
        if (comp.IconStateUp == null || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var state = comp.IsUp ? comp.IconStateUp : "icon";
        _spriteSys.LayerSetRsiState((uid, sprite), 0, state);
        _itemSys.VisualsChanged(uid);
    }
}
