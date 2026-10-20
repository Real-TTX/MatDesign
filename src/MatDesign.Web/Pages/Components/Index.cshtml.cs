using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.ComponentPages;

/// <summary>
/// The overview of the components (<c>/Components</c>, LIST-18): a tile for each component under the heading of its group, nothing drawn live, so that the page is light.
/// The search and the group filter work without the script as a GET form (the tiles that do not match are in the page, <c>hidden</c>); <c>wwwroot/js/components.js</c> filters
/// while the person types, with the same text.
/// </summary>
public class IndexModel(DesignData design, ToolkitFiles toolkit) : PageModel
{
    public ToolbarModel Toolbar { get; private set; } = null!;

    public IReadOnlyList<TileGroup> Groups { get; private set; } = [];

    public int Total { get; private set; }

    public int Matching { get; private set; }

    public string? Query { get; private set; }

    /// <summary>The group the page is filtered to (a group of the page), or null.</summary>
    public string? Group { get; private set; }

    public void OnGet()
    {
        var section = ComponentCatalog.Sections.Single(s => s.Id == "components");
        var q = ListQuery.Text(Request, "q");
        var all = design.ComponentsIn(section).ToList();
        var present = section.Groups.Where(g => all.Any(c => c.Group == g)).ToList();
        // a group that is not one of the page is no filter (an old link, a typo): the whole list is shown
        var group = ListQuery.Text(Request, "group") is { } asked && present.Contains(asked) ? asked : null;

        Query = q;
        Group = group;
        Groups = ComponentOverview.Build(all, section, toolkit.Properties, q, group);
        Total = all.Count;
        Matching = Groups.Sum(g => g.VisibleCount);

        Toolbar = new ToolbarModel
        {
            Id = "cmp",
            Action = ComponentCatalog.OverviewPath,
            Label = "Components",
            Search = q,
            SearchPlaceholder = "Name, group or rule id",
            SearchField = true,
            Noun = "components",
            Total = Total,
            Matching = Matching,
            Filters = [new("group", "Group", present.Select(g => new FilterOption(g, g)).ToList(), group)],
        };
    }
}
