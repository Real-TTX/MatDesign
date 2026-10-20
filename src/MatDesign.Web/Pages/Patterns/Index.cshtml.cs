using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Patterns;

/// <summary>The page types of the family as a list or a gallery, with the toolbar above.</summary>
public class IndexModel(DesignData design) : PageModel
{
    private const int PageSize = 12;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<PatternDoc> Items { get; private set; } = [];

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var kind = ListQuery.Text(Request, "kind");
        var sort = ListQuery.Text(Request, "sort") ?? "kind";
        var descending = ListQuery.Descending(Request);

        IEnumerable<PatternDoc> items = design.Patterns;
        if (q is not null)
        {
            items = items.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Summary.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (kind is not null)
        {
            items = items.Where(p => p.Kind == kind);
        }

        var matching = items.ToList();
        var ordered = sort == "name"
            ? matching.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : matching.OrderBy(p => PatternCatalog.Kinds.ToList().IndexOf(p.Kind)).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "patterns",
            Action = "/Patterns",
            Label = "Patterns",
            Search = q,
            SearchPlaceholder = "A page type, for example list or sign-in",
            Noun = "patterns",
            Total = design.Patterns.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "kind",
            SortOptions = [new("kind", "Kind"), new("name", "Name")],
            Filters = [new("kind", "Kind", PatternCatalog.Kinds.Select(k => new FilterOption(k, k)).ToList(), kind)],
            Views = [new("list", "List", "list"), new("gallery", "Gallery", "grid")],
            View = "list",
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
