using Content.Server.Popups;
using Content.Shared._Stalker_EN.Camera;
using Content.Shared.Charges.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.Camera;

/// <summary>
/// Handles camera use-in-hand, DoAfter, capture request/response, and photo spawning.
/// </summary>
public sealed class STCameraSystem : SharedSTCameraSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    /// <summary>
    /// Tracks pending viewport capture requests per player.
    /// </summary>
    private readonly Dictionary<NetUserId, PendingCapture> _pendingCaptures = new();

    private static readonly TimeSpan TokenExpiry = TimeSpan.FromSeconds(10);
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };

    /// <summary>
    /// Lazy-allocated list for expired token cleanup.
    /// </summary>
    private List<NetUserId>? _toRemove;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCameraComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<STCameraComponent, STCameraDoAfterEvent>(OnCameraDoAfterEvent);
        SubscribeNetworkEvent<STCaptureViewportResponseEvent>(OnViewportResponse);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingCaptures.Count == 0)
            return;

        var now = _timing.CurTime;
        _toRemove?.Clear();

        foreach (var (userId, pending) in _pendingCaptures)
        {
            if (now > pending.ExpiresAt)
                (_toRemove ??= new List<NetUserId>()).Add(userId);
        }

        if (_toRemove != null)
        {
            foreach (var userId in _toRemove)
            {
                _pendingCaptures.Remove(userId);
            }
        }
    }

    private void OnUseInHand(EntityUid uid, STCameraComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_timing.CurTime < comp.NextCaptureTime)
        {
            _popup.PopupEntity(Loc.GetString("st-camera-cooldown"), uid, args.User);
            return;
        }

        if (!_itemSlots.TryGetSlot(uid, STCameraComponent.FilmSlotId, out var filmSlot) || filmSlot.Item is not { } filmItem)
        {
            _popup.PopupEntity(Loc.GetString("st-camera-no-film"), uid, args.User);
            return;
        }

        if (_charges.IsEmpty(filmItem))
        {
            _popup.PopupEntity(Loc.GetString("st-camera-film-empty"), uid, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.CaptureDelay, new STCameraDoAfterEvent(), uid, used: uid)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnCameraDoAfterEvent(EntityUid uid, STCameraComponent comp, STCameraDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.User;

        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return;

        var token = Guid.NewGuid();
        _pendingCaptures[session.UserId] = new PendingCapture(
            token,
            uid,
            user,
            _timing.CurTime + TokenExpiry);

        RaiseNetworkEvent(new STCaptureViewportRequestEvent
        {
            Token = token,
            Camera = GetNetEntity(uid),
            Effect = comp.Effect,
        }, session);

        _audio.PlayPvs(comp.CaptureSound, uid);
        comp.NextCaptureTime = _timing.CurTime + comp.CaptureCooldown;
        Dirty(uid, comp);
    }

    private void OnViewportResponse(STCaptureViewportResponseEvent ev, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        if (!_pendingCaptures.TryGetValue(userId, out var pending))
            return;

        if (ev.Token != pending.Token)
            return;

        _pendingCaptures.Remove(userId);

        if (ev.ImageData.Length == 0)
            return;

        if (ev.ImageData.Length < JpegMagic.Length
            || ev.ImageData[0] != JpegMagic[0]
            || ev.ImageData[1] != JpegMagic[1]
            || ev.ImageData[2] != JpegMagic[2])
        {
            Log.Warning($"Player {args.SenderSession.Name} sent invalid photo data (bad JPEG header)");
            return;
        }

        var cameraUid = pending.Camera;

        if (!TryComp<STCameraComponent>(cameraUid, out var comp))
            return;

        if (ev.ImageData.Length > comp.MaxImageBytes)
        {
            Log.Warning($"Player {args.SenderSession.Name} sent oversized photo: {ev.ImageData.Length} bytes");
            return;
        }

        if (!Exists(pending.User))
            return;

        var photoUid = Spawn(comp.PhotoPrototype, _transform.GetMoverCoordinates(cameraUid));

        if (!TryComp<STPhotoComponent>(photoUid, out var photo))
        {
            Del(photoUid);
            return;
        }

        photo.ImageData = ev.ImageData;
        photo.PhotoId = Guid.NewGuid();
        Dirty(photoUid, photo);

        // Consume a film charge and auto-delete empty film
        if (_itemSlots.TryGetSlot(cameraUid, STCameraComponent.FilmSlotId, out var filmSlot)
            && filmSlot.Item is { } filmItem)
        {
            _charges.TryUseCharge(filmItem);

            if (_charges.IsEmpty(filmItem))
            {
                _itemSlots.TryEject(cameraUid, STCameraComponent.FilmSlotId, null, out _);
                Del(filmItem);
            }
        }

        // Try to give to player, fall back to dropping at feet
        _hands.PickupOrDrop(pending.User, photoUid);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        _pendingCaptures.Remove(args.Player.UserId);
    }

    private sealed record PendingCapture(Guid Token, EntityUid Camera, EntityUid User, TimeSpan ExpiresAt);
}
