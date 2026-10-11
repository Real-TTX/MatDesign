using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace MatDesign.Web.Services;

public sealed record IconGroup(string Id, string Title);

public sealed record IconCategory(string Id, string Title, string Description, string Group, string Icon, int Count);

/// <summary>An icon of the library: its name, the categories it is in (any number), the words it is found by and where it comes from (<c>lucide</c> or <c>matdesign</c>).</summary>
public sealed record IconEntry(string Name, IReadOnlyList<string> Categories, IReadOnlyList<string> Tags, string Origin);

/// <summary>
/// The optional icon library: a sprite of icons of every kind (<c>matui-iconlib.svg</c>, ids <c>mat-il-NAME</c>), a catalogue that sorts them into categories (an icon can be in several) and the
/// files to hand out. It lives in its own folder of the toolkit's static assets (<c>iconlib/</c>, built by <c>node tools/iconlib.js</c>), is not part of <c>matui.css</c> and nothing in the
/// toolkit depends on it; an app takes it when it needs more icons than the small sprite has. The site reads the folder once, shows it on the icon library page and serves the files under
/// <c>/ui/iconlib/</c>: the sprite, the catalogue, each icon as a single .svg file and all of it as one archive.
/// </summary>
public sealed partial class IconLibrary
{
    public const string IdPrefix = "mat-il-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IFileProvider _files;
    private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IconEntry> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IconCategory> _categories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _rank = new(StringComparer.Ordinal);
    private readonly Lazy<byte[]> _zip;

    public IconLibrary(IWebHostEnvironment env)
    {
        _files = env.WebRootFileProvider;
        Sprite = Read("iconlib/matui-iconlib.svg");
        Catalogue = Read("iconlib/iconlib.json");
        License = Read("iconlib/LICENSE.txt");
        Readme = Read("iconlib/README.txt");

        foreach (Match m in SymbolRegex().Matches(Sprite))
        {
            _bodies[m.Groups[1].Value] = m.Groups[2].Value;
        }

        var file = JsonSerializer.Deserialize<CatalogueFile>(Catalogue, Json) ?? throw new InvalidDataException("iconlib.json is empty.");
        Groups = file.Groups;
        Categories = file.Categories;
        Icons = file.Items.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        foreach (var category in Categories)
        {
            _rank[category.Id] = _categories.Count;
            _categories[category.Id] = category;
        }

        foreach (var icon in Icons)
        {
            _byName[icon.Name] = icon;
        }

        Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sprite + Catalogue)))[..10].ToLowerInvariant();
        _zip = new Lazy<byte[]>(BuildZip);
    }

    private sealed record CatalogueFile(IReadOnlyList<IconGroup> Groups, IReadOnlyList<IconCategory> Categories, IReadOnlyList<IconEntry> Items);

    /// <summary>The sprite as built: one symbol per icon.</summary>
    public string Sprite { get; }

    /// <summary>The catalogue as built (JSON), served as it is.</summary>
    public string Catalogue { get; }

    public string License { get; }

    public string Readme { get; }

    public string Hash { get; }

    public IReadOnlyList<IconGroup> Groups { get; }

    /// <summary>The categories in the order the page shows them: the start groups first, then the areas of the home, then the topics.</summary>
    public IReadOnlyList<IconCategory> Categories { get; }

    /// <summary>Every icon, by name.</summary>
    public IReadOnlyList<IconEntry> Icons { get; }

    public IconEntry? Find(string name) => _byName.GetValueOrDefault(name);

    public IconCategory? Category(string id) => _categories.GetValueOrDefault(id);

    /// <summary>The place of a category in the order the page shows them (an unknown one comes last).</summary>
    public int Rank(string categoryId) => _rank.GetValueOrDefault(categoryId, int.MaxValue);

    /// <summary>The shapes of an icon, as they stand in the sprite.</summary>
    public string? Body(string name) => _bodies.GetValueOrDefault(name);

    /// <summary>The icon as a file of its own: it takes its colour from the text around it when it is placed inline, and from the colour of the svg element otherwise.</summary>
    public string Svg(string name)
        => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{_bodies[name]}</svg>\n";

    /// <summary>The markup that shows the icon on a page that has the library sprite inline (the same form as <c>Ui.Icon</c> uses for the toolkit sprite).</summary>
    public static string Markup(string name)
        => $"<svg class=\"mat-icon\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#{IdPrefix}{name}\"></use></svg>";

    /// <summary>A sprite with only the given icons, for a page that shows a few of them: the page does not carry the whole library.</summary>
    public string SpriteOf(IEnumerable<string> names)
    {
        var sb = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"mat-sprite\" width=\"0\" height=\"0\" aria-hidden=\"true\" focusable=\"false\">");
        foreach (var name in names.Distinct())
        {
            if (_bodies.TryGetValue(name, out var body))
            {
                sb.Append("<symbol id=\"").Append(IdPrefix).Append(name).Append("\" viewBox=\"0 0 24 24\">").Append(body).Append("</symbol>");
            }
        }

        return sb.Append("</svg>").ToString();
    }

    /// <summary>The library as an archive: the sprite, the catalogue, the licence, the README and every icon as a file of its own under <c>svg/</c>.</summary>
    public byte[] Zip => _zip.Value;

    public IReadOnlyList<ToolkitFile> Files() =>
    [
        new("matui-iconlib.svg", "/ui/iconlib/matui-iconlib.svg", $"The library sprite ({Icons.Count} icons), to be placed inline in the page, or to copy a few icons from.", Encoding.UTF8.GetByteCount(Sprite)),
        new("iconlib.json", "/ui/iconlib/iconlib.json", $"The catalogue: {Categories.Count} categories and every icon with its categories, tags and origin.", Encoding.UTF8.GetByteCount(Catalogue)),
        new("matui-iconlib.zip", "/ui/iconlib/matui-iconlib.zip", "Everything above and each icon as a single .svg file, with the licence.", Zip.Length),
        new("LICENSE.txt", "/ui/iconlib/LICENSE.txt", "Where the icons come from and under which licence. Keep it with the icons.", Encoding.UTF8.GetByteCount(License)),
    ];

    private byte[] BuildZip()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                var entry = zip.CreateEntry($"matui-iconlib/{name}", CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add("matui-iconlib.svg", Sprite);
            Add("iconlib.json", Catalogue);
            Add("LICENSE.txt", License);
            Add("README.txt", Readme);
            foreach (var icon in Icons)
            {
                Add($"svg/{icon.Name}.svg", Svg(icon.Name));
            }
        }

        return stream.ToArray();
    }

    private string Read(string relative)
    {
        var file = _files.GetFileInfo($"_content/MatUi/{relative}");
        if (!file.Exists)
        {
            file = _files.GetFileInfo(relative);
        }

        if (!file.Exists)
        {
            throw new FileNotFoundException($"The icon library file '{relative}' was not found in the static web assets of MatUi. Run node tools/iconlib.js.");
        }

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"<symbol id=""mat-il-([a-z0-9-]+)"" viewBox=""0 0 24 24"">(.*?)</symbol>")]
    private static partial Regex SymbolRegex();
}
