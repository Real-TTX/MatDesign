using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio.Rules;

/// <summary>The rulebook as the Studio keeps it: toolbar above (search, filter, sort as dialogs), table, pagination, and the action to add a rule below.</summary>
public class IndexModel(StudioService studio, DesignData design) : StudioPageModel(studio)
{
    private const int PageSize = 25;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<Rule> Items { get; private set; } = [];

    public ItemSource SourceOf(Rule rule) => design.RuleSourceOf(rule.Id);

    public static string Label(ItemSource source) => source switch { ItemSource.Added => "Added", ItemSource.Changed => "Changed", _ => "Built in" };

    public static string PillClass(ItemSource source) => source switch { ItemSource.Added => "mat-pill--ok", ItemSource.Changed => "mat-pill--info", _ => string.Empty };

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var area = ListQuery.Text(Request, "area");
        var level = ListQuery.Text(Request, "level");
        var source = ListQuery.Text(Request, "source");
        var sort = ListQuery.Text(Request, "sort") ?? "id";
        var descending = ListQuery.Descending(Request);

        IEnumerable<Rule> rules = design.Rules;
        if (q is not null)
        {
            rules = rules.Where(r => r.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || r.Statement.Contains(q, StringComparison.OrdinalIgnoreCase));
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
            rules = rules.Where(r => SourceOf(r).ToString() == source);
        }

        var matching = rules.ToList();
        IEnumerable<Rule> ordered = sort switch
        {
            "title" => matching.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase),
            "area" => matching.OrderBy(r => RuleBook.Areas.Select(a => a.Id).ToList().IndexOf(r.Area)).ThenBy(r => r.Id, StringComparer.Ordinal),
            "level" => matching.OrderBy(r => r.Level).ThenBy(r => r.Id, StringComparer.Ordinal),
            "source" => matching.OrderByDescending(r => SourceOf(r) != ItemSource.BuiltIn).ThenBy(r => r.Id, StringComparer.Ordinal),
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
            Action = "/Studio/Rules",
            Label = "Rules",
            Search = q,
            SearchPlaceholder = "An id, a title or a word of the rule",
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
                new("source", "Source (Studio first)"),
            ],
            Filters =
            [
                new("area", "Area", RuleBook.Areas.Select(a => new FilterOption(a.Id, a.Name)).ToList(), area),
                new("level", "Level", Enum.GetValues<RuleLevel>().Select(l => new FilterOption(l.ToString(), l.ToString())).ToList(), level),
                new("source", "Source", [new("BuiltIn", "Built in"), new("Added", "Added in the Studio"), new("Changed", "Built in, changed in the Studio")], source),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
