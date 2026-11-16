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

    /// <summary>A picture drawn by MatDesktop (the gallery of its tests, MATDESKTOP_SHOTS) with its sample program, light and dark.</summary>
    private static DesktopFigure Desk(string page, string stem, string alt, string caption, int width, int height, params string[] rules)
        => new(page, $"matdesktop-{stem}-light.png", alt, caption, width, height, rules, $"matdesktop-{stem}-dark.png");

    /// <summary>A window of an agent (the Matgate Agent, WinForms), drawn by its <c>setup-preview</c> from sample data (computer ANNA-PC, user Anna), light and dark.</summary>
    private static DesktopFigure Agent(string page, string stem, string alt, string caption, int width, int height, params string[] rules)
        => new(page, $"agent-{stem}-light.png", alt, caption, width, height, rules, $"agent-{stem}-dark.png");

    /// <summary>A tray icon drawn by MatDesktop: in colour, or as the template image of macOS.</summary>
    private static DesktopFigure DeskIcon(string state, string word, bool macos, string caption)
        => new("tray", $"matdesktop-tray-{(macos ? "macos-" : "")}{state}.png",
            macos ? $"The tray icon of the sample agent as a macOS template image, {word}: the mark in black with the state as a shape at the bottom right." : $"The tray icon of the sample agent, {word}: its mark with the dot of the state at the bottom right, in a cut-out.",
            caption, 64, 64, ["TRAY-02"], Kind: macos ? DesktopFigureKind.TemplateIcon : DesktopFigureKind.Icon);

    public static IReadOnlyList<DesktopFigure> All { get; } =
    [
        // ---- the desktop program (ThixClip on MatDesktop, Avalonia), drawn by ThixClip.exe --snapshot with sample data (computer ANNA-PC, version 1.0.0), light and dark; its windows are in German
        Pair("app", "main-history", "The main window of ThixClip, in German: its own title bar with the logo and the word mark, the search, Pause, the shortcut and the caption buttons; the menu at the left with History tinted as the current item, Settings below a line and at its foot the retention, the cloud, the version and the fold button; the history as cards grouped by day, the chosen one tinted with the accent line all around and a filled star; a detail pane with one row of icon buttons.",
            "ThixClip: a title bar of its own, the menu of the web shell and the version at the foot of the menu.", 1180, 760, "DESK-03", "DESK-04", "SETUP-07", "COLOR-08"),
        Pair("app", "main-paused", "ThixClip while the recording is paused: the pause button of the title bar reads Resume · paused, in the colours of a warning; the rest of the window as before.",
            "A state that needs attention is shown where it is ended: Resume, in the colours of a warning.", 1180, 760, "DESK-02", "COLOR-04"),
        Pair("app", "main-folded", "ThixClip with the menu folded to a rail of icons; the foot of the rail holds the cloud, an info icon for About and the button that opens the menu again.",
            "The menu folded to a rail of 68 px; the choice is remembered.", 1180, 760, "DESK-04"),
        Pair("app", "main-settings", "The settings of ThixClip as a page of the main window, in German: tabs General, Recording, Cloud, Updates and Advanced; the shortcut, four switches, the appearance (as the system) and the retention.",
            "Settings are a page of the main window; the appearance offers System, Light and Dark.", 1180, 760, "DESK-02", "DESK-04", "DESK-07"),
        Pair("app", "main-about", "About ThixClip as a page of the main window, in German: logo, word mark, version, the state of updates with a button to check, a sentence on what it does, the build, the system and the data folder of the sample user.",
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
        Pair("tray", "toast", "A notification of ThixClip, 360 px wide, in German: Update available, ThixClip 1.0.1 can be installed (Settings › Updates).",
            "A notification of the program's own for news the user did not cause: an update is ready.", 360, 90, "TRAY-04"),
        Pair("tray", "dialog-pause", "The small dialog of ThixClip that pauses the recording, in German: a list of choices (15 minutes, 30 minutes chosen as a tint with the accent line all around, an hour, until the next start, until resumed) and the buttons Pause and Cancel.",
            "Pause is chosen by the user, so it is answered in the window and never by a notification.", 400, 393, "TRAY-04", "COLOR-08"),

        // ---- the setup of the desktop program, light and dark
        Pair("setup", "setup-welcome", "The first step of the setup of ThixClip, in German: header with logo, word mark and the mode Setup; what ThixClip does and how it is opened, and a note that it installs for the user without administrator rights and where to; the version at the left of the footer and Next and Cancel at the right.",
            "Welcome: what will happen and where it goes, without administrator rights.", 600, 560, "SETUP-01", "SETUP-02", "SETUP-03"),
        Pair("setup", "setup-options", "The Options step of the setup of ThixClip, in German: three switches with a sentence each, start at sign-in and automatic updates on, the desktop shortcut off; Install, Back and Cancel.",
            "Options: switches with the safe choice set.", 600, 560, "SETUP-02"),
        Pair("setup", "setup-progress", "The setup of ThixClip while it installs, in German: a bar and the steps with their state in an icon and a word (three done, one running); Install and Cancel are turned off.",
            "Progress: a bar and the steps; it cannot be cancelled or closed.", 600, 560, "SETUP-02"),
        Pair("setup", "setup-finish", "The last step of the setup of ThixClip: ThixClip is ready, how to open it, a switch to start it now and the button Done.",
            "Finish: the program is ready, and starts now if the switch stays on.", 600, 560, "SETUP-02"),
        Pair("setup", "setup-update", "The setup of ThixClip in the mode Update, in German: which version is installed (0.9.0) and which comes (1.0.0), that the settings are kept, and a note that a running ThixClip is ended and started again and the history is kept; Update and Cancel.",
            "Update: the same window, no options, the settings kept.", 600, 560, "SETUP-01", "SETUP-02"),
        Pair("setup", "setup-uninstall", "The setup of ThixClip in the mode Uninstall, in German: what will be removed, the switch Also delete my history (its own words for its data) that is off, a danger button Uninstall and Cancel.",
            "Uninstall: the same window; the data stays unless the switch is turned on.", 600, 560, "SETUP-04"),

        // ---- an agent (Matgate Agent, WinForms), drawn by matgate-agent setup-preview with sample data (computer ANNA-PC, version 0.8.0.42), light and dark; its windows are in German
        Agent("tray", "info", "The Info window of the Matgate Agent, in German: header with logo, name and Info; the state Running as a green dot and a word, with one sentence that it is ready for Matgate on port 7488; the facts as rows (computer ANNA-PC, port, mode as the signed-in user, started, version 0.8.0.42 · release, one open terminal, data folder); the version at the left of the footer and Connection details, Log folder and Close at the right. No password in it.",
            "An agent (WinForms): Info, with the state in a dot and a word; the password is one button away, never in Info.", 616, 460, "TRAY-05", "AGENT-03", "AGENT-05", "SETUP-07"),
        Agent("setup", "setup-update", "The setup of the Matgate Agent in the mode Update, in German: header with logo, name and Update; which version is installed (0.7.0.17) and which comes (0.8.0.42), that the token and the certificate are kept, where the program and the settings go, a note that the install needs administrator rights once; the version at the left of the footer and Next and Cancel at the right.",
            "An agent (WinForms): an update says what it keeps and that it needs administrator rights once.", 600, 560, "SETUP-01", "SETUP-03", "SETUP-07", "AGENT-05"),
        Agent("agents", "setup-finish", "The last step of the setup of the Matgate Agent, in German: the agent is ready and waits on port 7488; host ANNA-PC, port, the connection password (a sample value) and the SHA-256 fingerprint of the certificate, each in a field with a Copy button; a warning that the password is the only key to this computer and belongs only into Matgate; Done.",
            "Finish of an agent that others connect to: host, port, password and fingerprint, each with Copy, and the warning.", 600, 560, "SETUP-02", "AGENT-02", "AGENT-03"),
        Agent("agents", "connection", "The connection details of the Matgate Agent, opened from Info, in German: what to enter in Matgate, then host ANNA-PC, port, the connection password (a sample value) and the certificate fingerprint, each with Copy; the same warning; the version at the left of the footer and Close.",
            "Connection details: the one window with the secret, each value with Copy and the warning.", 616, 445, "AGENT-02", "AGENT-03", "TRAY-05"),
        Agent("agents", "setup-uninstall", "The setup of the Matgate Agent in the mode Uninstall, in German: what will be removed (the sign-in task, the firewall rule, the tray icon, the entry in Installed apps, the program), an unticked box to also delete the data (token, certificate, shared folders, log) with the sentence that a later install takes the same token, a note that the connection in Matgate stays until it is deleted there; a red Remove and Cancel.",
            "Removing an agent keeps its token unless the box is ticked, so that installing again keeps the connection.", 600, 560, "SETUP-04", "AGENT-03"),

        // ---- MatDesktop, the toolkit for desktop programs: drawn by its tests from the sample agent (computer ANNA-PC, version 1.4.2), light and dark
        Desk("app", "main-open", "A main window drawn by MatDesktop: its own title bar with the logo, the name, a search field and the caption buttons of Windows; the menu with Overview tinted as the current item, a line, Settings, and at its foot the state of the program (the place for its own), a link to the waiting update to 1.5.0 and the version; a page with the state, facts, two settings and two buttons.",
            "MatDesktop: the title bar, the menu with the version and a waiting update at its foot, and the controls, in the colours of the tokens.", 1000, 640, "DESK-02", "DESK-03", "DESK-04", "SETUP-07"),
        Desk("app", "main-folded", "The same window with the menu folded to a rail of icons: the current item tinted, and at the foot the download icon of the waiting update, an info icon for About and the button that opens the menu again.",
            "MatDesktop: the menu folded to a rail of 68 px.", 1000, 640, "DESK-04"),
        Desk("tray", "info", "The Info window drawn by MatDesktop for the sample agent: header with logo, name and Info; the state Running as a green dot and a word, one sentence; the facts as rows (computer, server, mode, started, version, data folder); the version at the left of the footer and Open, Log folder and Close at the right.",
            "MatDesktop: Info, the one window of a tray.", 630, 500, "TRAY-05", "COLOR-04"),
        Desk("tray", "notification", "A notification drawn by MatDesktop, 360 px wide: Connection lost, the agent cannot reach its server and tries again every minute.",
            "MatDesktop: a notification of the program's own, for what the user did not cause.", 360, 108, "TRAY-04"),
        DeskIcon("running", "running", macos: false, "MatDesktop, running: a green dot."),
        DeskIcon("waiting", "waiting", macos: false, "Waiting: an amber dot."),
        DeskIcon("stopped", "stopped", macos: false, "Stopped: a red dot."),
        DeskIcon("notsetup", "not set up", macos: false, "Not set up: a grey dot."),
        DeskIcon("running", "running", macos: true, "macOS, running: the mark alone."),
        DeskIcon("waiting", "waiting", macos: true, "macOS, waiting: a ring."),
        DeskIcon("stopped", "stopped", macos: true, "macOS, stopped: a cross."),
        DeskIcon("notsetup", "not set up", macos: true, "macOS, not set up: a dot."),
        Desk("setup", "setup-welcome", "The first step of the setup window drawn by MatDesktop for the sample agent: header with logo, name and the mode Setup; Welcome says what it does, that it runs as a service and needs administrator rights once, and where it goes; the version at the left of the footer and Next and Cancel at the right.",
            "MatDesktop: Welcome says what will happen, where to and with which rights.", 600, 560, "SETUP-01", "SETUP-02", "SETUP-03"),
        Desk("setup", "setup-connect", "The Connect step of the setup window drawn by MatDesktop: the address of the server and the enrolment code as fields with hints; Next, Back and Cancel.",
            "MatDesktop: Connect, the code is checked before anything is installed.", 600, 560, "SETUP-02", "AGENT-02"),
        Desk("setup", "setup-options", "The Options step drawn by MatDesktop: three switches, start at sign-in and automatic updates on, the desktop shortcut off; Install, Back and Cancel.",
            "MatDesktop: Options are switches with the safe choice set.", 600, 560, "SETUP-02"),
        Desk("setup", "setup-progress", "The Progress step drawn by MatDesktop: a bar two thirds along and the steps with their state in an icon and a word (done, running, waiting); the buttons are turned off.",
            "MatDesktop: Progress cannot be cancelled or closed.", 600, 560, "SETUP-02", "COLOR-04"),
        Desk("setup", "setup-finish", "The Finish step drawn by MatDesktop: the agent is ready, a sentence where its icon is, a switch to start it now that is on, and Done.",
            "MatDesktop: Finish offers to start the program now.", 600, 560, "SETUP-02"),
        Desk("setup", "setup-uninstall", "The setup window drawn by MatDesktop in the mode Uninstall: what will be removed, the switch Also delete my data that is off, a danger button Uninstall and Cancel.",
            "MatDesktop: Uninstall in the same window, the data kept unless the switch is turned on.", 600, 560, "SETUP-01", "SETUP-04"),
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
