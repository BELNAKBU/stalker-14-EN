using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker_EN.Portraits;

/// <summary>
/// Defines a set of character portraits for a specific role.
/// Multiple textures per portrait allow random variation for NPCs
/// and player choice in the loadout screen.
/// </summary>
[Prototype("characterPortrait")]
public sealed partial class CharacterPortraitPrototype : IPrototype
{
    /// <summary>
    /// Prefix for portrait texture paths. Added to relative paths in YAML to form full paths.
    /// </summary>
    public const string PortraitTexturePrefix = "/Textures/_Stalker_EN/Portraits/";

    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name shown in the portrait selector UI.
    /// </summary>
    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description for tooltip in the portrait selector.
    /// </summary>
    [DataField]
    public string? Description { get; private set; }

    /// <summary>
    /// The faction (band) this portrait belongs to.
    /// Leave empty to make the portrait admin-only (hidden from players).
    /// </summary>
    [DataField]
    public string BandId { get; private set; } = string.Empty;

    /// <summary>
    /// Optional role restriction. If set, only characters with this job can use the portrait.
    /// If null/empty, any member of the band can use it.
    /// </summary>
    [DataField]
    public string? JobId { get; private set; }

    /// <summary>
    /// List of available portrait textures for this role.
    /// NPCs pick one randomly. Players choose one in the loadout screen.
    /// Paths can be either relative (without prefix) or absolute.
    /// </summary>
    [DataField(required: true)]
    public List<ResPath> Textures { get; private set; } = new();

    /// <summary>
    /// Gets the full path for a texture, ensuring the prefix is present.
    /// If the path is already absolute (starts with /), returns it as-is.
    /// </summary>
    public static ResPath GetFullPath(ResPath path)
    {
        if (path == ResPath.Empty)
            return path;

        var pathString = path.ToString();
        if (pathString.StartsWith("/"))
            return path;

        return new ResPath(PortraitTexturePrefix + pathString);
    }

    /// <summary>
    /// Converts an absolute path to relative (removes the prefix).
    /// If the path doesn't start with the prefix, returns it as-is.
    /// </summary>
    public ResPath GetRelativePath(ResPath path)
    {
        if (path == ResPath.Empty)
            return path;

        var pathString = path.ToString();
        if (pathString.StartsWith(PortraitTexturePrefix))
            return new ResPath(pathString[PortraitTexturePrefix.Length..]);

        return path;
    }

    /// <summary>
    /// Validates if a portrait texture path exists in any of the given portrait prototypes.
    /// Supports both relative and absolute paths.
    /// </summary>
    /// <param name="texturePath">The texture path to validate.</param>
    /// <param name="portraits">List of portrait prototypes to search in.</param>
    /// <returns>True if the texture path exists in any prototype, false otherwise.</returns>
    public static bool ValidatePortraitPath(string texturePath, IEnumerable<CharacterPortraitPrototype> portraits)
    {
        if (string.IsNullOrEmpty(texturePath))
            return false;

        var portraitPath = new ResPath(texturePath);
        return portraits.Any(p => p.Textures.Contains(portraitPath) || p.Textures.Any(t => CharacterPortraitPrototype.GetFullPath(t).ToString() == texturePath));
    }
}
