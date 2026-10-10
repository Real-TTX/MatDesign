using System.Globalization;
using System.Web;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages;

/// <summary>
/// A sample app of the family, drawn with the real shell and the real controls: it is what the preview frames of the site show.
/// <c>?app=matmail&amp;theme=dark&amp;accent=%23ff6600&amp;view=list</c>. The search, filter and sort dialogs of the list work.
/// </summary>
public class PreviewModel : PageModel
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

    public string Query(string view) => $"/Preview?{Base()}&view={view}";

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

        return string.Join("&", parts);
    }

    public void OnGet()
    {
        App = AppRegistry.Get(Request.Query["app"].ToString()) ?? AppRegistry.Get("matdesign")!;
        View = Request.Query["view"].ToString() is "list" or "gallery" or "form" or "dashboard" or "settings" or "auth" ? Request.Query["view"].ToString() : "list";
        var theme = Request.Query["theme"].ToString() is "light" or "dark" ? Request.Query["theme"].ToString() : null;
        var accent = Request.Query["accent"].ToString();
        var customAccent = accent.Length == 7 && accent[0] == '#' && accent[1..].All(Uri.IsHexDigit) ? accent : null;
        var active = View == "auth" ? string.Empty : Query(View);

        Shell = new ShellModel
        {
            App = App,
            Tagline = App.Tagline,
            Theme = theme,
            CustomAccent = customAccent,
            PreviewQuery = Base(),
            ActiveHref = active,
            Nav =
            [
                new(null, [new("Dashboard", Query("dashboard"), "dashboard")]),
                new("Work",
                [
                    new("Items", Query("list"), "list"),
                    new("Gallery", Query("gallery"), "grid"),
                    new("New item", Query("form"), "plus"),
                ]),
                new("Account",
                [
                    new("Settings", Query("settings"), "settings"),
                    new("Sign in page", Query("auth"), "login"),
                ]),
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
