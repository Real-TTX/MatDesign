using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio.Users;

/// <summary>The people of the Studio (administrators only): toolbar above (search, filter, sort as dialogs), table, pagination, and the action to add a person below.</summary>
[RequireCapability(Capability.ManageUsers)]
public class IndexModel(StudioService studio) : StudioPageModel(studio)
{
    private const int PageSize = 20;

    public ToolbarModel Toolbar { get; private set; } = null!;

    public PagerModel Pager { get; private set; } = null!;

    public IReadOnlyList<StudioUser> Items { get; private set; } = [];

    /// <summary>The state of an account in a word: the account itself, then what is waiting.</summary>
    public static string State(StudioUser user) => user.Disabled ? "Disabled" : user.MustChange ? "Must change password" : "Active";

    public static string StatePill(StudioUser user) => user.Disabled ? "mat-pill--error" : user.MustChange ? "mat-pill--warn" : "mat-pill--ok";

    public static string RolePill(StudioUser user) => user.Role == StudioRole.AdministratorId ? "mat-pill--info" : "mat-pill--plain";

    public static string RoleOf(StudioUser user) => user.RoleInfo?.Name ?? user.Role;

    public void OnGet()
    {
        var q = ListQuery.Text(Request, "q");
        var role = ListQuery.Text(Request, "role");
        var state = ListQuery.Text(Request, "state");
        var sort = ListQuery.Text(Request, "sort") ?? "name";
        var descending = ListQuery.Descending(Request);

        var everybody = Studio.Users.All;
        IEnumerable<StudioUser> rows = everybody;
        if (q is not null)
        {
            rows = rows.Where(u => u.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (role is not null)
        {
            rows = rows.Where(u => u.Role == role);
        }

        if (state is not null)
        {
            rows = rows.Where(u => State(u) == state);
        }

        var matching = rows.ToList();
        var ordering = (sort switch
        {
            "role" => matching.OrderBy(u => StudioRole.All.ToList().FindIndex(r => r.Id == u.Role)).ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase),
            "signin" => matching.OrderByDescending(u => u.LastSignIn ?? DateTimeOffset.MinValue).ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase),
            _ => matching.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase),
        }).ToList();
        if (descending)
        {
            ordering.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordering.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordering, page, PageSize).ToList();

        Toolbar = new ToolbarModel
        {
            Id = "users",
            Action = "/Studio/Users",
            Label = "People",
            Search = q,
            SearchPlaceholder = "A name",
            Noun = "people",
            Total = everybody.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "name",
            SortOptions = [new("name", "Name"), new("role", "Role (most permission first)"), new("signin", "Last sign-in (newest first)")],
            Filters =
            [
                new("role", "Role", StudioRole.All.Select(r => new FilterOption(r.Id, r.Name)).ToList(), role),
                new("state", "State", new[] { "Active", "Must change password", "Disabled" }.Select(s => new FilterOption(s, s)).ToList(), state),
            ],
        };

        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }
}
