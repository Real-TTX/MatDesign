using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Download;

/// <summary>The toolkit as files: what to copy, how to use it, and the data endpoints.</summary>
public class IndexModel : PageModel
{
    private readonly ToolkitFiles _toolkit;
    private readonly IconLibrary _iconLibrary;
    private readonly MatDesign.Web.Studio.DesignData _design;

    public IndexModel(ToolkitFiles toolkit, IconLibrary iconLibrary, MatDesign.Web.Studio.DesignData design)
    {
        _toolkit = toolkit;
        _iconLibrary = iconLibrary;
        _design = design;
    }

    /// <summary>What the site calls itself: the name of the program that built the files.</summary>
    public string SiteName => _design.Tenant.SiteName;

    /// <summary>The version of the toolkit (semantic versioning): what an app compares with its copy.</summary>
    public string Version => AppVersion.MatUi;

    /// <summary>The build of the site that made the files.</summary>
    public string Build => AppVersion.Current;

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
        ("/api/rules/changes", "What changed in the rulebook, the newest first: date, rule, kind (Added, Changed, Removed, Moved) and note; ?rule=FORM-15, ?kind=Changed, ?area=FORM or ?since=2026-10-15 narrow it down."),
        ("/api/tokens", "Every design token with its light and dark value."),
        ("/api/properties", "The documented component properties (--mat-btn-radius ...): component, name, type, the default it falls back to and what it changes."),
        ("/api/icons", "The names of the icons in the sprite."),
        ("/api/iconlib", "The icon library: its categories, and every icon with its categories and tags."),
        ("/api/version", "The version of the toolkit (matui), the build of the site and the hash of the files."),
        ("/ui/manifest.json", "The version, the build and every file of the toolkit with its SHA-256: what a program compares with an app's copy."),
        ("/ui/CHANGELOG.md", "What changed in each version, as the file."),
        ("/api/changelog", "What changed, as data: all versions, or those after ?since=0.1.0, with their sections and migration steps."),
    ];

    /// <summary>The address as another app would write it down: with this site's host.</summary>
    public string Absolute(string url) => $"{Request.Scheme}://{Request.Host}{url}";
}
