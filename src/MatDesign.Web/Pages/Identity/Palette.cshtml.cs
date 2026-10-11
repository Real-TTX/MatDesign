using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Identity;

/// <summary>Which accents are too close to tell apart, and which free colours the advisor proposes.</summary>
public class PaletteModel(DesignData design) : PageModel
{
    private const int PageSize = 12;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<Collision> Items { get; private set; } = [];

    public IReadOnlyList<AccentSuggestion> Suggestions { get; private set; } = [];

    public IReadOnlyList<MatApp> ByHue { get; private set; } = [];

    public int TotalCollisions { get; private set; }

    public static string Verdict(double distance) => distance < 0.05 ? "Identical" : distance < 0.25 ? "Very close" : "Close";

    public static string VerdictClass(double distance) => distance < 0.05 ? "mat-pill--error" : distance < 0.25 ? "mat-pill--warn" : "mat-pill--info";

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var verdict = ListQuery.Text(Request, "verdict");
        var sort = ListQuery.Text(Request, "sort") ?? "distance";
        var descending = ListQuery.Descending(Request);

        var all = PaletteAdvisor.Collisions(design.Apps);
        TotalCollisions = all.Count;
        ByHue = design.Apps.OrderBy(IndexModel.Hue).ToList();
        Suggestions = PaletteAdvisor.Suggest(design.Apps);

        IEnumerable<Collision> items = all;
        if (q is not null)
        {
            items = items.Where(c => c.A.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || c.B.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (verdict is not null)
        {
            items = items.Where(c => Verdict(c.Distance) == verdict);
        }

        var matching = items.ToList();
        var ordered = (sort == "name" ? matching.OrderBy(c => c.A.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.B.Name) : matching.OrderBy(c => c.Distance)).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "pairs",
            Action = "/Identity/Palette",
            Label = "Colour pairs",
            Search = q,
            SearchPlaceholder = "An app name",
            Noun = "pairs",
            Total = all.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "distance",
            SortOptions = [new("distance", "Distance (closest first)"), new("name", "Name")],
            Filters = [new("verdict", "Verdict", [new("Identical", "Identical"), new("Very close", "Very close"), new("Close", "Close")], verdict)],
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
