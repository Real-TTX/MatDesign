using MatDesign.Core;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Services;

/// <summary>How a picture of Desktop and services is shown.</summary>
public enum DesktopFigureKind
{
    /// <summary>A window or a part of one: in the grid of pictures, as wide as it is or as the column.</summary>
    Window,

    /// <summary>A tray icon of 64 px: in a row of small tiles.</summary>
    Icon,

    /// <summary>A tray icon that is a macOS template image (black on transparent): on a light tile in both themes, so that it stays visible in dark mode.</summary>
    TemplateIcon,
}

/// <summary>
/// A picture of a real desktop program or agent on a page of Desktop and services: a file of <c>wwwroot/img/desktop</c> and, where the program draws both, its dark twin (<see cref="Dark"/>),
/// shown by the site's theme with the classes of the brand's dark variants; what it shows (the alt text), the caption under it, its size in pixels (so that the page does not jump while it
/// loads) and the rules it shows. The programs in the pictures are sample material of the demo: a page shows them only on the demo template and draws its specimens with the company's names on
/// every other (IDENT-09).
/// </summary>
public sealed record DesktopFigure(string Page, string File, string Alt, string Caption, int Width, int Height, IReadOnlyList<string> Rules, string? Dark = null, DesktopFigureKind Kind = DesktopFigureKind.Window)
{
    /// <summary>The address of the picture (the light one when there are two).</summary>
    public string Src => DesktopFigures.Folder + File;

    /// <summary>The address of the dark picture, or null.</summary>
    public string? DarkSrc => Dark is null ? null : DesktopFigures.Folder + Dark;

    /// <summary>The files of the picture.</summary>
    public IEnumerable<string> Files => Dark is null ? [File] : [File, Dark];
}

/// <summary>
/// The pictures of Desktop and services, as data: a new picture is one line here and its files in <c>wwwroot/img/desktop</c> (a test holds the two together and reads the size of each file).
/// The programs draw their windows from fixed sample data without a screen (docs/DESKTOP.md, "Pictures of the windows"); a newer picture replaces the file of the same name.
/// </summary>
public static class DesktopFigures
{
    /// <summary>The folder of the pictures under the site's <c>wwwroot</c>.</summary>
    public const string Folder = "/img/desktop/";

    /// <summary>The keys of the pages, in the order of the menu.</summary>
    public static IReadOnlyList<string> Pages { get; } = ["overview", "app", "tray", "setup", "agents"];

    private static DesktopFigure Pair(string page, string stem, string alt, string caption, int width, int height, params string[] rules)
        => new(page, $"app-{stem}-light.png", alt, caption, width, height, rules, $"app-{stem}-dark.png");

    public static IReadOnlyList<DesktopFigure> All { get; } =
    [
        // ---- the desktop program (ThixClip, Avalonia), drawn by ThixClip.exe --snapshot with sample data, light and dark
        Pair("app", "main-history", "The main window of ThixClip: its own title bar with the logo, the search, Pause, the shortcut and the caption buttons; the menu at the left with the version at its foot; a list of entries and a detail pane.",
            "ThixClip: a title bar of its own, the menu of the web shell and the version at the foot of the menu.", 1180, 760, "DESK-03", "DESK-04", "SETUP-07"),
        Pair("app", "main-folded", "ThixClip with the menu folded to a rail of icons; the foot of the rail holds the cloud, About and the button that opens the menu again.",
            "The menu folded to a rail of 68 px; the choice is remembered.", 1180, 760, "DESK-04"),
        Pair("app", "main-settings", "The settings of ThixClip as a page of the main window, with the shortcut, switches, and the appearance that offers System, Light and Dark.",
            "Settings are a page of the main window; the appearance offers System, Light and Dark.", 1180, 760, "DESK-02", "DESK-04", "DESK-07"),
        Pair("app", "main-about", "About ThixClip as a page of the main window: logo, name, version, the state of updates, the channel, the system and the data folder.",
            "About is a page too, opened by the version at the foot of the menu.", 1180, 760, "DESK-04", "SETUP-07"),

        // ---- the tray of the desktop program, and the Info window of an agent
        new("tray", "app-tray-icon-windows.png", "The tray icon of ThixClip on Windows: the clipboard mark in the accent colour.",
            "Windows: the mark in colour.", 64, 64, ["TRAY-02", "TRAY-03"], Kind: DesktopFigureKind.Icon),
        new("tray", "app-tray-icon-windows-paused.png", "The tray icon of ThixClip on Windows while paused: an amber dot with a pause sign at the bottom right.",
            "Windows, paused: a state of the program's own as a dot.", 64, 64, ["TRAY-02"], Kind: DesktopFigureKind.Icon),
        new("tray", "app-tray-icon-macos.png", "The tray icon of ThixClip on macOS: the mark as a black template image.",
            "macOS: a template image that the system colours.", 64, 64, ["TRAY-02", "TRAY-03"], Kind: DesktopFigureKind.TemplateIcon),
        new("tray", "app-tray-icon-macos-paused.png", "The tray icon of ThixClip on macOS while paused, as a template image.",
            "macOS, paused: the words in the menu carry the state.", 64, 64, ["TRAY-02"], Kind: DesktopFigureKind.TemplateIcon),
        Pair("tray", "toast", "A notification of ThixClip, 360 px wide: Update available, ThixClip 1.0.1 can be installed.",
            "A notification of the program's own for news the user did not cause: an update is ready.", 360, 90, "TRAY-04"),
        Pair("tray", "dialog-pause", "The small dialog of ThixClip that pauses the recording for 15 minutes, 30 minutes, an hour, until the next start or until resumed.",
            "Pause is chosen by the user, so it is answered in the window and never by a notification.", 400, 408, "TRAY-04"),

        // ---- the setup of the desktop program, light and dark
        Pair("setup", "setup-welcome", "The first step of the setup of ThixClip: header with logo, name and the mode Setup, three switches (start at sign-in, desktop shortcut, automatic updates), a note that it installs for the user without administrator rights and where to, the version at the left of the footer and Install and Cancel at the right.",
            "Welcome: the options with the safe choice set, and where it goes, without administrator rights.", 600, 560, "SETUP-01", "SETUP-02", "SETUP-03"),
        Pair("setup", "setup-progress", "The setup of ThixClip while it installs: a bar and the list of the steps that are done; Install and Cancel are turned off.",
            "Progress: a bar and the steps; it cannot be cancelled or closed.", 600, 560, "SETUP-02"),
        Pair("setup", "setup-finish", "The last step of the setup of ThixClip: ThixClip is ready, how to open it, a switch to start it now and the button Done.",
            "Finish: the program is ready, and starts now if the switch stays on.", 600, 560, "SETUP-02"),
        Pair("setup", "setup-update", "The setup of ThixClip in the mode Update: which version is installed and which comes, that the history is kept, and where it installs.",
            "Update: the same window, no options, the settings kept.", 600, 560, "SETUP-01", "SETUP-02"),
        Pair("setup", "setup-uninstall", "The setup of ThixClip in the mode Uninstall: what will be removed, a switch to also delete the history that is off, a red Uninstall button and Cancel.",
            "Uninstall: the same window; the data stays unless the switch is turned on.", 600, 560, "SETUP-04"),

        // ---- the setup of an agent (Matgate Agent, WinForms)
        new("setup", "agent-setup-update.png", "The first step of the setup of the Matgate Agent in the mode Update: header with logo, name and mode, what the update keeps, a note that it needs administrator rights, the version at the left of the footer and the buttons Next and Cancel.",
            "An agent (WinForms): the Welcome step says that administrator rights are needed.", 656, 719, ["SETUP-03", "AGENT-05"]),

        // ---- the agents
        new("agents", "agent-setup-uninstall.png", "The setup of the Matgate Agent in the mode Uninstall: what will be removed, an unticked box to also delete the settings with the token and the certificate, and the buttons Remove and Cancel.",
            "Removing an agent keeps its token unless the box is ticked, so that installing again keeps the connection.", 656, 719, ["SETUP-04", "AGENT-03"]),
    ];

    /// <summary>The pictures of one page, in the order of the list.</summary>
    public static IReadOnlyList<DesktopFigure> On(string page) => All.Where(f => f.Page == page).ToList();

    /// <summary>
    /// The program that the drawn specimens are about: on the demo the monitoring app of the sample apps, which has an agent; on every other template the company's own short name and the site's
    /// logo, never a name of the demo (IDENT-09).
    /// </summary>
    public static DesktopSample Sample(DesignData design)
    {
        var app = design.Pick("matmon");
        return new DesktopSample(app, design.IsDemo ? app.Name : design.Tenant.ShortName);
    }
}

/// <summary>The names the specimens of Desktop and services are drawn with (AGENT-01): the app whose logo they show and the product the agent belongs to.</summary>
public sealed record DesktopSample(MatApp App, string Product)
{
    /// <summary>The display name of the service, of the start menu entry and of Installed apps.</summary>
    public string Agent => Product + " Agent";

    /// <summary>The product in lower case with hyphens, the start of the program's file name.</summary>
    public string Slug => Navigation.Slug(Product);

    /// <summary>The version every specimen shows (SETUP-07).</summary>
    public const string Version = "Version 1.4.2 · release";
}
