namespace MatDesign.Web.Services;

public sealed record NavItem(string Title, string Href, string Icon);

public sealed record NavGroup(string? Title, IReadOnlyList<NavItem> Items);

/// <summary>The menu of the site. The item whose address is the longest prefix of the current path is the active one.</summary>
public static class Navigation
{
    public static IReadOnlyList<NavGroup> Groups { get; } =
    [
        new(null, [new("Overview", "/", "home")]),
        new("Identity",
        [
            new("Apps", "/Identity", "apps"),
            new("Palette", "/Identity/Palette", "palette"),
            new("Audit", "/Identity/Audit", "check-square"),
        ]),
        new("Rules", [new("Rulebook", "/Rules", "book")]),
        new("Toolkit",
        [
            new("Foundations", "/Foundations", "layers"),
            new("Shell", "/Shell", "sidebar"),
            new("Components", "/Components", "toggle"),
            new("Patterns", "/Patterns", "template"),
            new("Builder", "/Builder", "wrench"),
        ]),
        new("Use it", [new("Download", "/Download", "download")]),
    ];

    public static string? ActiveHref(string path)
        => Groups.SelectMany(g => g.Items)
            .Select(i => i.Href)
            .Where(h => h == "/" ? path == "/" : path.Equals(h, StringComparison.OrdinalIgnoreCase) || path.StartsWith(h + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Length)
            .FirstOrDefault();
}
