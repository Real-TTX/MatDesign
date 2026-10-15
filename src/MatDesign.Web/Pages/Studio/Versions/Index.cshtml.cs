using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio.Versions;

/// <summary>Which release of MatUi each app uses and where that stands: toolbar above (search, filter, sort as dialogs), table, pagination.</summary>
[RequireCapability(Capability.ViewStudio)]
public class IndexModel(StudioService studio, VersionTracker tracker) : StudioPageModel(studio)
{
    private const int PageSize = 25;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<UsageRow> Items { get; private set; } = [];

    public string? Latest => tracker.LatestVersion;

    public int Behind { get; private set; }

    public static string Label(UsageRow row) => row.State switch
    {
        VersionState.Current => "Latest",
        VersionState.Behind => StudioFormat.Count(row.Behind, "release") + " behind",
        VersionState.Foreign => "Not from a release",
        VersionState.NoRelease => "No release yet",
        _ => "Not written down",
    };

    public static string PillClass(UsageRow row) => row.State switch
    {
        VersionState.Current => "mat-pill--ok",
        VersionState.Behind => "mat-pill--warn",
        VersionState.Foreign => "mat-pill--info",
        _ => "mat-pill--plain",
    };

    public void OnGet()
    {
        var all = tracker.All();
        Behind = all.Count(r => r.State == VersionState.Behind);
        var q = ListQuery.Text(Request, "q");
        var state = ListQuery.Text(Request, "state");
        var sort = ListQuery.Text(Request, "sort") ?? "name";
        var descending = ListQuery.Descending(Request);

        IEnumerable<UsageRow> rows = all;
        if (q is not null)
        {
            rows = rows.Where(r => r.App.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.App.Id.Contains(q, StringComparison.OrdinalIgnoreCase) || (r.Version?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (state is not null)
        {
            rows = rows.Where(r => r.State.ToString() == state);
        }

        var matching = rows.ToList();
        var ordering = (sort switch
        {
            "state" => matching.OrderByDescending(r => r.State == VersionState.Behind).ThenByDescending(r => r.Behind).ThenBy(r => r.App.Name, StringComparer.OrdinalIgnoreCase),
            "version" => matching.OrderBy(r => r.Version is null).ThenBy(r => r.Version, StringComparer.Ordinal).ThenBy(r => r.App.Name, StringComparer.OrdinalIgnoreCase),
            _ => matching.OrderBy(r => r.App.Name, StringComparer.OrdinalIgnoreCase),
        }).ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordering, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "versions",
            Action = "/Studio/Versions",
            Label = "Versions",
            Search = q,
            SearchPlaceholder = "An app or a version",
            Noun = "apps",
            Total = all.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "name",
            SortOptions = [new("name", "Name"), new("state", "Behind first"), new("version", "Version")],
            Filters =
            [
                new("state", "Where it stands", [new("Behind", "Behind the latest"), new("Current", "On the latest"), new("Foreign", "Not from a release"), new("Unknown", "Not written down")], state),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
