using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Download;

/// <summary>The toolkit as files: what to copy, how to use it, and the data endpoints.</summary>
public class IndexModel : PageModel
{
    private readonly ToolkitFiles _toolkit;
    private readonly IconLibrary _iconLibrary;

    public IndexModel(ToolkitFiles toolkit, IconLibrary iconLibrary)
    {
        _toolkit = toolkit;
        _iconLibrary = iconLibrary;
    }

    public string Version => AppVersion.Current;

    public string Hash => _toolkit.Hash;

    public string ZipUrl => "/ui/matui.zip";

    public int ZipBytes => _toolkit.Zip.Length;

    /// <summary>The four files an app needs.</summary>
    public IReadOnlyList<ToolkitFile> Main => _toolkit.Files().Take(4).ToList();

    /// <summary>The CSS parts of the bundle, for apps that load them one by one.</summary>
    public IReadOnlyList<ToolkitFile> Parts => _toolkit.Files().Skip(4).ToList();

    /// <summary>The optional icon library: its own files, not part of the four.</summary>
    public IReadOnlyList<ToolkitFile> IconLibraryFiles => _iconLibrary.Files();

    public int IconLibraryIcons => _iconLibrary.Icons.Count;

    public int IconLibraryCategories => _iconLibrary.Categories.Count;

    public IReadOnlyList<(string Url, string What)> Endpoints { get; } =
    [
        ("/api/apps", "The apps: id, name, category, accent, text colour on it, glyph, port."),
        ("/api/accents", "The accents a user can choose in the settings."),
        ("/api/schemes", "The colour schemes: id, name, and the tokens each sets in light and in dark."),
        ("/api/rules", "The whole rulebook: id, area, title, statement, level, source."),
        ("/api/tokens", "Every design token with its light and dark value."),
        ("/api/icons", "The names of the icons in the sprite."),
        ("/api/iconlib", "The icon library: its categories, and every icon with its categories and tags."),
        ("/api/version", "The version and the hash of the toolkit."),
    ];

    /// <summary>The address as another app would write it down: with this site's host.</summary>
    public string Absolute(string url) => $"{Request.Scheme}://{Request.Host}{url}";
}
