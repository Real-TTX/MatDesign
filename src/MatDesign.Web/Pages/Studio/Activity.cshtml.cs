using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio;

/// <summary>Every change made in the Studio as a list: toolbar above (search, filter, sort as dialogs), table, pagination.</summary>
[RequireCapability(Capability.ViewStudio)]
public class ActivityModel(StudioService studio) : StudioPageModel(studio)
{
    private const int PageSize = 20;

    private static readonly string[] Changes = ["Created", "Changed", "Deleted", "Restored"];

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<StoredItem> Items { get; private set; } = [];

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var kind = ListQuery.Text(Request, "kind");
        var change = ListQuery.Text(Request, "change");
        var by = ListQuery.Text(Request, "by");
        var sort = ListQuery.Text(Request, "sort") ?? "when";
        var descending = ListQuery.Descending(Request);

        var store = Studio.Store;
        var everything = store.Activity(int.MaxValue);
        IEnumerable<StoredItem> rows = everything;
        if (q is not null)
        {
            rows = rows.Where(r => r.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || StudioFormat.ItemTitle(r).Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Revision.Note.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Revision.By.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (kind is not null)
        {
            rows = rows.Where(r => r.Kind == kind);
        }

        if (change is not null)
        {
            rows = rows.Where(r => StudioFormat.What(r.Revision) == change);
        }

        if (by is not null)
        {
            rows = rows.Where(r => r.Revision.By == by);
        }

        var matching = rows.ToList();
        var ordering = (sort switch
        {
            "item" => matching.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.Revision.Number),
            "kind" => matching.OrderBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase),
            _ => matching.OrderByDescending(r => r.Revision.At).ThenByDescending(r => r.Revision.Number),
        }).ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordering, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "activity",
            Action = "/Studio/Activity",
            Label = "Activity",
            Search = q,
            SearchPlaceholder = "An item, a note or a person",
            Noun = "changes",
            Total = everything.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "when",
            SortOptions =
            [
                new("when", "When (newest first)"),
                new("item", "Item"),
                new("kind", "Kind"),
            ],
            Filters =
            [
                new("kind", "Kind", store.Kinds().Select(k => new FilterOption(k, StudioFormat.KindName(k))).ToList(), kind),
                new("change", "Change", Changes.Select(c => new FilterOption(c, c)).ToList(), change),
                new("by", "By", everything.Select(r => r.Revision.By).Distinct().Order(StringComparer.Ordinal).Select(b => new FilterOption(b, b)).ToList(), by),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
