using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.Camera;

/// <summary>
/// UI key for the photo viewer bound user interface.
/// </summary>
[Serializable, NetSerializable]
public enum STPhotoUiKey : byte
{
    Key,
}

/// <summary>
/// BUI state sent from server to client containing photo metadata.
/// Image data is requested separately via STPhotoRequestEvent.
/// </summary>
[Serializable, NetSerializable]
public sealed class STPhotoBoundUiState : BoundUserInterfaceState
{
    public readonly Guid PhotoId;
    public readonly string PhotographerName;
    public readonly TimeSpan TimeTaken;

    public STPhotoBoundUiState(Guid photoId, string photographerName, TimeSpan timeTaken)
    {
        PhotoId = photoId;
        PhotographerName = photographerName;
        TimeTaken = timeTaken;
    }
}
