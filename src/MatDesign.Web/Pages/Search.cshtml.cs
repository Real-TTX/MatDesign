using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages;

/// <summary>
/// The results of the search of the site (<c>/Search?q=&amp;kind=&amp;page=</c>): the page that the search field of every header bar sends its form to. Without a kind the results are grouped by kind, the best
/// few of each with a link to all of them; with a kind they are one list of that kind, in pages. A list page like the others: the toolbar above it with the search as a field and the kind as the filter,
/// the pager under it.
/// </summary>
public class SearchModel(SearchIndex index) : PageModel
{
    public const int PageSize = 20;
    public const int PerKind = 5;

    /// <summary>What the page suggests when there is no query or nothing was found: a rule id, a component, a token.</summary>
    public static IReadOnlyList<string> Examples { get; } = ["FORM-15", "button", "--mat-accent"];

    public string Query { get; private set; } = string.Empty;

    public SearchKind? Kind { get; private set; }

    public SearchResults Results { get; private set; } = SearchResults.Empty;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    /// <summary>Without a kind: the best few hits of each kind, the kind of the best hit first.</summary>
    public IReadOnlyList<SearchGroup> Groups { get; private set; } = [];

    /// <summary>With a kind: the hits of the page.</summary>
    public IReadOnlyList<SearchHit> Hits { get; private set; } = [];

    /// <summary>How many hits match the query and the chosen kind.</summary>
    public int Matching { get; private set; }

    public bool Asked => Query.Length > 0;

    public void OnGet()
    {
        Query = SearchIndex.Normalise(ListQuery.Text(Request, "q"));
        Kind = SearchKinds.TryParse(ListQuery.Text(Request, "kind"), out var kind) ? kind : null;
        Results = index.Query(Query);
        Matching = Kind is { } chosen ? Results.CountOf(chosen) : Results.Total;

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(Matching / (double)PageSize)));
        if (Kind is { } k)
        {
            Hits = Results.Of(k).Skip((page - 1) * PageSize).Take(PageSize).ToList();
        }
        else
        {
            Groups = Results.Top(int.MaxValue).Select(g => g with { Hits = g.Hits.Take(PerKind).ToList() }).ToList();
        }

        Toolbar = new ToolbarModel
        {
            Id = "results",
            Action = "/Search",
            Label = "Search results",
            Search = Query.Length == 0 ? null : Query,
            SearchPlaceholder = "A rule id, a component, a token, an icon",
            SearchField = true,
            Noun = "results",
            Total = Results.Total,
            Matching = Matching,
            Filters =
            [
                new("kind", "Kind", SearchKinds.All.Where(x => Results.CountOf(x) > 0 || x == Kind).Select(x => new FilterOption(SearchKinds.Name(x), $"{SearchKinds.Heading(x)} ({Results.CountOf(x)})")).ToList(), Kind is { } selected ? SearchKinds.Name(selected) : null),
            ],
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = Kind is null ? 0 : Matching, Url = Toolbar.UrlWithPage };
    }
}
