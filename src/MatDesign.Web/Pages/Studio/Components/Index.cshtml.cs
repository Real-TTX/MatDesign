using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio.Components;

/// <summary>The components of the documentation as the Studio keeps them: toolbar above (search, filter, sort as dialogs), table, pagination, and the action to add one below.</summary>
[RequireCapability(Capability.ViewStudio)]
public class IndexModel(StudioService studio, DesignData design) : StudioPageModel(studio)
{
    private const int PageSize = 20;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<ComponentDoc> Items { get; private set; } = [];

    public ItemSource SourceOf(ComponentDoc doc) => design.ComponentSourceOf(doc.Id);

    public static string Label(ItemSource source) => source switch { ItemSource.Added => "Added", ItemSource.Changed => "Changed", _ => "Built in" };

    public static string PillClass(ItemSource source) => source switch { ItemSource.Added => "mat-pill--ok", ItemSource.Changed => "mat-pill--info", _ => string.Empty };

    /// <summary>Where the component is on the site: the page of its own, or its anchor on the page of the layout or the utilities.</summary>
    public static string PageOf(ComponentDoc doc) => ComponentCatalog.PageOf(doc);

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var group = ListQuery.Text(Request, "group");
        var source = ListQuery.Text(Request, "source");
        var sort = ListQuery.Text(Request, "sort") ?? "order";
        var descending = ListQuery.Descending(Request);

        IEnumerable<ComponentDoc> docs = design.Components;
        if (q is not null)
        {
            docs = docs.Where(c => c.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || c.Summary.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (group is not null)
        {
            docs = docs.Where(c => c.Group == group);
        }

        if (source is not null)
        {
            docs = docs.Where(c => SourceOf(c).ToString() == source);
        }

        var matching = docs.ToList();
        IEnumerable<ComponentDoc> ordered = sort switch
        {
            "name" => matching.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "group" => matching.OrderBy(c => ComponentCatalog.Groups.ToList().IndexOf(c.Group)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "examples" => matching.OrderByDescending(c => c.Examples.Count).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "source" => matching.OrderByDescending(c => SourceOf(c) != ItemSource.BuiltIn).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => matching,
        };
        var ordering = ordered.ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordering, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "components",
            Action = "/Studio/Components",
            Label = "Components",
            Search = q,
            SearchPlaceholder = "An id, a name or a word of the summary",
            Noun = "components",
            Total = design.Components.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "order",
            SortOptions =
            [
                new("order", "The order of the page"),
                new("name", "Name"),
                new("group", "Group"),
                new("examples", "Examples (most first)"),
                new("source", "Source (Studio first)"),
            ],
            Filters =
            [
                new("group", "Group", ComponentCatalog.Groups.Select(g => new FilterOption(g, g)).ToList(), group),
                new("source", "Source", [new("BuiltIn", "Built in"), new("Added", "Added in the Studio"), new("Changed", "Built in, changed in the Studio")], source),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
