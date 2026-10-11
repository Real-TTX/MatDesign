using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Identity;

/// <summary>How close each app is to the rules today, from the static scan in <c>Data/audit.json</c>.</summary>
public class AuditModel : PageModel
{
    private readonly AuditData _audit;
    private readonly DesignData _design;

    public AuditModel(AuditData audit, DesignData design)
    {
        _audit = audit;
        _design = design;
    }

    public AuditData Audit => _audit;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public IReadOnlyList<(MatApp App, AuditApp Scan, double Score)> Rows { get; private set; } = [];

    /// <summary>yes = 1, partial = 0.5, no = 0; "unknown" does not count. In percent of the checks that could be told.</summary>
    public static double Score(AuditApp app)
    {
        var known = app.Results.Values.Where(r => r.State != "unknown").ToList();
        return known.Count == 0 ? -1 : known.Sum(r => r.State == "yes" ? 1.0 : r.State == "partial" ? 0.5 : 0) * 100 / known.Count;
    }

    public static string Mark(string state) => state switch { "yes" => "check", "partial" => "minus", "no" => "x", _ => "help-circle" };

    public static string Word(string state) => state switch { "yes" => "Yes", "partial" => "Partly", "no" => "No", _ => "Unknown" };

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var category = ListQuery.Text(Request, "category");
        var sort = ListQuery.Text(Request, "sort") ?? "name";
        var descending = ListQuery.Descending(Request);

        var rows = _audit.Apps
            .Select(a => (App: _design.App(a.Id)!, Scan: a, Score: Score(a)))
            .Where(r => r.App is not null)
            .ToList();

        IEnumerable<(MatApp App, AuditApp Scan, double Score)> items = rows;
        if (q is not null)
        {
            items = items.Where(r => r.App.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (category is not null)
        {
            items = items.Where(r => r.App.Category == category);
        }

        var matching = items.ToList();
        var ordered = (sort switch
        {
            "score" => matching.OrderByDescending(r => r.Score).ThenBy(r => r.App.Name, StringComparer.OrdinalIgnoreCase),
            _ => matching.OrderBy(r => r.App.Name, StringComparer.OrdinalIgnoreCase),
        }).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        Rows = ordered;
        Toolbar = new ToolbarModel
        {
            Id = "audit",
            Action = "/Identity/Audit",
            Label = "Audit",
            Search = q,
            SearchPlaceholder = "An app name",
            Noun = "apps",
            Total = rows.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "name",
            SortOptions = [new("name", "Name"), new("score", "Score (closest to the rules first)")],
            Filters = [new("category", "Category", AppRegistry.Categories.Select(c => new FilterOption(c, c)).ToList(), category)],
        };
    }
}
