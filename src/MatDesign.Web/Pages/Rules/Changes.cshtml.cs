using System.Globalization;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Rules;

/// <summary>One line of the changelog of the rulebook as the page shows it: the change, and the title of the rule as it is now (null for a rule that is gone).</summary>
public sealed record ChangeRow(RuleChange Change, string? Title, string? AreaName, int Index)
{
    /// <summary>True when the rule is in the rulebook, so its page exists.</summary>
    public bool Exists => Title is not null;
}

/// <summary>The lines of one month, under one heading (LIST-16).</summary>
public sealed record MonthGroup(string Key, string Title, IReadOnlyList<ChangeRow> Rows);

/// <summary>
/// What changed in the rulebook: the changelog of the rulebook (<see cref="RuleChanges"/>), newest first, grouped by month, as a list with a toolbar like every list of the site. A rule that was
/// written or changed in the Studio keeps its own history there; this is the history of the rules that came with the program.
/// </summary>
public class ChangesModel(DesignData design) : PageModel
{
    private const int PageSize = 30;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<MonthGroup> Groups { get; private set; } = [];

    /// <summary>The lines that are shown on this page (before they are grouped).</summary>
    public int Shown => Groups.Sum(g => g.Rows.Count);

    /// <summary>The kinds in the order of the filter and the sort.</summary>
    public static IReadOnlyList<RuleChangeKind> Kinds { get; } = Enum.GetValues<RuleChangeKind>();

    /// <summary>The words a reader knows a kind by.</summary>
    public static string KindLabel(RuleChangeKind kind) => kind.ToString();

    /// <summary>The class of the pill of a kind: the status colours keep their meaning (COLOR-03), the pill always carries the word (TABLE-03).</summary>
    public static string KindClass(RuleChangeKind kind) => kind switch
    {
        RuleChangeKind.Added => "mat-pill--ok",
        RuleChangeKind.Changed => "mat-pill--info",
        RuleChangeKind.Removed => "mat-pill--error",
        _ => "mat-pill--plain",
    };

    /// <summary>What a line says when it has no note of its own (a rule that was added needs none).</summary>
    public static string NoteOf(RuleChange change) => change.Note.Length > 0 ? change.Note : change.Kind switch
    {
        RuleChangeKind.Added => "The rule was added.",
        RuleChangeKind.Changed => "The rule was changed.",
        RuleChangeKind.Removed => "The rule was removed.",
        _ => "The rule has another id now.",
    };

    /// <summary>The lines as the list shows them: every line of the changelog with the rule it is about as the rulebook has it now, filtered, sorted and a page of them.</summary>
    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var area = ListQuery.Text(Request, "area");
        var kindText = ListQuery.Text(Request, "kind");
        var sort = ListQuery.Text(Request, "sort") ?? "date";
        var descending = ListQuery.Descending(Request);
        var kind = Enum.TryParse<RuleChangeKind>(kindText, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : (RuleChangeKind?)null;
        if (kind is null)
        {
            kindText = null;
        }

        var areaIds = RuleBook.Areas.Select(a => a.Id).ToList();
        if (area is not null && !areaIds.Contains(area))
        {
            area = null;
        }

        var all = RuleChanges.All.Select((c, index) => new ChangeRow(c, design.Rule(c.RuleId)?.Title, AreaOf(c.RuleId)?.Name, index)).ToList();
        IEnumerable<ChangeRow> rows = all;
        if (q is not null)
        {
            rows = rows.Where(r => r.Change.RuleId.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (r.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || NoteOf(r.Change).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (area is not null)
        {
            rows = rows.Where(r => r.Change.RuleId.StartsWith(area + "-", StringComparison.Ordinal));
        }

        if (kind is { } wanted)
        {
            rows = rows.Where(r => r.Change.Kind == wanted);
        }

        var matching = rows.ToList();
        var places = design.Rules.Select((r, i) => (r.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        int Book(string id) => places.GetValueOrDefault(id, int.MaxValue);

        // the natural order of every sort starts with the newest line; inside a day the rules stand in the order of the book and the lines of one rule the newest first
        IEnumerable<ChangeRow> ordered = sort switch
        {
            "id" => matching.OrderBy(r => Book(r.Change.RuleId)).ThenBy(r => r.Change.RuleId, StringComparer.Ordinal).ThenByDescending(r => r.Change.Date).ThenByDescending(r => r.Index),
            "kind" => matching.OrderBy(r => r.Change.Kind).ThenByDescending(r => r.Change.Date).ThenBy(r => Book(r.Change.RuleId)).ThenByDescending(r => r.Index),
            _ => matching.OrderByDescending(r => r.Change.Date).ThenBy(r => Book(r.Change.RuleId)).ThenByDescending(r => r.Index),
        };
        var ordering = ordered.ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        var pageRows = ListQuery.Slice(ordering, page, PageSize).ToList();
        Groups = pageRows
            .GroupBy(r => r.Change.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .Select(g => new MonthGroup(g.Key, g.First().Change.Date.ToString("MMMM yyyy", CultureInfo.InvariantCulture), g.ToList()))
            .ToList();

        Toolbar = new ToolbarModel
        {
            Id = "rulechanges",
            Action = "/Rules/Changes",
            Label = "Rule changes",
            Search = q,
            SearchPlaceholder = "Id, title or note, like FORM-15",
            SearchField = true,
            Noun = "changes",
            Total = all.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "date",
            SortOptions =
            [
                new("date", "Date (the newest first)"),
                new("id", "Rule id (the order of the book)"),
                new("kind", "Kind of change"),
            ],
            Filters =
            [
                new("kind", "Kind of change", Kinds.Select(k => new FilterOption(k.ToString(), KindLabel(k))).ToList(), kindText),
                new("area", "Area", RuleBook.Areas.Select(a => new FilterOption(a.Id, a.Name)).ToList(), area),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }

    private static RuleArea? AreaOf(string ruleId)
    {
        var dash = ruleId.LastIndexOf('-');
        return dash < 0 ? null : RuleBook.Areas.FirstOrDefault(a => a.Id == ruleId[..dash]);
    }
}
