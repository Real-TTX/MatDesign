using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio.Releases;

/// <summary>The releases of the design system, newest first, with the toolbar of every list, and below them the last runs of the pipeline (a run that failed is kept too).</summary>
public class IndexModel(StudioService studio, ReleaseService releases) : StudioPageModel(studio)
{
    private const int PageSize = 20;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<ReleaseManifest> Items { get; private set; } = [];

    public IReadOnlyList<PipelineRun> Runs { get; private set; } = [];

    public string? Newest { get; private set; }

    public void OnGet()
    {
        var all = releases.Releases();
        Newest = all.FirstOrDefault()?.Version;
        Runs = releases.Runs(10);

        var q = ListQuery.Text(Request, "q");
        var sort = ListQuery.Text(Request, "sort") ?? "version";
        var descending = ListQuery.Descending(Request);

        IEnumerable<ReleaseManifest> rows = all;
        if (q is not null)
        {
            rows = rows.Where(m => m.Version.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Note.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Changelog.Any(c => c.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var matching = rows.ToList();
        var ordering = (sort == "date" ? matching.OrderByDescending(m => m.At) : matching.AsEnumerable()).ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordering, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "releases",
            Action = "/Studio/Releases",
            Label = "Releases",
            Search = q,
            SearchPlaceholder = "A version, a note or a change",
            Noun = "releases",
            Total = all.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "version",
            SortOptions = [new("version", "Version (highest first)"), new("date", "Date (newest first)")],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
