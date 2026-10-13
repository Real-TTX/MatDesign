using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Foundations;

/// <summary>Every custom property of the toolkit as a list: search, filter by group and by colour, sort, pagination.</summary>
public class TokensModel : PageModel
{
    private const int PageSize = 25;

    private readonly ToolkitFiles _toolkit;

    public TokensModel(ToolkitFiles toolkit)
    {
        _toolkit = toolkit;
    }

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<DesignToken> Items { get; private set; } = [];

    /// <summary>A colour the page can draw as a swatch (a hex or rgb value); derived colours (color-mix, var) are not drawn.</summary>
    public static bool Drawable(string value) => value.StartsWith('#') || value.StartsWith("rgb", StringComparison.Ordinal);

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var group = ListQuery.Text(Request, "group");
        var kind = ListQuery.Text(Request, "kind");
        var sort = ListQuery.Text(Request, "sort") ?? "file";
        var descending = ListQuery.Descending(Request);

        // the base tokens, then what each colour scheme sets over them (the group "Schemes": the same token can appear once per scheme)
        IReadOnlyList<DesignToken> all = [.. _toolkit.Tokens, .. SchemeDocs.TokenRows(_toolkit)];
        IEnumerable<DesignToken> tokens = all;
        if (q is not null)
        {
            tokens = tokens.Where(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Light.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (t.Dark?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) || t.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (group is not null)
        {
            tokens = tokens.Where(t => t.Group == group);
        }

        if (kind == "colour")
        {
            tokens = tokens.Where(t => t.IsColor);
        }
        else if (kind == "dark")
        {
            tokens = tokens.Where(t => t.Dark is not null);
        }
        else if (kind == "other")
        {
            tokens = tokens.Where(t => !t.IsColor);
        }

        var matching = tokens.ToList();
        var ordered = (sort == "name" ? matching.OrderBy(t => t.Name, StringComparer.Ordinal).ToList() : matching);
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "tokens",
            Action = "/Foundations/Tokens",
            Label = "Tokens",
            Search = q,
            SearchPlaceholder = "A name, a value or a word from the description",
            Noun = "tokens",
            Total = all.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "file",
            SortOptions = [new("file", "As in the file"), new("name", "Name")],
            Filters =
            [
                new("group", "Group", all.Select(t => t.Group).Distinct().Select(g => new FilterOption(g, g)).ToList(), group),
                new("kind", "Kind", [new("colour", "Colours"), new("dark", "Different in dark mode"), new("other", "Not a colour")], kind),
            ],
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
