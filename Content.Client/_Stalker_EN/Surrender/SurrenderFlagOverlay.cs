using Content.Shared._Stalker_EN.Surrender;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Stalker_EN.Surrender;

public sealed class SurrenderFlagOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private const float SwapInterval = 0.4f;

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private SpriteSystem? _sprite;
    private TransformSystem? _transform;
    private ShaderInstance? _unshadedShader;
    private readonly SpriteSpecifier.Rsi _flagIconNormal = new(new ResPath("/Textures/_Stalker_EN/Icons/surrender.rsi"), "flag_above");
    private readonly SpriteSpecifier.Rsi _flagIconRed = new(new ResPath("/Textures/_Stalker_EN/Icons/surrender.rsi"), "flag_above_red");

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public SurrenderFlagOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 10;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _sprite ??= _entity.System<SpriteSystem>();
        _transform ??= _entity.System<TransformSystem>();
        _unshadedShader ??= _prototype.Index(UnshadedShader).Instance();

        // Alternate between normal and red every 0.4 seconds
        var flagIcon = ((int)(_timing.CurTime.TotalSeconds / SwapInterval) % 2 == 0) ? _flagIconNormal : _flagIconRed;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var xformQuery = _entity.GetEntityQuery<TransformComponent>();

        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(1, 1));

        var query = _entity.AllEntityQueryEnumerator<SurrenderedComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var _, out var xform, out var sprite))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform, xformQuery);
            var bounds = _sprite.GetLocalBounds(new Entity<SpriteComponent>(xform.Owner, sprite));

            var texture = _sprite.Frame0(flagIcon);
            var ppm = EyeManager.PixelsPerMeter;

            // All in meters
            var spriteTop = (bounds.Height + sprite.Offset.Y) / 2f;
            var texH = (float)texture.Height / ppm;
            var texW = (float)texture.Width / ppm;

            var yOffset = spriteTop + texH - 0.4f;
            var xOffset = -texW / 2f;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            handle.UseShader(_unshadedShader);
            handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
            handle.UseShader(null);

            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}
