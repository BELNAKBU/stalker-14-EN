using Content.Shared.Charges.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;

namespace Content.Shared._Stalker_EN.Camera;

/// <summary>
/// Shared base for <see cref="STCameraComponent"/> logic.
/// Handles examine so the film charge count appears immediately on both client and server.
/// </summary>
public abstract class SharedSTCameraSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<STCameraComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, STCameraComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_itemSlots.TryGetSlot(uid, STCameraComponent.FilmSlotId, out var filmSlot)
            || filmSlot.Item is not { } filmItem)
        {
            args.PushMarkup(Loc.GetString("st-camera-examine-no-film"));
            return;
        }

        var charges = _charges.GetCurrentCharges(filmItem);
        if (charges < 0)
            return;

        args.PushMarkup(Loc.GetString("st-camera-examine-film", ("charges", charges)));
    }
}
