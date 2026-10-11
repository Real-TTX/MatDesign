using System.Text.Json;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Foundations;

/// <summary>
/// The icon library as a gallery: every icon with its categories (an icon can be in several), a search that looks at the name, the tags and the category, the categories as an
/// index beside it (an icon in a dialog on a small window), a filter by category, a sort, and a click on an icon opens a dialog with the markup to copy and the file to take.
/// The page carries only the icons it shows: a sprite of the page, not of the whole library.
/// </summary>
public class IconLibraryModel(IconLibrary library) : PageModel
{
    private const int PageSize = 96;

    public sealed record CategoryLink(IconCategory Category, string Url, bool Active);

    public sealed record CategoryGroup(IconGroup Group, IReadOnlyList<CategoryLink> Categories);

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<IconEntry> Items { get; private set; } = [];

    /// <summary>The category the list is narrowed to; null for all icons.</summary>
    public IconCategory? Category { get; private set; }

    public IReadOnlyList<CategoryGroup> CategoryGroups { get; private set; } = [];

    public string AllUrl { get; private set; } = "/Foundations/IconLibrary";

    public int Total => library.Icons.Count;

    public int CategoryCount => library.Categories.Count;

    public int HomeAutomationCount => library.Categories.First(c => c.Id == "home-automation").Count;

    /// <summary>The symbols of the icons on this page, to be placed inline: the page does not carry the whole library.</summary>
    public string PageSprite => library.SpriteOf(Items.Select(i => i.Name));

    public static string Markup(string name) => IconLibrary.Markup(name);

    public string TitleOf(string categoryId) => library.Category(categoryId)?.Title ?? categoryId;

    /// <summary>What the dialog needs to know about an icon, as a JSON array of [id, title] pairs (it is an attribute of the icon's button).</summary>
    public string CategoriesJson(IconEntry icon) => JsonSerializer.Serialize(icon.Categories.Select(c => new[] { c, TitleOf(c) }));

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var categoryId = ListQuery.Text(Request, "category");
        Category = categoryId is null ? null : library.Category(categoryId);
        var tokens = (q ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var defaultSort = tokens.Length > 0 ? "match" : "name";
        var sort = ListQuery.Text(Request, "sort") ?? defaultSort;
        var descending = ListQuery.Descending(Request);

        IEnumerable<IconEntry> icons = library.Icons;
        if (Category is not null)
        {
            icons = icons.Where(i => i.Categories.Contains(Category.Id));
        }

        if (tokens.Length > 0)
        {
            icons = icons.Where(i => tokens.All(t => Score(i, t) < NoMatch));
        }

        var matching = icons.ToList();
        var ordered = (sort switch
        {
            "category" => matching.OrderBy(i => i.Categories.Min(library.Rank)).ThenBy(i => i.Name, StringComparer.Ordinal),
            "match" when tokens.Length > 0 => matching.OrderBy(i => tokens.Sum(t => Score(i, t))).ThenBy(i => i.Name, StringComparer.Ordinal),
            _ => matching.OrderBy(i => i.Name, StringComparer.Ordinal),
        }).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        var sortOptions = new List<SortOption> { new("name", "Name"), new("category", "Category") };
        if (tokens.Length > 0)
        {
            sortOptions.Insert(0, new("match", "Best match"));
        }

        Toolbar = new ToolbarModel
        {
            Id = "iconlib",
            Action = "/Foundations/IconLibrary",
            Label = "Icon library",
            Search = q,
            SearchField = true,
            SearchPlaceholder = "Name or word, e.g. lamp",
            Noun = "icons",
            Total = Category is null ? library.Icons.Count : Category.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = defaultSort,
            SortOptions = sortOptions,
            Filters = [new("category", "Category", library.Categories.Select(c => new FilterOption(c.Id, $"{c.Title} ({c.Count})")).ToList(), Category?.Id)],
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };

        AllUrl = Toolbar.UrlWithout("category");
        CategoryGroups = library.Groups
            .Select(g => new CategoryGroup(g, library.Categories.Where(c => c.Group == g.Id).Select(c => new CategoryLink(c, WithCategory(AllUrl, c.Id), c.Id == Category?.Id)).ToList()))
            .Where(g => g.Categories.Count > 0)
            .ToList();
    }

    private const int NoMatch = 100;

    /// <summary>How well an icon answers a word: 0 is the name itself, higher is further away, <see cref="NoMatch"/> is not at all (name first, then the tags, then the categories).</summary>
    private int Score(IconEntry icon, string word)
    {
        if (icon.Name == word)
        {
            return 0;
        }

        if (icon.Name.StartsWith(word, StringComparison.Ordinal))
        {
            return 1;
        }

        if (icon.Name.Contains(word, StringComparison.Ordinal))
        {
            return 2;
        }

        if (icon.Tags.Any(t => t.Equals(word, StringComparison.OrdinalIgnoreCase)))
        {
            return 3;
        }

        if (icon.Tags.Any(t => t.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            return 4;
        }

        return icon.Categories.Any(c => TitleOf(c).Contains(word, StringComparison.OrdinalIgnoreCase)) ? 5 : NoMatch;
    }

    private static string WithCategory(string url, string id) => url + (url.Contains('?') ? "&" : "?") + "category=" + Uri.EscapeDataString(id);
}
