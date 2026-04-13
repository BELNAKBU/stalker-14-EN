using Robust.Client.Graphics;

namespace Content.Client._Stalker_EN.Surrender;

/// <summary>
/// Registers the surrender flag overlay. Only drawn for entities with SurrenderedComponent.
/// </summary>
public sealed class ClientSurrenderSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new SurrenderFlagOverlay());
    }
}
