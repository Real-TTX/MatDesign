using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Foundations;

/// <summary>The icon sprite as a gallery: search, filter by group and sort in the toolbar, a click copies the markup.</summary>
public class IconsModel : PageModel
{
    private const int PageSize = 48;

    private static readonly Dictionary<string, string> Groups = BuildGroups();

    private readonly ToolkitFiles _toolkit;

    public IconsModel(ToolkitFiles toolkit)
    {
        _toolkit = toolkit;
    }

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<string> Items { get; private set; } = [];

    public int Count => _toolkit.IconNames.Count;

    /// <summary>The markup an app puts on a page to show the icon (the sprite is inline in the page).</summary>
    public static string Markup(string name)
        => $"<svg class=\"mat-icon\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{name}\"></use></svg>";

    public static string GroupOf(string name) => Groups.GetValueOrDefault(name, "Other");

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var group = ListQuery.Text(Request, "group");
        var sort = ListQuery.Text(Request, "sort") ?? "name";
        var descending = ListQuery.Descending(Request);

        IEnumerable<string> icons = _toolkit.IconNames;
        if (q is not null)
        {
            icons = icons.Where(n => n.Contains(q.Replace(' ', '-'), StringComparison.OrdinalIgnoreCase) || GroupOf(n).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (group is not null)
        {
            icons = icons.Where(n => GroupOf(n) == group);
        }

        var matching = icons.ToList();
        var ordered = (sort == "group" ? matching.OrderBy(GroupOf).ThenBy(n => n, StringComparer.Ordinal) : matching.OrderBy(n => n, StringComparer.Ordinal)).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "icons",
            Action = "/Foundations/Icons",
            Label = "Icons",
            Search = q,
            SearchPlaceholder = "An icon name, for example arrow",
            Noun = "icons",
            Total = _toolkit.IconNames.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "name",
            SortOptions = [new("name", "Name"), new("group", "Group")],
            Filters = [new("group", "Group", GroupNames().Select(g => new FilterOption(g, g)).ToList(), group)],
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }

    public static IEnumerable<string> GroupNames() => Groups.Values.Distinct().Order().Append("Other");

    private static Dictionary<string, string> BuildGroups()
    {
        var map = new Dictionary<string, string>();
        void Add(string group, string names)
        {
            foreach (var n in names.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                map[n] = group;
            }
        }

        Add("Navigation", "arrow-down arrow-right arrow-up back chevron-down chevron-left chevron-right chevron-up compass external home menu more-horizontal more-vertical route");
        Add("Actions", "broom check copy download edit eye eye-off filter link minus pause play plus refresh restore save search sort stop trash upload wrench x");
        Add("Status", "alert bell check-circle check-square help-circle info sparkles x-circle");
        Add("Account", "key lock login logout shield user user-circle users");
        Add("Objects", "archive book box briefcase calendar clock cloud dollar file file-text folder gamepad image mail message-square rocket star store tag");
        Add("Interface", "accessibility apps dashboard grid layers list moon palette settings sidebar sliders sun table template toggle type volume");
        Add("Technology", "activity bar-chart code database git globe hard-drive monitor network plug registry server smartphone terminal tv whale");
        return map;
    }
}
