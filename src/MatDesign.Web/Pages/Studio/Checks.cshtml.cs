using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio;

/// <summary>What the checks of the design system find, for the data as it is now: the list of checks with their result, and the findings with the toolbar of every list.</summary>
[RequireCapability(Capability.ViewStudio)]
public class ChecksModel(StudioService studio, DesignChecks checks) : StudioPageModel(studio)
{
    private const int PageSize = 25;

    public CheckReport Report { get; private set; } = null!;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<(DesignCheck Check, Finding Finding)> Items { get; private set; } = [];

    public string? UrlOf(Finding finding) => checks.UrlOf(finding);

    /// <summary>Runs every check again now (they are also run by themselves when the data or the people change) and shows the result.</summary>
    [RequireCapability(Capability.RunChecks)]
    public IActionResult OnPostRun()
    {
        var report = checks.Refresh();
        TempData["Flash"] = report.Passed ? "The checks were run again: all pass." : $"The checks were run again: {StudioFormat.Count(report.Errors, "error")}.";
        return RedirectToPage("/Studio/Checks");
    }

    public void OnGet()
    {
        Report = checks.Report;
        var q = ListQuery.Text(Request, "q");
        var severity = ListQuery.Text(Request, "severity");
        var check = ListQuery.Text(Request, "check");
        var area = ListQuery.Text(Request, "area");
        var sort = ListQuery.Text(Request, "sort") ?? "severity";
        var descending = ListQuery.Descending(Request);

        var all = Report.Results.SelectMany(r => r.Findings.Select(f => (r.Check, Finding: f))).ToList();
        IEnumerable<(DesignCheck Check, Finding Finding)> rows = all;
        if (q is not null)
        {
            rows = rows.Where(r => r.Finding.Message.Contains(q, StringComparison.OrdinalIgnoreCase) || (r.Finding.Id?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (severity is not null)
        {
            rows = rows.Where(r => r.Finding.Severity.ToString() == severity);
        }

        if (check is not null)
        {
            rows = rows.Where(r => r.Check.Id == check);
        }

        if (area is not null)
        {
            rows = rows.Where(r => r.Check.Area == area);
        }

        var matching = rows.ToList();
        var ordering = (sort switch
        {
            "check" => matching.OrderBy(r => DesignChecks.All.ToList().IndexOf(r.Check)),
            "item" => matching.OrderBy(r => r.Finding.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => matching.OrderBy(r => r.Finding.Severity).ThenBy(r => DesignChecks.All.ToList().IndexOf(r.Check)),
        }).ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordering, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "findings",
            Action = "/Studio/Checks",
            Label = "Findings",
            Search = q,
            SearchPlaceholder = "A word of the message, or the id of an item",
            Noun = "findings",
            Total = all.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "severity",
            SortOptions = [new("severity", "Severity (errors first)"), new("check", "Check"), new("item", "Item")],
            Filters =
            [
                new("severity", "Severity", [new("Error", "Errors"), new("Advice", "Advice")], severity),
                new("area", "Area", DesignChecks.All.Select(c => c.Area).Distinct().Select(a => new FilterOption(a, a)).ToList(), area),
                new("check", "Check", DesignChecks.All.Select(c => new FilterOption(c.Id, c.Name)).ToList(), check),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
