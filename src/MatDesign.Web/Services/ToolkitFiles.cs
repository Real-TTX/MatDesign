using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.Extensions.FileProviders;

namespace MatDesign.Web.Services;

/// <summary>A file of the toolkit as the download page lists it.</summary>
public sealed record ToolkitFile(string Name, string Url, string Description, int Bytes);

/// <summary>A file in the manifest of the toolkit: where it is served and the SHA-256 of its content, so that an app can tell whether its copy is this one.</summary>
public sealed record ToolkitManifestFile(string Name, string Url, long Bytes, string Sha256);

/// <summary>
/// What a version of the toolkit is, as a program can read it (<c>/ui/manifest.json</c>, and <c>matui.manifest.json</c> in the archive an app copies): the version
/// (semantic versioning, the one number to compare), the build of the site that made the files, the hash that is in the first line of <c>matui.css</c>, where the
/// changelog is and every file with its hash. <c>tools/matui-check.js</c> reads it.
/// </summary>
public sealed record ToolkitManifest(string Name, string Version, string Build, string Hash, string Changelog, IReadOnlyList<ToolkitManifestFile> Files);

/// <summary>
/// The files of MatUi (CSS parts, script, sprite), read once from the toolkit project's static assets. The site serves them as
/// one bundle (<c>/ui/matui.css</c>) so that any app can use the toolkit with one line, and reads the tokens from the CSS for the docs.
/// The one part that can change while the site runs is the colours of the apps: what was added or changed in the Studio is written after the built-in ones,
/// so the bundle, its hash and the archive follow every change (<see cref="DesignData.Version"/>).
/// </summary>
public sealed partial class ToolkitFiles
{
    /// <summary>The CSS parts in the order they are bundled.</summary>
    public static readonly IReadOnlyList<string> CssParts =
    [
        "matui.tokens.css", "matui.base.css", "matui.shell.css", "matui.controls.css", "matui.forms.css", "matui.pickers.css", "matui.lists.css", "matui.dialogs.css", "matui.layout.css", "matui.apps.css",
    ];

    private const string AppsPart = "matui.apps.css";

    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _scriptTemplate;
    private readonly string _bootTemplate;
    private readonly IFileProvider _files;
    private readonly DesignData _design;
    private readonly Dictionary<string, string> _css = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private State _state;

    public ToolkitFiles(IWebHostEnvironment env, DesignData design)
    {
        _files = env.WebRootFileProvider;
        _design = design;
        foreach (var part in CssParts)
        {
            _css[part] = Read($"css/{part}");
        }

        _scriptTemplate = Read("js/matui.js");
        _bootTemplate = Read("js/matui.boot.js");
        Script = _scriptTemplate;
        BootScript = _bootTemplate;
        Sprite = Read("img/matui-icons.svg");
        IconNames = SymbolRegex().Matches(Sprite).Select(m => m.Groups[1].Value).Order().ToList();
        Tokens = CssTokens.Tokens(_css["matui.tokens.css"]);
        Schemes = CssTokens.Schemes(_css["matui.tokens.css"]);
        Properties = ComponentProperties.Parse(CssParts.Select(part => _css[part]));
        EditableTokens = BrandTokens.Editable(Tokens);
        EditableProperties = ComponentTokens.Editable(Properties, Tokens);
        Facts = new MarkupFacts(MarkupChecks.ClassesIn(CssParts.Select(part => _css[part])), IconNames.ToHashSet());
        _state = Build(design.Version);
    }

    /// <summary>What depends on the apps of the Studio: the colours of the apps, and everything made from them.</summary>
    private sealed record State(long Version, string AppsCss, string Hash, string CssBundle, Lazy<byte[]> Zip);

    private State Current
    {
        get
        {
            var state = _state;
            if (state.Version == _design.Version)
            {
                return state;
            }

            lock (_gate)
            {
                if (_state.Version != _design.Version)
                {
                    _state = Build(_design.Version);
                }

                return _state;
            }
        }
    }

    private State Build(long version)
    {
        // the order of the cascade: the toolkit's defaults, the site's own look, the colours of the apps, the brand (tokens, component properties, colour schemes), then what each app sets for itself
        var appsCss = _css[AppsPart] + SiteOverlay(_design) + AppsOverlay(_design.StudioApps) + BrandOverlay(_design.StudioBrand, EditableTokens, EditableProperties, Schemes) + AppOverridesOverlay(_design.StudioApps, EditableTokens, EditableProperties);
        string Part(string name) => name == AppsPart ? appsCss : _css[name];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(CssParts.Select(Part)) + Script + BootScript + Sprite)))[..10].ToLowerInvariant();
        // the first line names the version of the toolkit first (what an app compares), then the build of the site that made the file
        var bundle = $"/* MatUi {AppVersion.MatUi} ({hash}) - build {AppVersion.Current} - bundled from {string.Join(", ", CssParts)} */\n" + string.Join("\n", CssParts.Select(Part));
        return new State(version, appsCss, hash, bundle, new Lazy<byte[]>(() => BuildZip(bundle, Script, BootScript, appsCss, hash, AppVersion.MatUi)));
    }

    /// <summary>
    /// The colour of the site itself when it is not an app of the family (stage 14, the blank template): <c>[data-app="this-site"]</c> with the accent of the tenant. The demo writes nothing: the site
    /// is the <c>matdesign</c> app there, whose colour <c>matui.apps.css</c> has. The text colours are the ones the toolkit derives (the accent is checked for AA like any app's, when the tenant is saved).
    /// </summary>
    public static string SiteOverlay(DesignData design)
    {
        if (design.IsDemo)
        {
            return string.Empty;
        }

        var site = design.SiteApp;
        return $"\n/* The site itself (template {design.Template.Id}). */\n[data-app=\"{site.Id}\"] {{ --mat-app-accent: {site.Accent}; }}\n";
    }

    /// <summary>The colours of the apps that were added or changed in the Studio, written like the built-in ones and placed after them, so they win.</summary>
    public static string AppsOverlay(IEnumerable<MatApp> apps)
    {
        var sb = new StringBuilder();
        foreach (var app in apps.Where(AppValidator.IsSafe))
        {
            sb.Append("[data-app=\"").Append(app.Id).Append("\"] { --mat-app-accent: ").Append(app.Accent).Append(';');
            if (app.OnAccent is not null)
            {
                sb.Append(" --mat-app-on-accent: ").Append(app.OnAccent).Append(';');
            }

            if (app.AccentText is not null)
            {
                sb.Append(" --mat-app-accent-text: ").Append(app.AccentText).Append(';');
            }

            sb.Append(" }\n");
        }

        return sb.Length == 0 ? string.Empty : "\n/* Apps added or changed in the Studio. */\n" + sb;
    }

    /// <summary>
    /// The tokens a brand sets (<c>COLOR-11</c>), written after everything else so that they win: the light values on <c>:root</c>, and the dark values twice and identical, once for an explicit
    /// <c>data-theme="dark"</c> and once for the system under <c>prefers-color-scheme: dark</c>, exactly as <c>matui.tokens.css</c> writes its dark set. Only a token a brand may set, with a value of its
    /// kind within its limits, is written; a brand that sets nothing writes nothing, so the file is what it was without a brand.
    /// </summary>
    public static string BrandOverlay(Brand? brand, IReadOnlyList<BrandToken> tokens, IReadOnlyList<PropertyToken>? properties = null, IReadOnlyList<SchemeBlocks>? schemes = null)
    {
        if (brand is null || !BrandValidator.IsSafe(brand))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        // the tokens: light on :root, dark twice
        var light = TokenLines(tokens, brand.Values(false), dark: false);
        var dark = TokenLines(tokens, brand.Values(true), dark: true);
        if (light.Count > 0 || dark.Count > 0)
        {
            sb.Append("\n/* Brand set in the Studio (COLOR-11): tokens only. The dark set is written twice, like in matui.tokens.css. */\n");
            sb.Append(TokenBlocks(":root", ":root[data-theme=\"dark\"]", ":root:not([data-theme=\"light\"]):not([data-theme=\"dark\"])", light, dark));
        }

        // the component properties: one value for light and dark; a height under the touch target is written for a fine pointer only (MOBILE-04)
        var props = PropertyBlocks(":root", properties ?? [], brand.ComponentValues);
        if (props.Length > 0)
        {
            sb.Append("\n/* Component properties of the brand (SPACE-06): a height under 44 px is only for a fine pointer, so a touch screen keeps the 44 px of the control height (MOBILE-04). */\n").Append(props);
        }

        // inside a colour scheme, after the scheme's own blocks of matui.tokens.css and with the same selectors, so they win; oled has no light values
        var inSchemes = new StringBuilder();
        foreach (var scheme in ColorSchemes.All)
        {
            if (brand.SchemeFor(scheme.Id) is not { } values)
            {
                continue;
            }

            var own = (schemes ?? []).FirstOrDefault(s => s.Id == scheme.Id);
            var schemeLight = own?.Light is null ? new List<string>() : TokenLines(tokens.Where(t => own.Light.ContainsKey(t.Name)).ToList(), values.Values(false), dark: false, schemeDark: false);
            var schemeDark = own?.Dark is null ? new List<string>() : TokenLines(tokens.Where(t => own.Dark.ContainsKey(t.Name)).ToList(), values.Values(true), dark: true, schemeDark: true);
            if (schemeLight.Count > 0 || schemeDark.Count > 0)
            {
                var root = $":root[data-scheme=\"{scheme.Id}\"]";
                inSchemes.Append(TokenBlocks(root, root + "[data-theme=\"dark\"]", root + ":not([data-theme=\"light\"]):not([data-theme=\"dark\"])", schemeLight, schemeDark));
            }
        }

        if (inSchemes.Length > 0)
        {
            sb.Append("\n/* Tokens of the brand inside a colour scheme (COLOR-09, COLOR-11): after the blocks of the scheme, with their selectors. The dark set is written twice. */\n").Append(inSchemes);
        }

        return sb.ToString();
    }

    /// <summary>
    /// What each app sets for itself on top of the brand (stage 12 of docs/PRODUCT.md): its tokens (shape, spacing, type, motion) on <c>[data-app="id"]</c> with the dark set twice and identical, and its
    /// component properties on <c>[data-app="id"]</c>, after the brand, so the order is the toolkit, the brand, the app. An app that sets nothing writes nothing: with no override at all the result is empty.
    /// </summary>
    public static string AppOverridesOverlay(IEnumerable<MatApp> apps, IReadOnlyList<BrandToken> tokens, IReadOnlyList<PropertyToken>? properties = null)
    {
        var allowed = AppOverrides.Tokens(tokens);
        var sb = new StringBuilder();
        foreach (var app in apps.Where(a => a.HasOverrides && AppValidator.IsSafe(a)))
        {
            var scope = $"[data-app=\"{app.Id}\"]";
            var light = TokenLines(allowed, app.Tokens?.Values(false) ?? new Dictionary<string, string>(), dark: false);
            var dark = TokenLines(allowed, app.Tokens?.Values(true) ?? new Dictionary<string, string>(), dark: true);
            var props = PropertyBlocks(scope, properties ?? [], app.Properties ?? new Dictionary<string, string>());
            if (light.Count == 0 && dark.Count == 0 && props.Length == 0)
            {
                continue;
            }

            sb.Append(TokenBlocks(scope, $":root[data-theme=\"dark\"]{scope}", $":root:not([data-theme=\"light\"]):not([data-theme=\"dark\"]){scope}", light, dark)).Append(props);
        }

        return sb.Length == 0 ? string.Empty : "\n/* Overrides of apps set in the Studio (SPACE-06, stage 12): after the brand, on [data-app]. A height under 44 px is only for a fine pointer. */\n" + sb;
    }

    private static List<string> TokenLines(IReadOnlyList<BrandToken> tokens, IReadOnlyDictionary<string, string> values, bool dark, bool schemeDark = false)
        => tokens
            .Where(t => values.TryGetValue(t.Name, out var v) && (schemeDark || !dark || t.HasDark) && BrandValidator.CheckValue(t, v) is null)
            .Select(t => $"{t.Name}: {values[t.Name]};")
            .ToList();

    /// <summary>A light block, and the dark values twice and identical: for an explicit dark theme and, in a media query, for the system. A block without lines is left out.</summary>
    private static string TokenBlocks(string lightSelector, string darkSelector, string systemSelector, List<string> light, List<string> dark)
    {
        var sb = new StringBuilder();
        if (light.Count > 0)
        {
            sb.Append(lightSelector).Append(" {\n").AppendJoin('\n', light.Select(l => "  " + l)).Append("\n}\n");
        }

        if (dark.Count > 0)
        {
            sb.Append(darkSelector).Append(" {\n").AppendJoin('\n', dark.Select(l => "  " + l)).Append("\n}\n");
            sb.Append("@media (prefers-color-scheme: dark) {\n  ").Append(systemSelector).Append(" {\n").AppendJoin('\n', dark.Select(l => "    " + l)).Append("\n  }\n}\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The properties with a value of their type within their limits: one block on <paramref name="selector"/> and, for a height of a control that is pressed and is under the touch target of 44 px, a block
    /// inside <c>@media (pointer: fine)</c>. On a touch screen the property is then unset and the control keeps its fallback, <c>--mat-control-h</c>, which the tokens make 44 px under
    /// <c>(pointer: coarse)</c>: nothing can make a touch target smaller (<c>MOBILE-04</c>).
    /// </summary>
    private static string PropertyBlocks(string selector, IReadOnlyList<PropertyToken> properties, IReadOnlyDictionary<string, string> values)
    {
        var valid = properties.Where(p => values.TryGetValue(p.Name, out var v) && BrandValidator.CheckValue(p.Token, v) is null).ToList();
        var always = valid.Where(p => !ComponentTokens.AppliesToFinePointerOnly(p, values[p.Name])).Select(p => $"{p.Name}: {values[p.Name]};").ToList();
        var fine = valid.Where(p => ComponentTokens.AppliesToFinePointerOnly(p, values[p.Name])).Select(p => $"{p.Name}: {values[p.Name]};").ToList();
        var sb = new StringBuilder();
        if (always.Count > 0)
        {
            sb.Append(selector).Append(" {\n").AppendJoin('\n', always.Select(l => "  " + l)).Append("\n}\n");
        }

        if (fine.Count > 0)
        {
            sb.Append("@media (pointer: fine) {\n  ").Append(selector).Append(" {\n").AppendJoin('\n', fine.Select(l => "    " + l)).Append("\n  }\n}\n");
        }

        return sb.ToString();
    }

    /// <summary>The tokens a brand may set, with kind and limits: what the brand editor lists.</summary>
    public IReadOnlyList<BrandToken> EditableTokens { get; }

    /// <summary>The documented component properties the Studio may set, with kind, limits, whether they are touch targets and what each is today.</summary>
    public IReadOnlyList<PropertyToken> EditableProperties { get; }

    /// <summary>Every file of the toolkit and a short README, to be copied into an app's static files (an app never loads them from this site).</summary>
    public byte[] Zip => Current.Zip.Value;

    public string ZipName => $"matui-{AppVersion.MatUi}.zip";

    /// <summary>The bundle as a release names it: the same CSS, with the version of the release in the first line instead of the version of the toolkit.</summary>
    public string ReleaseBundle(string version)
    {
        var state = Current;
        return state.CssBundle.Replace($"MatUi {AppVersion.MatUi} ({state.Hash})", $"MatUi {version} ({state.Hash})", StringComparison.Ordinal);
    }

    /// <summary>The script as a release names it: <c>MatUi.version</c> is the version of the release (the files carry the version of the toolkit, a test holds them to it).</summary>
    public string ReleaseScript(string version)
        => _scriptTemplate.Replace($"version: '{AppVersion.MatUi}'", $"version: '{version}'", StringComparison.Ordinal)
            .Replace($"Version {AppVersion.MatUi} of the toolkit MatUi", $"Version {version} of the toolkit MatUi", StringComparison.Ordinal);

    public string ReleaseBootScript(string version) => _bootTemplate.Replace($"MatUi boot {AppVersion.MatUi}:", $"MatUi boot {version}:", StringComparison.Ordinal);

    /// <summary>The archive as a release names it: the files as the site serves them now, and a README that names the version of the release.</summary>
    public byte[] ReleaseZip(string version)
    {
        var state = Current;
        return BuildZip(ReleaseBundle(version), ReleaseScript(version), ReleaseBootScript(version), state.AppsCss, state.Hash, version);
    }

    /// <summary>The manifest of the toolkit as the site serves it now (<c>/ui/manifest.json</c>).</summary>
    public ToolkitManifest Manifest()
    {
        var state = Current;
        return ManifestOf(AppVersion.MatUi, state.Hash, [
            ("matui.css", "/ui/matui.css", state.CssBundle),
            ("matui.js", "/ui/matui.js", Script),
            ("matui.boot.js", "/ui/matui.boot.js", BootScript),
            ("matui-icons.svg", "/ui/matui-icons.svg", Sprite),
            .. CssParts.Select(p => ($"css/{p}", $"/ui/css/{p}", p == AppsPart ? state.AppsCss : _css[p])),
        ], "/ui/CHANGELOG.md");
    }

    private static ToolkitManifest ManifestOf(string version, string hash, IEnumerable<(string Name, string Url, string Content)> files, string changelog)
        => new("MatUi", version, AppVersion.Current, hash, changelog,
            files.Select(f =>
            {
                var bytes = Encoding.UTF8.GetBytes(f.Content);
                return new ToolkitManifestFile(f.Name, f.Url, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            }).ToList());

    public string ManifestText() => JsonSerializer.Serialize(Manifest(), ManifestJson);

    public string Hash => Current.Hash;

    public string CssBundle => Current.CssBundle;

    public string Script { get; }

    public string BootScript { get; }

    /// <summary>The icon sprite, to be placed inline at the top of every page.</summary>
    public string Sprite { get; }

    public IReadOnlyList<string> IconNames { get; }

    public IReadOnlyList<DesignToken> Tokens { get; }

    /// <summary>The documented component properties (<c>--mat-btn-radius</c> ...), read from the <c>@prop</c> lines of the CSS parts.</summary>
    public IReadOnlyList<ComponentProperty> Properties { get; }

    /// <summary>What each colour scheme sets in <c>matui.tokens.css</c> (the base tokens are <see cref="Tokens"/>).</summary>
    public IReadOnlyList<SchemeBlocks> Schemes { get; }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string, bool), SchemeSurfaces> _surfaces = new();

    /// <summary>The colours of a scheme (null or "standard" for the standard look) in light or dark mode, read from the tokens; the contrast checks and the pages use these.</summary>
    public SchemeSurfaces Surfaces(string? scheme, bool dark)
        => _surfaces.GetOrAdd((ColorSchemes.Normalize(scheme) ?? ColorSchemes.Standard, dark), key => SchemeSurfaces.Read(TokensCss, key.Item1, dark));

    /// <summary>What the toolkit ships (its classes and its icons): the markup of an example may use nothing else.</summary>
    public MarkupFacts Facts { get; }

    public string TokensCss => _css["matui.tokens.css"];

    public string? Css(string name) => name.Equals(AppsPart, StringComparison.OrdinalIgnoreCase) ? Current.AppsCss : _css.GetValueOrDefault(name);

    public IReadOnlyList<ToolkitFile> Files()
    {
        var state = Current;
        return
        [
            new("matui.css", "/ui/matui.css", "All styles in one file: tokens, base, shell, controls, forms, pickers, lists, dialogs, layout, apps.", Encoding.UTF8.GetByteCount(state.CssBundle)),
            new("matui.js", "/ui/matui.js", "Behaviour: theme button, menu drawer, context menus, dialogs, dependent fields, tabs, toasts.", Encoding.UTF8.GetByteCount(Script)),
            new("matui.boot.js", "/ui/matui.boot.js", "Tiny script for <head>: applies the saved theme before the first paint.", Encoding.UTF8.GetByteCount(BootScript)),
            new("matui-icons.svg", "/ui/matui-icons.svg", $"The icon sprite ({IconNames.Count} icons), to be placed inline in the page.", Encoding.UTF8.GetByteCount(Sprite)),
            .. CssParts.Select(p => new ToolkitFile(p, $"/ui/css/{p}", "A part of the bundle, for apps that load them one by one.", Encoding.UTF8.GetByteCount(p == AppsPart ? state.AppsCss : _css[p]))),
        ];
    }

    private byte[] BuildZip(string bundle, string script, string boot, string appsCss, string hash, string version)
    {
        // the files, then the manifest that lists them with their hashes, then the README: an app that copies the archive keeps what it needs to check its copy later
        var files = new List<(string Name, string Content)> { ("matui.css", bundle), ("matui.js", script), ("matui.boot.js", boot), ("matui-icons.svg", Sprite) };
        files.AddRange(CssParts.Select(part => ($"css/{part}", part == AppsPart ? appsCss : _css[part])));
        files.Add(("CHANGELOG.md", Changelog.LoadText()));
        files.Add(("matui.manifest.json", JsonSerializer.Serialize(ManifestOf(version, hash, files.Select(f => (f.Name, f.Name, f.Content)), "CHANGELOG.md"), ManifestJson)));
        files.Add(("README.txt", Readme(hash, version)));

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = zip.CreateEntry($"matui/{name}", CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private string Readme(string hash, string version) => string.Join('\n',
        $"MatUi {version} ({hash})",
        $"Made by {_design.Tenant.SiteName} {AppVersion.Current}.",
        $"The UI toolkit of the {_design.Tenant.Apps}. Rules, documentation and live examples: the {_design.Tenant.SiteName} site.",
        string.Empty,
        "Copy these files into the app's static files. An app never loads them from another site (rule CODE-03); update by copying again.",
        string.Empty,
        "  <html lang=\"en\" data-app=\"yourapp\">      data-app and data-theme belong on the html element",
        "  <head>",
        "    <script src=\"/matui.boot.js\"></script>      applies the saved theme before the first paint",
        "    <link rel=\"stylesheet\" href=\"/matui.css\" />",
        "  </head>",
        "  <body>",
        "    the icon sprite, matui-icons.svg, inline once at the top of the body",
        "    <script src=\"/matui.js\"></script>           at the end of the body",
        string.Empty,
        "An app sets one colour:  [data-app=\"yourapp\"] { --mat-app-accent: #2f6feb; }",
        "css/ holds the parts of matui.css (tokens, base, shell, controls, forms, lists, dialogs, layout, apps) for apps that load them one by one.",
        string.Empty,
        $"Versions (docs/VERSIONING.md on the {_design.Tenant.SiteName} site):",
        "  The version is in the first line of matui.css and in window.MatUi.version; matui.manifest.json lists every file with its SHA-256.",
        "  CHANGELOG.md says what changed in each version. A version with a Breaking section needs a change in the app: its Migration section says what.",
        "  Update by copying ALL the files again, never a few of them: the files of one version belong together.",
        "  To see how an app's copy stands against the toolkit:  node tools/matui-check.js <folder of the app>",
        string.Empty);
    private string Read(string relative)
    {
        var file = _files.GetFileInfo($"_content/MatUi/{relative}");
        if (!file.Exists)
        {
            file = _files.GetFileInfo(relative);
        }

        if (!file.Exists)
        {
            throw new FileNotFoundException($"The toolkit file '{relative}' was not found in the static web assets of MatUi.");
        }

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>The first ten hex characters of the SHA-256 of a text: the tag of a file that has no hash of its own.</summary>
    public static string ShortHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..10].ToLowerInvariant();

    /// <summary>A text response with an ETag, so browsers and other apps revalidate cheaply.</summary>
    public static IResult Result(HttpContext http, string content, string contentType, string hash)
    {
        var etag = $"\"{hash}-{content.Length}\"";
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = "public, max-age=300, must-revalidate";
        if (http.Request.Headers.IfNoneMatch == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Text(content, contentType, Encoding.UTF8);
    }

    [GeneratedRegex(@"<symbol id=""mat-i-([a-z0-9-]+)""")]
    private static partial Regex SymbolRegex();
}
