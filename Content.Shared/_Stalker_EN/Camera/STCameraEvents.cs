using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Camera;

/// <summary>
/// DoAfter completion event for taking a photo.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class STCameraDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Server to Client: request viewport capture after DoAfter completes.
/// </summary>
[Serializable, NetSerializable]
public sealed class STCaptureViewportRequestEvent : EntityEventArgs
{
    public Guid Token;
    public NetEntity Camera;
}

/// <summary>
/// Client to Server: viewport screenshot data.
/// </summary>
[Serializable, NetSerializable]
public sealed class STCaptureViewportResponseEvent : EntityEventArgs
{
    public Guid Token;
    public byte[] ImageData = Array.Empty<byte>();
}

/// <summary>
/// Client to Server: request photo image data for viewing.
/// </summary>
[Serializable, NetSerializable]
public sealed class STPhotoRequestEvent : EntityEventArgs
{
    public NetEntity PhotoEntity;
    public Guid PhotoId;
}

/// <summary>
/// Server to Client: deliver photo image data.
/// </summary>
[Serializable, NetSerializable]
public sealed class STPhotoResponseEvent : EntityEventArgs
{
    public Guid PhotoId;
    public byte[] ImageData = Array.Empty<byte>();
}
