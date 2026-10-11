using System.Globalization;
using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Web.Services;

/// <summary>
/// A badge at the right end of a menu item (SHELL-14): a count, or a dot for "something new" (empty text). <see cref="Tone"/> is one of
/// accent, ok, warn, error, info, plain and follows the meaning of the status colours (COLOR-03). <see cref="Label"/> is what a screen reader says ("3 unread").
/// </summary>
public sealed record NavBadge(string Text, string Tone = "accent", string? Label = null)
{
    private static readonly string[] Severity = ["error", "warn", "info", "accent", "ok", "plain"];

    public bool IsDot => Text.Length == 0;

    /// <summary>A count; above 99 it reads "99+".</summary>
    public static NavBadge Count(int number, string tone = "accent", string? label = null)
        => new(number > 99 ? "99+" : number.ToString(CultureInfo.InvariantCulture), tone, label);

    public static NavBadge Dot(string tone = "accent", string? label = null) => new(string.Empty, tone, label);

    /// <summary>The classes of the badge; <paramref name="sum"/> marks the stand-in of a closed parent for its children's badges.</summary>
    public string Classes(bool sum = false)
        => "mat-nav__badge" + (Tone == "accent" ? string.Empty : $" mat-nav__badge--{Tone}") + (IsDot ? " mat-nav__badge--dot" : string.Empty) + (sum ? " mat-nav__badge--sum" : string.Empty);

    /// <summary>The badge of a closed parent: the sum of its children's counts, or a dot of the most urgent tone when they are not all counts.</summary>
    public static NavBadge? Sum(IEnumerable<NavBadge> badges)
    {
        var list = badges.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var tone = Severity.First(t => list.Any(b => b.Tone == t));
        var counts = list.Where(b => !b.IsDot && int.TryParse(b.Text.TrimEnd('+'), NumberStyles.None, CultureInfo.InvariantCulture, out _)).ToList();
        if (counts.Count == list.Count)
        {
            return Count(counts.Sum(b => int.Parse(b.Text.TrimEnd('+'), CultureInfo.InvariantCulture)), tone, string.Join(", ", list.Select(b => b.Label).Where(l => l is not null)));
        }

        return Dot(tone, string.Join(", ", list.Select(b => b.Label).Where(l => l is not null)));
    }
}

/// <summary>
/// An item of the menu. With <see cref="Children"/> it is the parent of a submenu (SHELL-13): not a page, a button that opens its children, one level deep.
/// A parent has no <see cref="Href"/>; if the section has a page of its own, that page is its first child. Children show no icon.
/// </summary>
public sealed record NavItem(string Title, string? Href, string? Icon, NavBadge? Badge = null, IReadOnlyList<NavItem>? Children = null)
{
    public bool IsParent => Children is { Count: > 0 };

    /// <summary>A stable key for remembering whether the user opened the branch.</summary>
    public string Key => Regex.Replace(Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    public bool Contains(string? href) => href is not null && (Href == href || (Children?.Any(c => c.Href == href) ?? false));

    /// <summary>What a closed parent shows when it has no badge of its own: the sum of its children's badges.</summary>
    public NavBadge? SumOfChildren => IsParent && Badge is null ? NavBadge.Sum(Children!.Where(c => c.Badge is not null).Select(c => c.Badge!)) : null;
}

public sealed record NavGroup(string? Title, IReadOnlyList<NavItem> Items);

/// <summary>
/// One menu of an app (SHELL-15). An app with distinct areas, such as an administration, has several; the switcher under the logo shows one at a time.
/// </summary>
public sealed record NavSet(string Id, string Title, string Icon, IReadOnlyList<NavGroup> Groups)
{
    /// <summary>Every item, parents included.</summary>
    public IEnumerable<NavItem> AllItems => Groups.SelectMany(g => g.Items).SelectMany(i => i.IsParent ? i.Children!.Prepend(i) : [i]);

    /// <summary>The pages of the menu (parents are not pages).</summary>
    public IEnumerable<NavItem> Pages => AllItems.Where(i => !i.IsParent && i.Href is not null);

    public bool Contains(string? href) => href is not null && Pages.Any(i => i.Href == href);

    /// <summary>True when an item of this menu asks for attention (a warning or error badge): the switcher then shows a dot while another menu is shown.</summary>
    public bool NeedsAttention => AllItems.Any(i => i.Badge is { Tone: "warn" or "error" });

    /// <summary>A menu has at most one level of submenus.</summary>
    public bool IsValid => Groups.SelectMany(g => g.Items).All(i => !i.IsParent || i.Children!.All(c => !c.IsParent && c.Href is not null));
}

/// <summary>What the menu counts: the apps (the palette's badge counts the pairs of accents that are too close), the rules and the components of the components page.</summary>
public sealed record NavFacts(IReadOnlyList<MatApp> Apps, int Rules, int Components)
{
    /// <summary>The built-in data, before the Studio has changed anything.</summary>
    public static NavFacts BuiltIn { get; } = new(AppRegistry.All, RuleBook.All.Count, ComponentCatalog.In(ComponentCatalog.Sections[0]).Count());
}

/// <summary>The menus of the site. The item whose address is the longest prefix of the current path is the active one.</summary>
public static class Navigation
{
    /// <summary>The two menus of the site as the built-in data gives them. The pages use <see cref="Create"/> with the data the Studio changed.</summary>
    public static IReadOnlyList<NavSet> Sets { get; } = Create(NavFacts.BuiltIn);

    /// <summary>The two menus of the site with the counts of <paramref name="facts"/>.</summary>
    public static IReadOnlyList<NavSet> Create(NavFacts facts) =>
    [
        new("design", "Design", "palette",
        [
            new(null, [new("Overview", "/", "home")]),
            new("Identity",
            [
                new("Apps", "/Identity", "apps"),
                new("Palette", "/Identity/Palette", "palette", NavBadge.Count(PaletteAdvisor.Collisions(facts.Apps).Count, "warn", "pairs of accents that are too close")),
                new("Audit", "/Identity/Audit", "check-square"),
            ]),
            new("Rules", [new("Rulebook", "/Rules", "book", NavBadge.Count(facts.Rules, "plain", "rules"))]),
            new("Foundations",
            [
                new("Foundations", null, "layers", Children:
                [
                    new("Colour", "/Foundations", null),
                    new("Type", "/Foundations/Type", null),
                    new("Space and shape", "/Foundations/Space", null),
                    new("Motion", "/Foundations/Motion", null),
                    new("Icons", "/Foundations/Icons", null),
                    new("All tokens", "/Foundations/Tokens", null),
                ]),
            ]),
        ]),
        new("toolkit", "Toolkit", "toggle",
        [
            new(null, [new("Shell", "/Shell", "sidebar")]),
            new("Build with it",
            [
                new("Components", "/Components", "toggle", NavBadge.Count(facts.Components, "plain", "components")),
                new("Layout", "/Layout", "grid"),
                new("Utilities", "/Utilities", "sliders"),
                new("Patterns", "/Patterns", "template"),
                new("Builder", "/Builder", "wrench"),
            ]),
            new("Use it", [new("Download", "/Download", "download")]),
        ]),
    ];

    /// <summary>
    /// The menu of the Studio (docs/STUDIO.md). It is a third menu behind the switcher, shown only to the signed-in administrator, so it is not one of <see cref="Sets"/>.
    /// </summary>
    public static NavSet StudioSet { get; } = new("studio", "Studio", "wrench",
    [
        new(null, [new("Overview", "/Studio", "home")]),
        new("Maintain",
        [
            new("Apps", "/Studio/Apps", "apps"),
            new("Rules", "/Studio/Rules", "book"),
            new("Components", "/Studio/Components", "toggle"),
            new("Activity", "/Studio/Activity", "activity"),
        ]),
    ]);

    /// <summary>The menus a visitor sees: the two of the site, and the Studio for the signed-in administrator.</summary>
    public static IReadOnlyList<NavSet> For(NavFacts facts, bool studio) => studio ? [.. Create(facts), StudioSet] : Create(facts);

    /// <summary>Every page of every menu (no parents), the Studio's included.</summary>
    public static IEnumerable<NavItem> Pages => Sets.Append(StudioSet).SelectMany(s => s.Pages);

    /// <summary>
    /// The page of the menu that belongs to <paramref name="path"/>: an address that is the same, or the longest one the path continues
    /// (<c>/Foundations/Type</c> is its own item, <c>/Identity/App</c> belongs to <c>/Identity</c>). A menu address with a query (<c>?group=Forms</c>) must match the query too.
    /// </summary>
    public static string? ActiveHref(string path, string? query = null) => ActiveHref(Pages.Select(i => i.Href!), path, query);

    /// <summary>The same for any list of menu addresses.</summary>
    public static string? ActiveHref(IEnumerable<string> hrefs, string path, string? query = null)
    {
        var parameters = System.Web.HttpUtility.ParseQueryString(query ?? string.Empty);
        return hrefs
            .Select(h => (Href: h, Path: h.Split('?')[0], Query: h.Contains('?') ? System.Web.HttpUtility.ParseQueryString(h[(h.IndexOf('?') + 1)..]) : null))
            .Where(x => x.Path == "/" ? path == "/" : path.Equals(x.Path, StringComparison.OrdinalIgnoreCase) || path.StartsWith(x.Path + "/", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Query is null || x.Query.AllKeys.All(k => k is not null && x.Query[k] == parameters[k]))
            .OrderByDescending(x => x.Path.Length + (x.Query is null ? 0 : 1000))
            .Select(x => x.Href)
            .FirstOrDefault();
    }
}
