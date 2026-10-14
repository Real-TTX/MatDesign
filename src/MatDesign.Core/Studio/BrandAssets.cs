using System.Text.Json.Serialization;

namespace MatDesign.Core.Studio;

/// <summary>
/// A file of the brand as the item refers to it: <see cref="File"/> is <c>&lt;sha256&gt;.&lt;ext&gt;</c> (the name of the file in <c>&lt;data&gt;/assets/</c>, which is its content address), the rest is what the
/// validator found when it was uploaded (size in bytes, width and height in px; for an SVG the size it declares or its view box). The item only ever refers to a file by that name, so a revision
/// can always be restored while the file is on the volume (IDENT-08).
/// </summary>
public sealed record AssetFile(string File, int Width = 0, int Height = 0, long Bytes = 0);

/// <summary>One face of a font family of the brand: <see cref="Weight"/> is 100 to 900 in steps of 100, <see cref="Style"/> is <c>normal</c> or <c>italic</c>, <see cref="File"/> is the stored WOFF2 or WOFF.</summary>
public sealed record FontFace(string Family, int Weight, string Style, string File, long Bytes = 0);

/// <summary>A file of the brand as a release and the site serve it: the slot it is for, its public name (under <c>brand/</c>, the same on the site and in a release) and the stored file.</summary>
public sealed record BrandFileRef(string Slot, string Name, string File);

/// <summary>
/// The assets of a brand (docs/PRODUCT.md, stage 13): the logo (with an optional variant for dark surfaces and an alt text, which is required), the word mark (the same), the favicon (SVG, PNG or ICO), the app icon
/// (PNG, 512 px) and the fonts (a list of faces). The Studio keeps exactly one such item, <see cref="ItemId"/> of the kind <see cref="Kind"/>, and never holds a file in it: the files are immutable
/// and named by their SHA-256 in the volume, the item only refers to them. A brand that has none of this is simply the built-in look; nothing is written for it.
/// </summary>
public sealed record BrandAssets(
    AssetFile? Logo = null,
    AssetFile? LogoDark = null,
    string LogoAlt = "",
    AssetFile? Wordmark = null,
    AssetFile? WordmarkDark = null,
    string WordmarkAlt = "",
    AssetFile? Favicon = null,
    AssetFile? AppIcon = null,
    IReadOnlyList<FontFace>? Fonts = null)
{
    /// <summary>The kind of the Studio item that holds the assets.</summary>
    public const string Kind = "assets";

    /// <summary>The id of the one item of that kind.</summary>
    public const string ItemId = "assets";

    /// <summary>The folder of the files in a release, in the archive and, below <c>/ui/</c>, on the site: <c>brand/</c>.</summary>
    public const string Folder = "brand";

    /// <summary>The slots that hold one file each, by the name the forms and the public file names use.</summary>
    public static readonly IReadOnlyList<string> Slots = ["logo", "logo-dark", "wordmark", "wordmark-dark", "favicon", "app-icon"];

    /// <summary>The faces, never null (a stored item that had none).</summary>
    [JsonIgnore]
    public IReadOnlyList<FontFace> Faces => Fonts ?? [];

    /// <summary>True when nothing is set: no file, no font.</summary>
    [JsonIgnore]
    public bool IsEmpty => Logo is null && LogoDark is null && Wordmark is null && WordmarkDark is null && Favicon is null && AppIcon is null && Faces.Count == 0;

    /// <summary>The file in a slot, by its name (<see cref="Slots"/>); null when the slot is empty.</summary>
    public AssetFile? In(string slot) => slot switch
    {
        "logo" => Logo,
        "logo-dark" => LogoDark,
        "wordmark" => Wordmark,
        "wordmark-dark" => WordmarkDark,
        "favicon" => Favicon,
        "app-icon" => AppIcon,
        _ => null,
    };

    /// <summary>The assets with one slot set (a file) or emptied (null).</summary>
    public BrandAssets With(string slot, AssetFile? file) => slot switch
    {
        "logo" => this with { Logo = file },
        "logo-dark" => this with { LogoDark = file },
        "wordmark" => this with { Wordmark = file },
        "wordmark-dark" => this with { WordmarkDark = file },
        "favicon" => this with { Favicon = file },
        "app-icon" => this with { AppIcon = file },
        _ => throw new ArgumentException($"'{slot}' is not a slot of the brand assets.", nameof(slot)),
    };

    /// <summary>The first ten characters of the hash in a stored file name: what the public name carries, so that it changes with the content (and may be cached for good).</summary>
    public static string ShortHash(string file) => file.Length >= 10 ? file[..10] : file;

    /// <summary>The extension of a stored file, with the dot (<c>.svg</c>).</summary>
    public static string Extension(string file) => file.Contains('.', StringComparison.Ordinal) ? file[file.LastIndexOf('.')..] : string.Empty;

    /// <summary>The public name of the file of a slot, without the folder: <c>logo-1a2b3c4d5e.svg</c>.</summary>
    public static string PublicName(string slot, AssetFile file) => $"{slot}-{ShortHash(file.File)}{Extension(file.File)}";

    /// <summary>The public name of a font face, below <c>fonts/</c>: <c>fonts/acme-sans-400-normal-1a2b3c4d5e.woff2</c>.</summary>
    public static string PublicName(FontFace face)
        => $"fonts/{BrandAssetValidator.Slug(face.Family)}-{face.Weight}-{face.Style}-{ShortHash(face.File)}{Extension(face.File)}";

    /// <summary>Every file the brand refers to, with the name it is served and released under (<c>brand/&lt;name&gt;</c>).</summary>
    public IEnumerable<BrandFileRef> References()
    {
        foreach (var slot in Slots)
        {
            if (In(slot) is { } file)
            {
                yield return new BrandFileRef(slot, PublicName(slot, file), file.File);
            }
        }

        foreach (var face in Faces)
        {
            yield return new BrandFileRef("font", PublicName(face), face.File);
        }
    }

    /// <summary>The content type a stored file is served with, from its extension; null for anything else.</summary>
    public static string? ContentTypeOf(string file) => Extension(file) switch
    {
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".ico" => "image/x-icon",
        ".woff2" => "font/woff2",
        ".woff" => "font/woff",
        _ => null,
    };
}
