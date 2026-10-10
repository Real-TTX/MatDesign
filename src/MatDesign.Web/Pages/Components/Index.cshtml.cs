using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Components;

/// <summary>The controls of the toolkit as a list or a gallery, with the toolbar above (search, filter, sort).</summary>
public class IndexModel : PageModel
{
    private const int PageSize = 12;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<ComponentDoc> Items { get; private set; } = [];

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var group = ListQuery.Text(Request, "group");
        var sort = ListQuery.Text(Request, "sort") ?? "group";
        var descending = ListQuery.Descending(Request);

        IEnumerable<ComponentDoc> items = ComponentCatalog.All;
        if (q is not null)
        {
            items = items.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (group is not null)
        {
            items = items.Where(c => c.Group == group);
        }

        var matching = items.ToList();
        var ordered = (sort == "name"
            ? matching.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : matching.OrderBy(c => ComponentCatalog.Groups.ToList().IndexOf(c.Group)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "components",
            Action = "/Components",
            Label = "Components",
            Search = q,
            SearchPlaceholder = "A control, for example table or dialog",
            Noun = "components",
            Total = ComponentCatalog.All.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "group",
            SortOptions = [new("group", "Group"), new("name", "Name")],
            Filters = [new("group", "Group", ComponentCatalog.Groups.Select(g => new FilterOption(g, g)).ToList(), group)],
            Views = [new("list", "List", "list"), new("gallery", "Gallery", "grid")],
            View = "list",
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
