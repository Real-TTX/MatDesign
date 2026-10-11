using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Rules;

/// <summary>The rulebook as a list: toolbar above (search, filter, sort as dialogs), table, pagination.</summary>
public class IndexModel(DesignData design) : PageModel
{
    private const int PageSize = 20;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<Rule> Items { get; private set; } = [];

    /// <summary>Who checks a rule: a test of this repository, the palette report, or a reviewer.</summary>
    public static string CheckedBy(Rule rule)
        => rule.Check.StartsWith("Test", StringComparison.Ordinal) ? "Test" : rule.Check.StartsWith("Report", StringComparison.Ordinal) ? "Report" : "Review";

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var area = ListQuery.Text(Request, "area");
        var level = ListQuery.Text(Request, "level");
        var source = ListQuery.Text(Request, "source");
        var checkedBy = ListQuery.Text(Request, "check");
        var sort = ListQuery.Text(Request, "sort") ?? "id";
        var descending = ListQuery.Descending(Request);

        IEnumerable<Rule> rules = design.Rules;
        if (q is not null)
        {
            rules = rules.Where(r => r.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Statement.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (r.Why?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (area is not null)
        {
            rules = rules.Where(r => r.Area == area);
        }

        if (level is not null)
        {
            rules = rules.Where(r => r.Level.ToString() == level);
        }

        if (source is not null)
        {
            rules = rules.Where(r => r.Source == source);
        }

        if (checkedBy is not null)
        {
            rules = rules.Where(r => CheckedBy(r) == checkedBy);
        }

        var matching = rules.ToList();
        IEnumerable<Rule> ordered = sort switch
        {
            "title" => matching.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase),
            "area" => matching.OrderBy(r => RuleBook.Areas.Select(a => a.Id).ToList().IndexOf(r.Area)).ThenBy(r => r.Id),
            "level" => matching.OrderBy(r => r.Level).ThenBy(r => r.Id),
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
            Id = "rules",
            Action = "/Rules",
            Label = "Rules",
            Search = q,
            SearchPlaceholder = "Id, title or text, like SHELL or toolbar",
            SearchField = true,
            Noun = "rules",
            Total = design.Rules.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "id",
            SortOptions =
            [
                new("id", "Rule id (the order of the book)"),
                new("title", "Title"),
                new("area", "Area"),
                new("level", "Level (Must first)"),
            ],
            Filters =
            [
                new("area", "Area", RuleBook.Areas.Select(a => new FilterOption(a.Id, a.Name)).ToList(), area),
                new("level", "Level", Enum.GetValues<RuleLevel>().Select(l => new FilterOption(l.ToString(), l.ToString())).ToList(), level),
                new("source", "Source", design.Rules.Select(r => r.Source).Distinct().Order().Select(s => new FilterOption(s, s)).ToList(), source),
                new("check", "Checked by", [new("Test", "A test"), new("Report", "The palette report"), new("Review", "A reviewer")], checkedBy),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
