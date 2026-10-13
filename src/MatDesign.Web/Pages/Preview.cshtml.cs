using System.Globalization;
using System.Web;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages;

/// <summary>
/// A sample app of the family, drawn with the real shell and the real controls: it is what the preview frames of the site show.
/// <c>?app=matmail&amp;theme=dark&amp;accent=%23ff6600&amp;view=list</c>. The search, filter and sort dialogs of the list work.
/// <c>&amp;action=1</c> adds a main action to the menu (SHELL-19) and <c>&amp;switcher=end</c> puts the switcher at the end of the menu (SHELL-15), <c>&amp;tenants=1</c> adds the tenants as choices to its popup.
/// </summary>
public class PreviewModel(MatDesign.Web.Studio.DesignData design) : PageModel
{
    private const int PageSize = 8;

    public sealed record Sample(string Name, string Detail, string Status, string Tone, DateOnly Date, int Size);

    private static readonly IReadOnlyList<Sample> Samples =
    [
        new("Quarterly report", "Finance", "Done", "ok", new(2026, 9, 28), 412),
        new("Insurance policy", "Contracts", "Review", "warn", new(2026, 9, 21), 188),
        new("Holiday photos", "Family", "Done", "ok", new(2026, 9, 14), 5230),
        new("Tax return 2025", "Finance", "Failed", "error", new(2026, 9, 9), 96),
        new("Router backup", "Network", "Done", "ok", new(2026, 9, 2), 14),
        new("Lease agreement", "Contracts", "Review", "warn", new(2026, 8, 30), 240),
        new("Invoice 2026-04", "Finance", "Done", "ok", new(2026, 8, 24), 64),
        new("Meeting notes", "Work", "Done", "ok", new(2026, 8, 19), 22),
        new("Server certificate", "Network", "Failed", "error", new(2026, 8, 11), 4),
        new("Warranty card", "Family", "Review", "warn", new(2026, 8, 3), 38),
        new("Project plan", "Work", "Done", "ok", new(2026, 7, 29), 310),
        new("Passport scan", "Family", "Done", "ok", new(2026, 7, 17), 1480),
    ];

    public ShellModel Shell { get; private set; } = null!;

    public string View { get; private set; } = "list";

    public IReadOnlyList<Sample> Items { get; private set; } = [];

    public ToolbarModel? Toolbar { get; private set; }

    public PagerModel? Pager { get; private set; }

    public MatApp App { get; private set; } = null!;

    /// <summary>The address of a page of the sample app. An <paramref name="area"/> tells two menu items apart that show the same kind of page.</summary>
    public string Query(string view, string? area = null) => $"/Preview?{Base()}&view={view}" + (area is null ? string.Empty : $"&area={area}");

    /// <summary>The areas of the sample's menus: the title of the page, its icon and the breadcrumb above it.</summary>
    private static readonly Dictionary<string, (string Title, string Icon, string[] Trail)> Areas = new()
    {
        ["review"] = ("Needs review", "alert", ["Work", "Items"]),
        ["users"] = ("Users", "users", ["Administration"]),
        ["roles"] = ("Roles", "shield", ["Administration"]),
        ["general"] = ("General", "settings", ["Administration", "Settings"]),
        ["security"] = ("Security", "lock", ["Administration", "Settings"]),
        ["backups"] = ("Backups", "archive", ["Administration", "Settings"]),
        ["audit"] = ("Audit log", "clock", ["Administration"]),
    };

    public string? Area { get; private set; }

    /// <summary>
    /// Where the sample draws its switcher: <c>?switcher=bottom</c> at the foot above the user, <c>both</c> at both places, <c>end</c> at the end of the menu,
    /// otherwise under the logo.
    /// </summary>
    private SwitcherPlace Place => Request.Query["switcher"].ToString() switch { "bottom" => SwitcherPlace.Bottom, "both" => SwitcherPlace.Both, "end" => SwitcherPlace.End, _ => SwitcherPlace.Top };

    /// <summary><c>?menus=tree</c>: the administration is two menus below one parent in the switcher.</summary>
    private bool Tree => Request.Query["menus"].ToString() == "tree";

    /// <summary><c>?tenants=1</c>: the switcher offers the tenants as choices between the menus and the administration (SHELL-15).</summary>
    private bool Tenants => Request.Query["tenants"].ToString() == "1";

    /// <summary>The page of an area (a menu item that is not one of the basic views), or null.</summary>
    public (string Title, string Icon, IReadOnlyList<(string Text, string? Url)> Crumbs)? AreaPage
        => Area is not null && Areas.TryGetValue(Area, out var a)
            ? (a.Title, a.Icon, [.. a.Trail.Select(t => (t, (string?)Query("dashboard"))), (a.Title, null)])
            : null;

    private string Base()
    {
        var parts = new List<string> { $"app={App.Id}" };
        if (Request.Query["theme"].ToString() is { Length: > 0 } theme)
        {
            parts.Add($"theme={HttpUtility.UrlEncode(theme)}");
        }

        if (Request.Query["accent"].ToString() is { Length: > 0 } accent)
        {
            parts.Add($"accent={HttpUtility.UrlEncode(accent)}");
        }

        if (ColorSchemes.Normalize(Request.Query["scheme"].ToString()) is { } scheme)
        {
            parts.Add($"scheme={scheme}");
        }

        if (Place != SwitcherPlace.Top)
        {
            parts.Add($"switcher={Place.ToString().ToLowerInvariant()}");
        }

        if (Tree)
        {
            parts.Add("menus=tree");
        }

        if (Tenants)
        {
            parts.Add("tenants=1");
        }

        return string.Join("&", parts);
    }

    public void OnGet()
    {
        App = design.App(Request.Query["app"].ToString()) ?? design.App("matdesign")!;
        View = Request.Query["view"].ToString() is "list" or "gallery" or "form" or "dashboard" or "settings" or "auth" ? Request.Query["view"].ToString() : "list";
        var theme = Request.Query["theme"].ToString() is "light" or "dark" ? Request.Query["theme"].ToString() : null;
        var accent = Request.Query["accent"].ToString();
        var customAccent = accent.Length == 7 && accent[0] == '#' && accent[1..].All(Uri.IsHexDigit) ? accent : null;
        Area = Request.Query["area"].ToString() is { Length: > 0 } requested && Areas.ContainsKey(requested) ? requested : null;
        var active = View == "auth" ? string.Empty : Query(View, Area);
        // ?action=1 draws the main action (SHELL-19) as the first item of the menu, in place of "New item"; ?switcher=end puts the switcher at the end of the menu (SHELL-15).
        var action = Request.Query["action"].ToString() == "1";
        List<NavItem> itemPages =
        [
            new("All items", Query("list"), null, NavBadge.Count(12, "plain", "items")),
            new("Needs review", Query("list", "review"), null, NavBadge.Count(3, "warn", "to review")),
            new("Gallery", Query("gallery"), null),
        ];
        if (!action)
        {
            itemPages.Add(new("New item", Query("form"), null));
        }

        List<NavItem> start = [new("Dashboard", Query("dashboard"), "dashboard")];
        if (action)
        {
            start.Insert(0, new("Add item", Query("form"), "plus", Primary: true));
        }

        // The menu of the sample: a submenu with badges, and an administration behind the switcher. With ?menus=tree the administration is two menus below one
        // parent in the switcher (SHELL-15); ?switcher=bottom|both puts the switcher at the foot of the menu, above the user.
        var users = new NavItem("Users", Query("list", "users"), "users", NavBadge.Count(3, "warn", "invitations pending"));
        var roles = new NavItem("Roles", Query("gallery", "roles"), "shield");
        var settings = new NavItem("Settings", null, "settings", Children:
        [
            new("General", Query("settings", "general"), null),
            new("Security", Query("form", "security"), null),
            new("Backups", Query("dashboard", "backups"), null, NavBadge.Count(1, "error", "failed")),
        ]);
        var audit = new NavItem("Audit log", Query("list", "audit"), "clock");
        var administration = new NavSetParent("Administration", "shield");
        IReadOnlyList<NavSet> admin = Tree
            ?
            [
                new("people", "People", "users", [new(null, [users, roles])], administration, Last: true),
                new("system", "System", "settings", [new(null, [settings, audit])], administration, Last: true),
            ]
            :
            [
                new("admin", "Administration", "shield", [new(null, [users, roles]), new("System", [settings, audit])], Last: true),
            ];

        Shell = new ShellModel
        {
            App = App,
            Tagline = App.Tagline,
            Theme = theme,
            Scheme = ColorSchemes.Normalize(Request.Query["scheme"].ToString()),
            CustomAccent = customAccent,
            PreviewQuery = Base(),
            ActiveHref = active,
            SwitcherPlace = Place,
            Choices = Tenants
                ? [new SwitcherChoices("Tenants", "store", [new("Muster GmbH", "/Preview", true), new("Beispiel AG", "/Preview"), new("Dritte KG", "/Preview")])]
                : [],
            Menus =
            [
                new("work", "Work", "briefcase",
                [
                    new(null, start),
                    new("Work", [new("Items", null, "list", Children: itemPages)]),
                    new("Account",
                    [
                        new("Settings", Query("settings"), "settings"),
                        new("Sign in page", Query("auth"), "login"),
                    ]),
                ]),
                .. admin,
            ],
            UserName = "Alex Morgan",
            UserRole = "Administrator",
            UserLine = "alex@example.com",
            UserMenu =
            [
                new("Show account", Query("form"), "user"),
                new("Settings", Query("settings"), "settings"),
                new("Sign out", Query("auth"), "logout", SeparatorBefore: true),
            ],
            Version = $"{App.Name} 1.4.212-20261010",
        };

        if (View is "list" or "gallery")
        {
            BuildList();
        }
    }

    private void BuildList()
    {
        var q = ListQuery.Text(Request, "q");
        var category = ListQuery.Text(Request, "category");
        var status = ListQuery.Text(Request, "status");
        var sort = ListQuery.Text(Request, "sort") ?? "date";
        var descending = Request.Query.ContainsKey("sort") ? ListQuery.Descending(Request) : true;

        IEnumerable<Sample> items = Samples;
        if (q is not null)
        {
            items = items.Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || s.Detail.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (category is not null)
        {
            items = items.Where(s => s.Detail == category);
        }

        if (status is not null)
        {
            items = items.Where(s => s.Status == status);
        }

        var matching = items.ToList();
        var ordered = (sort switch
        {
            "name" => matching.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase),
            "size" => matching.OrderBy(s => s.Size),
            _ => matching.OrderBy(s => s.Date),
        }).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        var page = Math.Min(ListQuery.Page(Request), Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)PageSize)));
        Items = ListQuery.Slice(ordered, page, PageSize).ToList();

        var keep = new Dictionary<string, string?>
        {
            ["app"] = App.Id,
            ["theme"] = Request.Query["theme"].ToString(),
            ["accent"] = Request.Query["accent"].ToString(),
            ["scheme"] = ColorSchemes.Normalize(Request.Query["scheme"].ToString()) ?? string.Empty,
            ["switcher"] = Place == SwitcherPlace.Top ? null : Place.ToString().ToLowerInvariant(),
            ["menus"] = Tree ? "tree" : null,
            ["tenants"] = Tenants ? "1" : null,
            ["view"] = View,
        };

        Toolbar = new ToolbarModel
        {
            Id = "sample",
            Action = "/Preview",
            Label = "Items",
            Search = q,
            SearchPlaceholder = "Name or category",
            Noun = "items",
            Total = Samples.Count,
            Matching = matching.Count,
            Sort = sort,
            Descending = descending,
            DefaultSort = "date",
            SortOptions = [new("date", "Date"), new("name", "Name"), new("size", "Size")],
            Filters =
            [
                new("category", "Category", Samples.Select(s => s.Detail).Distinct().Order().Select(c => new FilterOption(c, c)).ToList(), category),
                new("status", "Status", [new("Done", "Done"), new("Review", "Review"), new("Failed", "Failed")], status),
            ],
            Keep = keep,
        };
        Pager = new PagerModel { Page = page, PageSize = PageSize, Total = matching.Count, Url = Toolbar.UrlWithPage };
    }

    public static string Size(int kb) => kb >= 1024 ? $"{kb / 1024.0:0.#} MB" : $"{kb.ToString(CultureInfo.InvariantCulture)} KB";
}
