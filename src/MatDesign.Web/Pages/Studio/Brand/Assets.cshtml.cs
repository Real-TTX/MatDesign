using System.Globalization;
using MatDesign.Core.Studio;
using MatDesign.Web.Pages.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

// A namespace of its own: the folder is called Brand, and a namespace of that name below MatDesign.Web.Pages.Studio would hide the type MatDesign.Core.Studio.Brand from the page of the brand.
namespace MatDesign.Web.Pages.StudioBrandAssets;

/// <summary>One check beside a file: what was looked at and whether it passes.</summary>
public sealed record SlotCheck(string Text, bool Ok);

/// <summary>What the page draws for one slot: the file that is there, the checks beside it, what is wrong with the last upload.</summary>
public sealed class SlotView
{
    public required string Slot { get; init; }

    public required string Title { get; init; }

    public required string Hint { get; init; }

    /// <summary>The <c>accept</c> of the upload and the size it names (<c>100KB</c>).</summary>
    public required string Accept { get; init; }

    public required string MaxSize { get; init; }

    /// <summary>True for the logo and the word mark: the alt text is asked for beside the file.</summary>
    public bool HasAlt { get; init; }

    public string Alt { get; init; } = string.Empty;

    public AssetFile? File { get; init; }

    public IReadOnlyList<SlotCheck> Checks { get; init; } = [];

    public string? Error { get; init; }

    /// <summary>The public name of the file (below <c>brand/</c>) and its address on the site.</summary>
    public string Name => File is null ? string.Empty : BrandAssets.PublicName(Slot, File);

    public string Url => File is null ? string.Empty : Ui.BrandUrl(Name);
}

/// <summary>
/// The assets of the brand (docs/PRODUCT.md, stage 13): a card for the logo, the word mark, the favicon, the app icon and the fonts. A file is uploaded through a normal form; the server looks at
/// its <b>content</b> (<see cref="BrandAssetValidator"/>: the magic bytes, an SVG by an allowlist), keeps it under its SHA-256 in the volume and saves a new revision of the item <c>assets</c>.
/// A file is never overwritten or deleted, so every revision can be restored. The size limit of this page's requests is its own (a megabyte), not the site's.
/// </summary>
[RequestSizeLimit(MaxRequestBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
public class AssetsModel(StudioService studio, DesignData design, ToolkitFiles toolkit) : StudioPageModel(studio)
{
    /// <summary>The most a request to this page may carry: the biggest file (a PNG of 500 KB) and a form around it, with room for nothing else.</summary>
    public const int MaxRequestBytes = 1024 * 1024;

    private readonly Dictionary<string, string> _errors = [];
    private readonly Dictionary<string, string> _typedAlt = [];

    /// <summary>What is wrong, by slot (<c>logo</c>, <c>favicon</c>, <c>fonts</c> ...), after a refused upload or a save that did not pass.</summary>
    public IReadOnlyDictionary<string, string> Errors => _errors;

    /// <summary>What a person typed in the form that was refused, to put it back.</summary>
    public string TypedFont { get; private set; } = string.Empty;

    public ItemSource Source => design.Assets is null ? ItemSource.BuiltIn : ItemSource.Changed;

    /// <summary>The assets as saved (never null): what the cards show.</summary>
    public BrandAssets Current => design.AssetsOrEmpty;

    /// <summary>The families the toolkit's own font stacks use: a face may not take one of these names.</summary>
    public IReadOnlyList<string> Reserved => toolkit.ReservedFamilies;

    public IReadOnlyList<FontFace> Faces => Current.Faces;

    /// <summary>The families of the brand (once each), in the order of the faces, with how a stack names them in the brand editor.</summary>
    public IEnumerable<(string Family, string Stack)> Families => Faces.Select(f => f.Family).Distinct(StringComparer.OrdinalIgnoreCase).Select(f => (f, $"\"{f}\", system-ui, sans-serif"));

    public bool HasHistory => Studio.Store.Current(BrandAssets.Kind, BrandAssets.ItemId) is not null;

    public string Budget => Kb(BrandAssetValidator.TotalBudget);

    public static string Kb(long bytes) => bytes < 1024 ? $"{bytes} bytes" : string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB");

    private static string SizeName(long bytes) => $"{bytes / 1024}KB";

    // ------------------------------------------------------------------------------------------------ what the cards show

    /// <summary>The view of one slot: the file, with the checks run on its bytes now.</summary>
    public SlotView View(string slot)
    {
        var (title, hint, accept, kind) = slot switch
        {
            "logo" => ("Logo", "The logo shown at the top of the menu in place of the tile with the glyph. SVG or PNG, wide is allowed (up to 8 to 1).", ".svg,.png,image/svg+xml,image/png", AssetSlot.Logo),
            "logo-dark" => ("Logo for dark surfaces", "Optional. Shown instead of the logo when the theme is dark. Without it the logo is used in both themes.", ".svg,.png,image/svg+xml,image/png", AssetSlot.Logo),
            "wordmark" => ("Word mark", "Optional. The name of the company as a picture, shown in the menu header in place of the name as text. SVG or PNG.", ".svg,.png,image/svg+xml,image/png", AssetSlot.Wordmark),
            "wordmark-dark" => ("Word mark for dark surfaces", "Optional. Shown instead of the word mark when the theme is dark.", ".svg,.png,image/svg+xml,image/png", AssetSlot.Wordmark),
            "favicon" => ("Favicon", "The icon in the browser tab. SVG, or a square PNG of 16 to 512 px, or an ICO.", ".svg,.png,.ico,image/svg+xml,image/png,image/x-icon", AssetSlot.Favicon),
            _ => ("App icon", "The icon for the home screen of a phone (apple-touch-icon). A PNG of exactly 512 by 512 px.", ".png,image/png", AssetSlot.AppIcon),
        };
        var max = BrandAssetValidator.AllowedTypes(kind).Max(t => BrandAssetValidator.MaxBytes(t));
        var file = Current.In(slot);
        var alt = slot switch { "logo" => Current.LogoAlt, "wordmark" => Current.WordmarkAlt, _ => string.Empty };
        return new SlotView
        {
            Slot = slot,
            Title = title,
            Hint = hint,
            Accept = accept,
            MaxSize = SizeName(max),
            HasAlt = slot is "logo" or "wordmark",
            Alt = _typedAlt.GetValueOrDefault(slot, alt),
            File = file,
            Checks = file is null ? [] : ChecksOf(file, kind, slot),
            Error = _errors.GetValueOrDefault(slot),
        };
    }

    private List<SlotCheck> ChecksOf(AssetFile file, AssetSlot kind, string slot)
    {
        var checks = new List<SlotCheck>();
        var bytes = toolkit.Assets.Store.Read(file.File);
        if (bytes is null)
        {
            checks.Add(new SlotCheck("The file is on the volume", false));
            return checks;
        }

        checks.Add(new SlotCheck("The file is on the volume", true));
        checks.Add(new SlotCheck("The content still has the hash in the name of the file", BrandAssetValidator.Sha256(bytes) == file.File[..64]));
        var inspection = BrandAssetValidator.Inspect(bytes, kind);
        if (inspection.Ok)
        {
            checks.Add(new SlotCheck($"The type, by its content: {Describe(inspection.Ext)}", true));
            checks.Add(new SlotCheck($"The size: {Kb(bytes.Length)} of {Kb(BrandAssetValidator.MaxBytes(inspection.Ext))}", true));
            if (inspection.Width > 0)
            {
                checks.Add(new SlotCheck($"The picture: {inspection.Width} x {inspection.Height} px", true));
            }
        }
        else
        {
            checks.AddRange(inspection.Problems.Select(p => new SlotCheck(p, false)));
        }

        if (slot is "logo" or "wordmark")
        {
            var alt = slot == "logo" ? Current.LogoAlt : Current.WordmarkAlt;
            checks.Add(new SlotCheck(BrandAssetValidator.CheckAlt(alt) is { } problem ? problem : "The alt text is set", BrandAssetValidator.CheckAlt(alt) is null));
        }

        return checks;
    }

    private static string Describe(string ext) => ext switch { ".svg" => "SVG", ".png" => "PNG", ".ico" => "ICO", ".woff2" => "WOFF2", _ => "WOFF" };

    /// <summary>The check of one font face: what the bytes say now.</summary>
    public IReadOnlyList<SlotCheck> FaceChecks(FontFace face)
    {
        var bytes = toolkit.Assets.Store.Read(face.File);
        if (bytes is null)
        {
            return [new SlotCheck("The file is missing from the volume", false)];
        }

        if (BrandAssetValidator.Sha256(bytes) != face.File[..64])
        {
            return [new SlotCheck("The content does not have the hash in the name of the file", false)];
        }

        var inspection = BrandAssetValidator.Inspect(bytes, AssetSlot.Font);
        return inspection.Ok
            ? [new SlotCheck($"{Describe(inspection.Ext)}, {Kb(bytes.Length)}", true)]
            : inspection.Problems.Select(p => new SlotCheck(p, false)).ToList();
    }

    public string FaceUrl(FontFace face) => Ui.BrandUrl(BrandAssets.PublicName(face));

    // ------------------------------------------------------------------------------------------------ handlers

    public IActionResult OnGet() => Page();

    /// <summary>Sets the file of a slot, and the alt text of a logo or a word mark (a form with an alt text and no file changes only the text).</summary>
    public async Task<IActionResult> OnPostSaveAsync(string slot)
    {
        if (!BrandAssets.Slots.Contains(slot))
        {
            return NotFound();
        }

        var assets = design.AssetsOrEmpty;
        var kind = BrandAssetValidator.SlotOf(slot)!.Value;
        var note = new List<string>();
        var alt = Request.Form["alt"].ToString();
        if (slot is "logo" or "wordmark")
        {
            _typedAlt[slot] = alt;
        }

        if (Request.Form.Files.GetFile("file") is { Length: > 0 } upload)
        {
            using var memory = new MemoryStream();
            await upload.CopyToAsync(memory);
            var bytes = memory.ToArray();
            var inspection = BrandAssetValidator.Inspect(bytes, kind);
            if (!inspection.Ok)
            {
                _errors[slot] = string.Join(" ", inspection.Problems);
                return Page();
            }

            var stored = toolkit.Assets.Store.Put(bytes, inspection.Ext);
            assets = assets.With(slot, new AssetFile(stored, inspection.Width, inspection.Height, bytes.Length));
            note.Add($"{SlotTitle(slot)} set");
        }
        else if (slot is not ("logo" or "wordmark"))
        {
            _errors[slot] = "Choose a file first.";
            return Page();
        }

        if (slot is "logo" or "wordmark")
        {
            if (assets.In(slot) is null)
            {
                _errors[slot] = "Choose the file first: the alt text belongs to it.";
                return Page();
            }

            var before = slot == "logo" ? assets.LogoAlt : assets.WordmarkAlt;
            assets = slot == "logo" ? assets with { LogoAlt = alt } : assets with { WordmarkAlt = alt };
            if (before != alt.Trim() && note.Count == 0)
            {
                note.Add($"{SlotTitle(slot)}: alt text changed");
            }
        }

        return Save(slot, assets, note.Count == 0 ? "Nothing changed" : string.Join(", ", note), $"{SlotTitle(slot)} was saved.");
    }

    /// <summary>Empties a slot. The file stays on the volume (the history refers to it).</summary>
    public IActionResult OnPostRemove(string slot)
    {
        if (!BrandAssets.Slots.Contains(slot) || design.AssetsOrEmpty.In(slot) is null)
        {
            return RedirectToPage("/Studio/Brand/Assets");
        }

        return Save(slot, design.AssetsOrEmpty.With(slot, null), $"{SlotTitle(slot)} removed", $"{SlotTitle(slot)} was removed.");
    }

    /// <summary>Adds one face of a font family: family, weight, style and a WOFF2 or WOFF file.</summary>
    public async Task<IActionResult> OnPostAddFontAsync()
    {
        var family = Request.Form["family"].ToString().Trim();
        _ = int.TryParse(Request.Form["weight"], NumberStyles.None, CultureInfo.InvariantCulture, out var weight);
        var style = Request.Form["style"].ToString().Trim().ToLowerInvariant();
        TypedFont = family;
        if (Request.Form.Files.GetFile("file") is not { Length: > 0 } upload)
        {
            _errors["fonts"] = "Choose the font file first (WOFF2 or WOFF).";
            return Page();
        }

        using var memory = new MemoryStream();
        await upload.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var inspection = BrandAssetValidator.Inspect(bytes, AssetSlot.Font);
        if (!inspection.Ok)
        {
            _errors["fonts"] = string.Join(" ", inspection.Problems);
            return Page();
        }

        var stored = toolkit.Assets.Store.Put(bytes, inspection.Ext);
        var assets = BrandAssetValidator.Normalize(design.AssetsOrEmpty with { Fonts = [.. design.AssetsOrEmpty.Faces, new FontFace(family, weight, style, stored, bytes.Length)] });
        return Save("fonts", assets, $"Font {family} {weight} {style} added", $"The font face {family} {weight} {style} was added.");
    }

    /// <summary>Removes one face (the file stays on the volume).</summary>
    public IActionResult OnPostRemoveFont(string family, int weight, string style)
    {
        var assets = design.AssetsOrEmpty;
        var face = assets.Faces.FirstOrDefault(f => f.Family == family && f.Weight == weight && f.Style == style);
        if (face is null)
        {
            return RedirectToPage("/Studio/Brand/Assets");
        }

        return Save("fonts", assets with { Fonts = assets.Faces.Where(f => f != face).ToList() }, $"Font {family} {weight} {style} removed", $"The font face {family} {weight} {style} was removed.");
    }

    /// <summary>Takes every asset away: the item is deleted and the brand is the built-in look again. Files and history stay.</summary>
    public IActionResult OnPostReset()
    {
        if (design.Assets is null)
        {
            return RedirectToPage("/Studio/Brand/Assets");
        }

        Studio.Store.Delete(BrandAssets.Kind, BrandAssets.ItemId, Studio.Administrator, "Removed every asset");
        design.Reload();
        TempData["Flash"] = "The assets were removed. The built-in look is back.";
        return RedirectToPage("/Studio/Brand/Assets");
    }

    private static string SlotTitle(string slot) => slot switch
    {
        "logo" => "Logo",
        "logo-dark" => "Dark logo",
        "wordmark" => "Word mark",
        "wordmark-dark" => "Dark word mark",
        "favicon" => "Favicon",
        _ => "App icon",
    };

    private static readonly System.Text.Json.JsonSerializerOptions Compare = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>True when two items say the same (saving it again would only add a revision that changes nothing).</summary>
    private static bool Same(BrandAssets a, BrandAssets b)
        => System.Text.Json.JsonSerializer.Serialize(BrandAssetValidator.Normalize(a), Compare) == System.Text.Json.JsonSerializer.Serialize(BrandAssetValidator.Normalize(b), Compare);

    /// <summary>Validates the item as it would be, and saves it as a new revision; what is wrong stays on the page.</summary>
    private IActionResult Save(string where, BrandAssets assets, string note, string flash)
    {
        assets = BrandAssetValidator.Normalize(assets);
        var problems = BrandAssetValidator.Validate(assets, toolkit.ReservedFamilies);
        if (problems.Count > 0)
        {
            _errors[where] = string.Join(" ", problems.Select(p => p.Message));
            return Page();
        }

        if (design.Assets is not null && Same(design.Assets, assets))
        {
            TempData["Flash"] = "Nothing changed.";
            return RedirectToPage("/Studio/Brand/Assets");
        }

        Studio.Store.Save(BrandAssets.Kind, BrandAssets.ItemId, assets, Studio.Administrator, design.Assets is null ? "Created: " + note : note);
        design.Reload();
        TempData["Flash"] = flash;
        return RedirectToPage("/Studio/Brand/Assets");
    }
}
