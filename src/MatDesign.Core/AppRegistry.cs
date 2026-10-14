using System.Text.Json.Serialization;

namespace MatDesign.Core;

/// <summary>
/// One app of the Mat family as the design system knows it. <see cref="Accent"/> is the colour the toolkit uses (the identity colour,
/// adjusted where the original failed WCAG AA with white text). What was found in the app's own files is kept next to it, so the
/// site can show where the apps differ today.
/// </summary>
public sealed record MatApp(
    string Id,
    string Name,
    string Tagline,
    string Category,
    string Accent,
    string Glyph,
    string Repo,
    string? FoundAccent = null,
    string? LogoColor = null,
    string? OnAccent = null,
    string? AccentText = null,
    int? Port = null,
    string CssPrefix = "",
    string TokenScheme = "",
    string Notes = "")
{
    [JsonIgnore]
    public Rgb AccentRgb => Rgb.Parse(Accent);

    /// <summary>The colour of text on a filled accent surface.</summary>
    [JsonIgnore]
    public Rgb OnAccentRgb => Rgb.Parse(OnAccent ?? "#ffffff");
}

/// <summary>The apps of the family. The accents here must equal those in <c>matui.apps.css</c> (a unit test checks it).</summary>
public static class AppRegistry
{
    public const string Platform = "Platform";
    public const string Infrastructure = "Infrastructure";
    public const string Productivity = "Productivity";
    public const string Data = "Monitoring and data";
    public const string Business = "Business";
    public const string Home = "Home and fun";

    public static IReadOnlyList<string> Categories { get; } = [Platform, Infrastructure, Productivity, Data, Business, Home];

    public static IReadOnlyList<MatApp> All { get; } =
    [
        new("mattiverse", "Mattiverse", "Launcher and store for your Docker apps", Platform, "#6164ec", "sparkles", "Mattiverse",
            FoundAccent: "#6366f1", LogoColor: "#4f46e5", Port: 8030, CssPrefix: "mat-", TokenScheme: "--accent, --bg, --surface"),
        new("matos", "MatOS", "A desktop for your Docker host, in the browser", Platform, "#8558ec", "monitor", "MatOS",
            FoundAccent: "#8b5cf6", CssPrefix: "mat-, wp-, sm-", TokenScheme: "--accent plus --mat-* (two sets)"),
        new("matdesign", "MatDesign", "Design and identity of all Mat apps", Platform, "#d6336c", "palette", "MatDesign",
            Port: 8060, CssPrefix: "mat-", TokenScheme: "--mat-*",
            Notes: "This site. Its toolkit (MatUi) is the reference."),

        new("matdock", "MatDock", "Advanced Docker management", Infrastructure, "#1d78be", "whale", "MatDock",
            FoundAccent: "#2496ed", LogoColor: "#2496ed", Port: 4455, CssPrefix: "mat-", TokenScheme: "--accent, --bg, --surface"),
        new("matcad", "Matcad", "Reverse proxy with automatic HTTPS", Infrastructure, "#2f6feb", "route", "Matcad",
            FoundAccent: "#2f6feb", LogoColor: "#2f6feb", Port: 4433, CssPrefix: "(none)", TokenScheme: "--primary, --bg, --surface"),
        new("matddns", "Matddns", "DynDNS client with failover", Infrastructure, "#204db6", "globe", "Matddns",
            FoundAccent: "#4f8cff", LogoColor: "#4f8cff", Port: 4060, CssPrefix: "dt-", TokenScheme: "--accent, --panel"),
        new("matgate", "Matgate", "One login for your home network", Infrastructure, "#0a7d57", "shield", "Matgate",
            Port: 8088, Notes: "No stylesheet of its own in the repository (the UI comes from Apache Guacamole), so the accent is a proposal."),
        new("matbu", "MatBu", "Backup and sync", Infrastructure, "#0b7f8a", "archive", "MatBu",
            FoundAccent: "#0b7f8a", LogoColor: "#0b7f8a", CssPrefix: "(none)", TokenScheme: "--teal, --ink, --card",
            Notes: "Light theme uses #297c73 for the teal, dark theme #0b7f8a."),
        new("matarchive", "Matarchive", "Backup and sync jobs", Infrastructure, "#4fd1c5", "archive", "Matarchive",
            FoundAccent: "#4fd1c5", OnAccent: "#0b1020", AccentText: "#1d7f77", CssPrefix: "(none)", TokenScheme: "--accent, --panel",
            Notes: "Only a dark theme. The accent is light, so text on it is dark."),

        new("matmail", "MatMail", "Mail gateway with a web mail client", Productivity, "#1a56db", "mail", "MatMail",
            FoundAccent: "#475569", LogoColor: "#1a56db", Port: 9933, CssPrefix: "mm-", TokenScheme: "--color-*",
            Notes: "The default UI accent is slate (user-selectable), the logo is blue: the toolkit follows the logo."),
        new("matpaper", "MatPaper", "Document management system", Productivity, "#268538", "paperclip", "MatPaper",
            FoundAccent: "#2f9e44", LogoColor: "#268538", Port: 4994, CssPrefix: "mp-", TokenScheme: "--color-*",
            Notes: "Has schemes (standard, paper, contrast, black) and a user-selectable accent. Green darkened for white text."),
        new("matdo", "Matdo", "Tasks and projects", Productivity, "#8300bc", "check-square", "Matdo",
            FoundAccent: "#8300bc", LogoColor: "#540078", CssPrefix: "dp-", TokenScheme: "--accent, --bg, --card",
            Notes: "Ten accent themes (purple is the default). The logo is a darker purple."),
        new("matfile", "MatFile", "File explorer for your NAS", Productivity, "#3472d8", "hard-drive", "MatFile",
            FoundAccent: "#3b82f6", LogoColor: "#3b82f6", Port: 2222, CssPrefix: "mf-, mfx-", TokenScheme: "--mf-*"),
        new("matcms", "MatCMS", "Block-based CMS for websites", Productivity, "#2563eb", "template", "MatCMS",
            FoundAccent: "#2563eb", LogoColor: "#3b82f6", Port: 9101, CssPrefix: "el-, le-, admin-", TokenScheme: "--accent, --bg, --surface",
            Notes: "MatCMS Cloud (9100) shares the admin shell."),

        new("matmon", "Matmon", "Monitoring for your services", Data, "#295cff", "activity", "Matmon",
            FoundAccent: "#295cff", LogoColor: "#1b86d0", Port: 8099, CssPrefix: "(feature names)", TokenScheme: "--matmon-*",
            Notes: "A logo with a teal-to-blue gradient; the UI is blue."),
        new("matstat", "Matstat", "Data integration, statistics and reporting", Data, "#2563eb", "bar-chart", "Matstat",
            FoundAccent: "#2563eb", CssPrefix: "mat-", TokenScheme: "--mat-*"),
        new("msbi", "msBi", "Self-hosted business intelligence", Data, "#6d5df6", "layers", "msBi",
            FoundAccent: "#6d5df6", Port: 2442, CssPrefix: "ms-", TokenScheme: "--ms-accent + Bootstrap"),

        new("matbiz", "Matbiz", "Modular ERP", Business, "#1b6ec2", "briefcase", "Matbiz",
            FoundAccent: "#1b6ec2", Port: 8040, CssPrefix: "(none)", TokenScheme: "none (literal colours)"),

        new("matsplit", "MatSplit", "Shared expenses for trips and flats", Home, "#bf5610", "dollar", "MatSplit",
            FoundAccent: "#ef6c14", LogoColor: "#e4570a", Port: 4774, CssPrefix: "ms-", TokenScheme: "--ms-*",
            Notes: "Warm palette. Orange darkened for white text."),
        new("mattrakr", "MatTrakr", "Movies, series and books", Home, "#6c5ce7", "progress-play", "MatTrakr",
            FoundAccent: "#6c5ce7", Port: 9242, CssPrefix: "mt-", TokenScheme: "--mt-*"),
        new("matplay", "MatPlay", "Scorekeeper for game nights", Home, "#00819b", "gamepad", "MatPlay",
            FoundAccent: "#0099b8", LogoColor: "#00e5ff", Port: 4664, CssPrefix: "mp-", TokenScheme: "--primary, --panel",
            Notes: "Neon look in dark mode (cyan, pink, lime). Cyan darkened for white text."),
        new("matlan", "Matlan", "Language practice", Home, "#418251", "message-square", "Matlan",
            FoundAccent: "#4d9b61", CssPrefix: "(none)", TokenScheme: "--accent, --panel"),
    ];

    public static MatApp? Get(string id) => All.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>An accent a user can choose in the settings of any app (the same ten as in Matdo, plus their contrast-checked values).</summary>
public sealed record AccentPreset(string Id, string Name, string Value);

public static class AccentPresets
{
    public static IReadOnlyList<AccentPreset> All { get; } =
    [
        new("purple", "Purple", "#8300bc"),
        new("indigo", "Indigo", "#3f51b5"),
        new("ocean", "Ocean", "#1a73e8"),
        new("teal", "Teal", "#008578"),
        new("forest", "Forest", "#1c863a"),
        new("gold", "Gold", "#966d07"),
        new("sunset", "Sunset", "#ca4c09"),
        new("red", "Red", "#cb4538"),
        new("berry", "Berry", "#c2185b"),
        new("graphite", "Graphite", "#546e7a"),
    ];
}
