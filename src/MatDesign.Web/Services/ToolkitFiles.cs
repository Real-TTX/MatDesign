using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MatDesign.Core;
using Microsoft.Extensions.FileProviders;

namespace MatDesign.Web.Services;

/// <summary>A file of the toolkit as the download page lists it.</summary>
public sealed record ToolkitFile(string Name, string Url, string Description, int Bytes);

/// <summary>
/// The files of MatUi (CSS parts, script, sprite), read once from the toolkit project's static assets. The site serves them as
/// one bundle (<c>/ui/matui.css</c>) so that any app can use the toolkit with one line, and reads the tokens from the CSS for the docs.
/// </summary>
public sealed partial class ToolkitFiles
{
    /// <summary>The CSS parts in the order they are bundled.</summary>
    public static readonly IReadOnlyList<string> CssParts =
    [
        "matui.tokens.css", "matui.base.css", "matui.shell.css", "matui.controls.css", "matui.lists.css", "matui.dialogs.css", "matui.apps.css",
    ];

    private readonly IFileProvider _files;
    private readonly Dictionary<string, string> _css = new(StringComparer.OrdinalIgnoreCase);

    public ToolkitFiles(IWebHostEnvironment env)
    {
        _files = env.WebRootFileProvider;
        foreach (var part in CssParts)
        {
            _css[part] = Read($"css/{part}");
        }

        Script = Read("js/matui.js");
        BootScript = Read("js/matui.boot.js");
        Sprite = Read("img/matui-icons.svg");
        IconNames = SymbolRegex().Matches(Sprite).Select(m => m.Groups[1].Value).Order().ToList();
        Tokens = CssTokens.Tokens(_css["matui.tokens.css"]);

        Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(_css.Values) + Script + BootScript + Sprite)))[..10].ToLowerInvariant();
        CssBundle = $"/* MatUi {AppVersion.Current} ({Hash}) - bundled from {string.Join(", ", CssParts)} */\n" + string.Join("\n", CssParts.Select(p => _css[p]));
        _zip = new Lazy<byte[]>(BuildZip);
    }

    private readonly Lazy<byte[]> _zip;

    /// <summary>Every file of the toolkit and a short README, to be copied into an app's static files (an app never loads them from this site).</summary>
    public byte[] Zip => _zip.Value;

    public string ZipName => $"matui-{AppVersion.Current}.zip";

    public string Hash { get; }

    public string CssBundle { get; }

    public string Script { get; }

    public string BootScript { get; }

    /// <summary>The icon sprite, to be placed inline at the top of every page.</summary>
    public string Sprite { get; }

    public IReadOnlyList<string> IconNames { get; }

    public IReadOnlyList<DesignToken> Tokens { get; }

    public string TokensCss => _css["matui.tokens.css"];

    public string? Css(string name) => _css.GetValueOrDefault(name);

    public IReadOnlyList<ToolkitFile> Files() =>
    [
        new("matui.css", "/ui/matui.css", "All styles in one file: tokens, base, shell, controls, lists, dialogs, apps.", Encoding.UTF8.GetByteCount(CssBundle)),
        new("matui.js", "/ui/matui.js", "Behaviour: theme button, menu drawer, context menus, dialogs, dependent fields, tabs, toasts.", Encoding.UTF8.GetByteCount(Script)),
        new("matui.boot.js", "/ui/matui.boot.js", "Tiny script for <head>: applies the saved theme before the first paint.", Encoding.UTF8.GetByteCount(BootScript)),
        new("matui-icons.svg", "/ui/matui-icons.svg", $"The icon sprite ({IconNames.Count} icons), to be placed inline in the page.", Encoding.UTF8.GetByteCount(Sprite)),
        .. CssParts.Select(p => new ToolkitFile(p, $"/ui/css/{p}", "A part of the bundle, for apps that load them one by one.", Encoding.UTF8.GetByteCount(_css[p]))),
    ];

    private byte[] BuildZip()
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

            Add("matui.css", CssBundle);
            Add("matui.js", Script);
            Add("matui.boot.js", BootScript);
            Add("matui-icons.svg", Sprite);
            foreach (var part in CssParts)
            {
                Add($"css/{part}", _css[part]);
            }

            Add("README.txt", Readme());
        }

        return stream.ToArray();
    }

    private string Readme() => string.Join('\n',
        $"MatUi {AppVersion.Current} ({Hash})",
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
        "css/ holds the parts of matui.css (tokens, base, shell, controls, lists, dialogs, apps) for apps that load them one by one.",
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
