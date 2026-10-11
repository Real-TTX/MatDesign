using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Identity;

/// <summary>The apps of the family as a list or a gallery, with the toolbar above (search, filter, sort).</summary>
public class IndexModel(DesignData design) : PageModel
{
    private const int PageSize = 24;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<MatApp> Items { get; private set; } = [];

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var category = ListQuery.Text(Request, "category");
        var colour = ListQuery.Text(Request, "colour");
        var sort = ListQuery.Text(Request, "sort") ?? "name";
        var descending = ListQuery.Descending(Request);

        IEnumerable<MatApp> apps = design.Apps;
        if (q is not null)
        {
            apps = apps.Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || a.Tagline.Contains(q, StringComparison.OrdinalIgnoreCase) || a.Accent.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (category is not null)
        {
            apps = apps.Where(a => a.Category == category);
        }

        if (colour == "changed")
        {
            apps = apps.Where(a => a.FoundAccent is not null && !a.FoundAccent.Equals(a.Accent, StringComparison.OrdinalIgnoreCase));
        }
        else if (colour == "same")
        {
            apps = apps.Where(a => a.FoundAccent is not null && a.FoundAccent.Equals(a.Accent, StringComparison.OrdinalIgnoreCase));
        }
        else if (colour == "none")
        {
            apps = apps.Where(a => a.FoundAccent is null);
        }

        var matching = apps.ToList();
        var ordered = (sort switch
        {
            "category" => matching.OrderBy(a => AppRegistry.Categories.ToList().IndexOf(a.Category)).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            "hue" => matching.OrderBy(a => Hue(a)),
            _ => matching.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
        }).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "apps",
            Action = "/Identity",
            Label = "Apps",
            Search = q,
            SearchPlaceholder = "Name, tagline or colour",
            Noun = "apps",
            Total = design.Apps.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "name",
            SortOptions = [new("name", "Name"), new("category", "Category"), new("hue", "Colour (by hue)")],
            Filters =
            [
                new("category", "Category", AppRegistry.Categories.Select(c => new FilterOption(c, c)).ToList(), category),
                new("colour", "Colour in the app", [new("changed", "Changed by the toolkit"), new("same", "As found"), new("none", "None found")], colour),
            ],
            Views = [new("list", "List", "list"), new("gallery", "Gallery", "grid")],
            View = "list",
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }

    /// <summary>The hue of the accent in degrees (OKLCH), for sorting the colours around the wheel.</summary>
    public static double Hue(MatApp app)
    {
        var lab = Colors.ToOklab(app.AccentRgb);
        var h = Math.Atan2(lab.B, lab.A) * 180 / Math.PI;
        return h < 0 ? h + 360 : h;
    }
}
