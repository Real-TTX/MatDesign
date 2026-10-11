using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio.Apps;

/// <summary>The apps of the family as the Studio keeps them: toolbar above (search, filter, sort as dialogs), table, pagination, and the action to add one below.</summary>
public class IndexModel(StudioService studio, DesignData design) : StudioPageModel(studio)
{
    private const int PageSize = 20;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<MatApp> Items { get; private set; } = [];

    public ItemSource SourceOf(MatApp app) => design.SourceOf(app.Id);

    public static string Label(ItemSource source) => source switch { ItemSource.Added => "Added", ItemSource.Changed => "Changed", _ => "Built in" };

    public static string PillClass(ItemSource source) => source switch { ItemSource.Added => "mat-pill--ok", ItemSource.Changed => "mat-pill--info", _ => string.Empty };

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var category = ListQuery.Text(Request, "category");
        var source = ListQuery.Text(Request, "source");
        var sort = ListQuery.Text(Request, "sort") ?? "name";
        var descending = ListQuery.Descending(Request);

        IEnumerable<MatApp> apps = design.Apps;
        if (q is not null)
        {
            apps = apps.Where(a => a.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || a.Tagline.Contains(q, StringComparison.OrdinalIgnoreCase)
                || a.Accent.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (category is not null)
        {
            apps = apps.Where(a => a.Category == category);
        }

        if (source is not null)
        {
            apps = apps.Where(a => SourceOf(a).ToString() == source);
        }

        var matching = apps.ToList();
        IEnumerable<MatApp> ordered = sort switch
        {
            "category" => matching.OrderBy(a => AppRegistry.Categories.ToList().IndexOf(a.Category)).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            "colour" => matching.OrderBy(Identity.IndexModel.Hue).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            "source" => matching.OrderByDescending(a => SourceOf(a) != ItemSource.BuiltIn).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            _ => matching.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
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
            Id = "apps",
            Action = "/Studio/Apps",
            Label = "Apps",
            Search = q,
            SearchPlaceholder = "An id, a name, a tagline or a colour",
            Noun = "apps",
            Total = design.Apps.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "name",
            SortOptions =
            [
                new("name", "Name"),
                new("category", "Category"),
                new("colour", "Colour (around the colour wheel)"),
                new("source", "Source (Studio first)"),
            ],
            Filters =
            [
                new("category", "Category", AppRegistry.Categories.Select(c => new FilterOption(c, c)).ToList(), category),
                new("source", "Source", [new("BuiltIn", "Built in"), new("Added", "Added in the Studio"), new("Changed", "Built in, changed in the Studio")], source),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
