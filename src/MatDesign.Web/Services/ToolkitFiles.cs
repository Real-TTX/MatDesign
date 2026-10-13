using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.Extensions.FileProviders;

namespace MatDesign.Web.Services;

/// <summary>A file of the toolkit as the download page lists it.</summary>
public sealed record ToolkitFile(string Name, string Url, string Description, int Bytes);

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

        Script = Read("js/matui.js");
        BootScript = Read("js/matui.boot.js");
        Sprite = Read("img/matui-icons.svg");
        IconNames = SymbolRegex().Matches(Sprite).Select(m => m.Groups[1].Value).Order().ToList();
        Tokens = CssTokens.Tokens(_css["matui.tokens.css"]);
        Schemes = CssTokens.Schemes(_css["matui.tokens.css"]);
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
        var appsCss = _css[AppsPart] + AppsOverlay(_design.StudioApps);
        string Part(string name) => name == AppsPart ? appsCss : _css[name];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(CssParts.Select(Part)) + Script + BootScript + Sprite)))[..10].ToLowerInvariant();
        var bundle = $"/* MatUi {AppVersion.Current} ({hash}) - bundled from {string.Join(", ", CssParts)} */\n" + string.Join("\n", CssParts.Select(Part));
        return new State(version, appsCss, hash, bundle, new Lazy<byte[]>(() => BuildZip(bundle, appsCss, hash, AppVersion.Current)));
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

    /// <summary>Every file of the toolkit and a short README, to be copied into an app's static files (an app never loads them from this site).</summary>
    public byte[] Zip => Current.Zip.Value;

    public string ZipName => $"matui-{AppVersion.Current}.zip";

    /// <summary>The bundle as a release names it: the same CSS, with the version of the release in the first line instead of the one of this build.</summary>
    public string ReleaseBundle(string version)
    {
        var state = Current;
        return state.CssBundle.Replace($"MatUi {AppVersion.Current} ({state.Hash})", $"MatUi {version} ({state.Hash})", StringComparison.Ordinal);
    }

    /// <summary>The archive as a release names it: the files as the site serves them now, and a README that names the version of the release.</summary>
    public byte[] ReleaseZip(string version)
    {
        var state = Current;
        return BuildZip(ReleaseBundle(version), state.AppsCss, state.Hash, version);
    }

    public string Hash => Current.Hash;

    public string CssBundle => Current.CssBundle;

    public string Script { get; }

    public string BootScript { get; }

    /// <summary>The icon sprite, to be placed inline at the top of every page.</summary>
    public string Sprite { get; }

    public IReadOnlyList<string> IconNames { get; }

    public IReadOnlyList<DesignToken> Tokens { get; }

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

    private byte[] BuildZip(string bundle, string appsCss, string hash, string label)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                var entry = zip.CreateEntry($"matui/{name}", CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add("matui.css", bundle);
            Add("matui.js", Script);
            Add("matui.boot.js", BootScript);
            Add("matui-icons.svg", Sprite);
            foreach (var part in CssParts)
            {
                Add($"css/{part}", part == AppsPart ? appsCss : _css[part]);
            }

            Add("README.txt", Readme(hash, label));
        }

        return stream.ToArray();
    }

    private string Readme(string hash, string label) => string.Join('\n',
        $"MatUi {label} ({hash})",
        "The UI toolkit of the Mat apps. Rules, documentation and live examples: the MatDesign site.",
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
