# MatDesktop

The toolkit for the desktop programs of the Mat apps: what MatUi is for a web page, MatDesktop is for a program with windows, a tray and a setup, written with
Avalonia 11 (11.3.22) on .NET 10. It implements the rules `DESK`, `TRAY`, `SETUP` and `AGENT` of the rulebook; [docs/DESKTOP.md](../../docs/DESKTOP.md) says what each
rule means in practice. It is not part of the site or its image: this folder is where it is built and tested (`tests/MatDesktop.Tests`), and programs copy it.

## What is in it

| Part | Files | Rules |
| --- | --- | --- |
| The tokens of `matui.tokens.css` as C#: colours in light and dark, sizes, type, motion, the rules of the accent | `Tokens/MatTokens.g.cs` (generated) | `DESK-02` |
| Colour maths like the CSS (OKLab `color-mix`, a tint with transparency, WCAG contrast) and the palette of one mode for one accent | `Tokens/MatColor.cs`, `Tokens/MatPalette.cs` | `DESK-02`, `IDENT-01` |
| The theme: Fluent in the colours of MatUi, a brush per token, the sizes, the control themes, System / Light / Dark | `MatTheme.cs`, `Themes/Controls.axaml` | `DESK-02`, `COLOR-08`, `A11Y-01` |
| Every icon of the sprite as a geometry, and the control that strokes it (or fills it: `IsFilled`) | `Themes/MatIcons.axaml` (generated), `Controls/MatIcon.cs` | `DESK-09` |
| Every icon of the sprite as plain segments for a program without Avalonia (WinForms) | `Tokens/MatIconPaths.g.cs` (generated) | `DESK-09`, `AGENT-05` |
| The window, the title bar with the caption buttons of the system, the menu that folds to a rail | `Controls/MatWindow.cs`, `MatTitleBar.cs`, `MatNav.cs` | `DESK-03`, `DESK-04` |
| Setting rows, form fields, facts, a value to copy, the state as a dot and a word | `Controls/MatSettingRow.cs`, `MatFacts.cs`, `MatCopyField.cs`, `MatStateDot.cs`, `MatState.cs` | `DESK-04`, `TRAY-02`, `COLOR-04`, `AGENT-02` |
| The version, the texts in English and German | `MatVersion.cs`, `MatText.cs` | `SETUP-07`, `DESK-08` |
| The tray: the menu in the order of the rule, the icons with the dot of the state, the clicks, a notification of its own, the Info window | `Tray/MatTrayMenu.cs`, `MatTray.cs`, `MatTrayIcons.cs`, `MatNotification.cs`, `Windows/MatInfoWindow.cs` | `TRAY-01` … `TRAY-05` |
| The header and the footer of a dialog (Info, the setup, a program's own dialogs) | `Windows/MatDialogChrome.cs` | `SETUP-01`, `SETUP-07`, `AGENT-05` |
| The setup window for Setup, Update and Uninstall, its steps as building blocks | `Setup/MatSetupWindow.cs`, `MatSetupStep.cs` | `SETUP-01`, `SETUP-02`, `SETUP-04`, `SETUP-07` |
| One instance per user session (a named mutex and a named pipe; .NET makes them a lock file and a Unix socket on macOS and Linux) | `MatSingleInstance.cs` | `DESK-05` |

`Tokens/MatTokens.g.cs`, `Themes/MatIcons.axaml` and `Tokens/MatIconPaths.g.cs` are written by `node tools/desktop.js` from `src/MatUi/wwwroot/css/matui.tokens.css` and
`src/MatUi/wwwroot/img/matui-icons.svg`; never edit them, change the CSS or the sprite and run the script (CI fails when they are stale). An icon the sprite lacks is
added to the sprite first (`tools/icons.js`), never drawn here.

**The sizes are those of MatUi's compact density with a mouse** (`data-density="compact"` with `pointer: fine`): controls 34 px, body type 14 px, the radii and the
spacing of the tokens. A desktop window is used with a mouse; the touch sizes of the web (44 px) do not apply.

**No colour schemes yet**: `data-scheme` (paper, contrast, oled, `COLOR-09`) is not part of MatDesktop 0.2; the accent text keeps all of the accent, as in the standard look.

## Use it in a program

1. Copy it: `node tools/matdesktop-copy.js <folder of the program's project>` (in MatDesign). Every file gets the first line
   `// MatDesktop 0.2.0 – a copy from MatDesign, do not edit here` (an XML comment in an `.axaml` file), `MatDesktop/matdesktop.version` lists the SHA-256 of each file,
   and the files of an older copy that the package no longer has are removed. The project needs `Avalonia` and `Avalonia.Themes.Fluent` (the tool says so when they
   are missing) and `ImplicitUsings`. Never edit a copy: change MatDesktop here and copy again.
2. Install the theme in `Application.Initialize`, with the app's accent from `matui.apps.css` (`IDENT-01`):

   ```csharp
   public override void Initialize() =>
       MatTheme.Install(this, new MatThemeOptions("#295cff", Appearance: settings.Appearance));
   ```

   `MatThemeOptions.Icon` (or `MatWindow.DefaultIcon`) is the icon of every window, the setup and Info included, so that no window has to set it.
   `MatTheme.SetAppearance(MatAppearance.System | Light | Dark)` switches later (the setting "Appearance", `DESK-02`). An app that sets its own text colours passes them
   (`OnAccent`, `AccentText`, as `--mat-app-on-accent` and `--mat-app-accent-text` of `matui.apps.css`).
3. Windows derive from `MatWindow`. Colours are `{DynamicResource Mat.Surface}` and never a literal (`DESK-02`); the named looks are control themes:
   `Theme="{DynamicResource MatButton.Primary}"` (in code `button.Themed("MatButton.Primary")`).

### The keys of the theme

| Kind | Keys |
| --- | --- |
| A brush per colour token (light and dark) | `--mat-surface-2` is `Mat.Surface2`, its colour `Mat.Surface2.Color`: `Mat.Bg`, `Mat.Bg2`, `Mat.Surface`, `Mat.Surface2`, `Mat.Surface3`, `Mat.Glass`, `Mat.NavBg`, `Mat.Overlay`, `Mat.Border`, `Mat.BorderStrong`, `Mat.Text`, `Mat.TextMuted`, `Mat.TextFaint`, `Mat.Danger`, `Mat.DangerBg`, `Mat.DangerText`, `Mat.OnDanger`, `Mat.Thumb`, `Mat.Success`, `Mat.SuccessBg`, `Mat.SuccessText`, `Mat.Warning`, `Mat.WarningBg`, `Mat.WarningText`, `Mat.Info`, `Mat.InfoBg`, `Mat.InfoText`, `Mat.ScrollThumb`, and of the accent `Mat.Accent`, `Mat.AccentHover`, `Mat.AccentActive`, `Mat.AccentSoft`, `Mat.AccentSoftStrong`, `Mat.AccentText`, `Mat.OnAccent`, `Mat.FocusRing`, `Mat.Selection` (`MatTheme.KeyOf(token)`) |
| Derived by MatDesktop | `Mat.DangerLine` (the line of a danger button); `Mat.CaptionCloseHover` and `Mat.CaptionCloseHoverGlyph` (the red close button of Windows, the one literal colour, with its reason in `MatTitleBar`) |
| Fluent's own | `SystemAccentColor` (by the palette), the `TextControl*`, `ComboBox*`, `ToggleSwitch*` and `CheckBox*` brushes, `ControlCornerRadius`, `TextControlThemeMinHeight` |
| Sizes | `Mat.RadiusXs`, `Mat.RadiusSm`, `Mat.Radius`, `Mat.RadiusLg`, `Mat.RadiusXl`, `Mat.RadiusPill` (`CornerRadius`); `Mat.Space1` … `Mat.Space12` (numbers) and `Mat.Space4.All` (`Thickness`); `Mat.ControlHeight` (34), `Mat.ControlHeightSm`, `Mat.ControlHeightLg`; `Mat.FocusRingWidth` |
| Type | `Mat.FontSize` (14), `Mat.FontSizeXs` … `Mat.FontSize3xl`; `Mat.Font` (Segoe UI Variable on Windows, the system font on macOS, the stack of the web elsewhere) and `Mat.Mono` (`FontFamily`) |
| Control themes | the default `Button` (MatUi's secondary button), `MatButton.Primary`, `.Danger`, `.Warning` (a state that needs attention and the button that ends it: Resume while paused), `.Ghost`, `.Icon`, `.Link`, `.Caption`, `.CaptionClose`; `MatText.PageTitle`, `.SectionTitle`, `.Muted`, `.Faint`, `.FieldLabel`; `MatCard`, `MatKeyCap`, `MatBadge`, `MatBadge.Soft` (the tint of the accent, `mat-badge--soft`), `MatSearchBox`, `MatProgress`; `MatList` (a `ListBox` without a frame) with `MatChoiceRow` (a choice: chosen = the tint with the accent line all around, `COLOR-08`) or `MatCardRow` (a row as a card, chosen the same way) as its `ItemContainerTheme`; `TabControl` and `TabItem`; `MatNavItem`, `MatSettingRow`, `MatFormField` |
| Icons | `MatIcon.<name>` for every symbol of the sprite (`MatIcon.<name>.Fill` where the sprite fills a part); draw them with `<mat:MatIcon Icon="search" Size="16"/>` (the property is `Icon`: `Name` is the name of an element in Avalonia), filled with `IsFilled="True"` (a chosen star, a pinned pin: the same geometry) |

### The main window (DESK-03, DESK-04)

```csharp
var bar = new MatTitleBar { AppName = "Example", Logo = logo, CloseKey = "Esc", Center = searchBox };
bar.CloseRequested += (_, e) => { e.Handled = true; Hide(); };      // a program with a tray hides (DESK-06)
var nav = new MatNav { AppName = "Example", Version = "1.4.2", Channel = MatVersion.Release, TitleBar = bar, IsFolded = settings.MenuFolded };
nav.Items.Add(new MatNavItem("dashboard", "Overview", "Ctrl+1") { IsCurrent = true });
nav.Settings = new MatNavItem("settings", "Settings", "Ctrl+,");
nav.ItemInvoked += (_, item) => ShowPage(item);
nav.AboutRequested += (_, _) => ShowAbout();
nav.FoldChanged += (_, folded) => settings.MenuFolded = folded;  // remembered
nav.FoldShortcut = "Ctrl+B";                                       // named in the tooltip of the fold button (DESK-07); the window handles the key
nav.Foot = retentionCard;                                          // the program's own above the version: a card, the state of a connection
nav.UpdateVersion = "1.5.0";                                       // a link "Update to 1.5.0" above the version while one waits (SETUP-06)
nav.UpdateRequested += (_, _) => ShowAbout();
window.UseOwnTitleBar();                                           // in the constructor of the MatWindow
```

The menu is 248 px wide, 210 px in a window narrower than 900 px and 68 px folded; a long version (`Version local-20261115 · local`) is trimmed with an ellipsis and its tooltip has all of it; the title bar is 68 px high, its caption buttons 46 × 32 in the shape of Windows 11.

### The tray (TRAY-01 … TRAY-04)

```csharp
var menu = new MatTrayMenu("Example Agent", "1.4.2", quit: () => app.Shutdown())
{
    Open = ShowMainWindow, Info = ShowInfo, Settings = ShowSettings, LogFolder = OpenLogs, State = MatState.Running,
};
menu.AddService(MatServiceAction.Restart, RestartService);         // with the shield (administrator rights)
menu.OpenShortcut = "Ctrl+Shift+V";                                // at the right of Open
menu.TooltipText = "Synchronised at 14:32";                        // a second line of the tooltip, after "Example Agent: Running" (TRAY-02)
var tray = new MatTray(app, menu, mark);                           // mark: the app's mark, 64 px or more
tray.MenuOpening += (_, _) => FillLastEntries(menu);               // where the system says that the menu opens (macOS, Linux)
menu.State = MatState.Waiting; tray.Refresh();                     // the dot, the tooltip, the second line of the menu
MatNotification.Show("Connection lost", "It tries again every minute.", isWarning: true, onClick: ShowMainWindow);
```

`MatTrayMenu.Build()` puts the items in the order of `TRAY-01` whatever order the program gives them (its own items in groups: `AddOwnSeparator()` starts the next one, all in the place of the rule), leaves out what it does not have, never shows a separator twice and has
no Uninstall item; `Install update …` is there only while `InstallUpdate` is set. Avalonia's menu cannot draw the default item in bold: it is the first item that acts, and
a left click on Windows runs it (`TRAY-03`). `MatTrayIcons` draws the dot of the state into a cut-out of the mark (in colour, or as a shape on the template image of macOS); `Refresh()` draws it again only when the colour or the mark of the state changed, so a program may call it whenever its menu changes. Avalonia 11 on Windows builds the tray menu without saying that it opens: there the menu is what the last `Refresh()` left, so a program with changing items calls `Refresh()` when they change (cheap) and also fills them in `MenuOpening` for macOS and Linux.

### Info and the setup (TRAY-05, SETUP-01 … SETUP-07)

```csharp
new MatInfoWindow(new MatInfo("Example Agent", "1.4.2", "release", MatState.Running, "Connected since 09:12.",
    [new MatFact("Computer", machine), new MatFact("Data folder", folder, IsMono: true)]) { Logo = logo, Open = OpenPage, OpenLogFolder = OpenLogs }).Show();

var setup = new MatSetupWindow(new MatSetup("Example Agent", "1.4.2", "release", MatSetupMode.Setup,
    [
        MatSetupStep.Welcome("What happens, where to, with which rights."),
        MatSetupStep.Connect("The address and the code.", fields, check: CheckCodeAsync),     // a bad code installs nothing
        MatSetupStep.WithOptions(null, MatSetupOption.Autostart(), MatSetupOption.AutomaticUpdates(), MatSetupOption.DesktopShortcut()),
    ],
    run: async r => { r.Progress.Report(new MatSetupReport("Copy the program", MatSetupStepState.Done, 40)); await InstallAsync(r.Options); })
    { Logo = logo, WordMark = wordMark, DeleteDataText = "Also delete my history", Finish = MatSetupStep.Finish("Its icon is in the notification area.") });
setup.Finished += (_, _) => { if (setup.StartNow) StartProgram(); };
```

The window is 600 × 560 and cannot be resized; the footer shows `Version 1.4.2 · release` at the left and the primary action, Back and Cancel at the right. Progress cannot
be cancelled or closed. `MatSetupMode.Update` asks nothing and names the installed and the new version (`InstalledVersion`); `MatSetupMode.Uninstall` needs
`MatSetupStep.Uninstall("what goes")` and adds "Also delete my data" (off; `DeleteDataText` and `DeleteDataDescription` name the program's data) and a danger button. `WordMark` puts the program's word mark into the header instead of its name.

A dialog of the program's own takes the same header and footer: `MatDialogChrome.Header(logo, "Example", "Pause")`, `MatDialogChrome.Footer(null, ok, cancel)` and `MatDialogChrome.Compose(header, body, footer)` as the content of a `MatWindow`.

### A tray that stays WinForms (AGENT-05)

`node tools/matdesktop-copy.js <folder> --tokens-only` copies `Tokens/MatTokens.g.cs`, `Tokens/MatColor.cs` and `Tokens/MatPalette.cs`, which need nothing but System:

```csharp
var palette = MatPalette.For("#295cff", dark: false);
header.BackColor = Color.FromArgb(palette.Surface.R8, palette.Surface.G8, palette.Surface.B8);
var line = palette.Border.Over(palette.Surface);                   // WinForms has no transparency: paint a line over its surface first
```

`node tools/matdesktop-copy.js <folder> --winforms` copies every file that needs nothing but System: the three of the tokens, `Tokens/MatIconPaths.g.cs`, `MatText`, `MatVersion`,
`MatState`, `MatDesktopInfo`, `MatSingleInstance` and `Tray/MatTrayMenu.cs` (a WinForms tray builds its `ContextMenuStrip` from `MatTrayMenu.Build()`).

**The icons without Avalonia** (`DESK-09`): `MatIconPaths.Find(name)` is an icon of the sprite as segments on the grid of 24 (`MoveTo`, `LineTo`, `CubicTo`, `Close`; the generator turns
arcs and quadratic curves into cubic curves, and every figure starts with a `MoveTo`), `Stroke` to stroke with a line of 2 and round ends, `Fill` to fill. A test holds them to the
Avalonia geometry of the same icon. With GDI+, instead of a glyph of Segoe Fluent Icons:

```csharp
static void DrawIcon(Graphics g, string name, RectangleF box, Color colour)
{
    var icon = MatIconPaths.Find(name) ?? throw new ArgumentException($"The sprite has no icon {name}.", nameof(name));
    var scale = box.Width / (float)MatIconPaths.Grid;
    PointF P(double x, double y) => new(box.X + (float)x * scale, box.Y + (float)y * scale);
    GraphicsPath PathOf(IReadOnlyList<MatPathSegment> segments)
    {
        var path = new GraphicsPath();
        var at = PointF.Empty;
        foreach (var s in segments)
        {
            switch (s.Verb)
            {
                case MatPathVerb.MoveTo: path.StartFigure(); at = P(s.X, s.Y); break;
                case MatPathVerb.LineTo: path.AddLine(at, at = P(s.X, s.Y)); break;
                case MatPathVerb.CubicTo: path.AddBezier(at, P(s.X1, s.Y1), P(s.X2, s.Y2), at = P(s.X, s.Y)); break;
                case MatPathVerb.Close: path.CloseFigure(); break;
            }
        }

        return path;
    }

    g.SmoothingMode = SmoothingMode.AntiAlias;
    using var pen = new Pen(colour, (float)MatIconPaths.StrokeWidth * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
    using var stroke = PathOf(icon.Stroke);
    using var fill = PathOf(icon.Fill);
    using var brush = new SolidBrush(colour);
    g.DrawPath(pen, stroke);
    g.FillPath(brush, fill);
}
```

`DrawIcon(e.Graphics, "power", new RectangleF(8, 8, 16, 16), text)` draws the power icon of the web at 16 px; draw it into a `Bitmap` for a `ToolStripMenuItem.Image`.

## Pictures

`MATDESKTOP_SHOTS=<folder> dotnet test tests/MatDesktop.Tests --filter GalleryTests` draws every window of this library without a screen from fixed sample data (the sample
program "Matmon Agent", the computer `ANNA-PC`, version 1.4.2), in light and dark: the site shows them on the pages of Desktop and services (`matdesktop-*.png`).

## Versions

The version is `MatDesktopVersion` in `MatDesktop.csproj` and `MatDesktopInfo.Version` (a test holds them equal). A change that a program can see raises it and gets a
line here.

## 0.2.0

What moving four programs onto it showed (2026-11-16; ThixClip uses it, the WinForms trays of Matmon, Matgate and MatHub take its plain part):

- `MatNav`: `Foot`, a place for the program's own above the version (a card, the state of a cloud); `UpdateVersion` and `UpdateRequested`, a link "Update to 1.5.0" above the
  version while an update waits (the icon on the rail); `FoldShortcut`, named in the tooltip of the fold button; a long version is trimmed with an ellipsis and the tooltip has all of it.
- `MatIcon.IsFilled`: the same geometry filled (a chosen star, a pinned pin).
- `MatTray`: `MenuOpening` where the system says that the menu opens; `Refresh()` draws the icon again only when the colour or the mark of the state changed (`IconDraws` counts it).
- `MatTrayMenu`: `AddOwnSeparator()` (the program's own items in groups), `OpenShortcut` (shown at the right of Open), `TooltipText` and `Tooltip` (the name and the state first, then more,
  127 characters at most).
- `MatWindow.DefaultIcon` and `MatThemeOptions.Icon`: the icon of every window.
- `MatSetup.WordMark`, `DeleteDataText`, `DeleteDataDescription`; `MatDialogChrome` (the header and the footer, public now: a program's own dialogs use them).
- Control themes: `MatButton.Warning`, `MatBadge.Soft`, `MatList`, `MatChoiceRow`, `MatCardRow`.
- `Tokens/MatIconPaths.g.cs`: every icon of the sprite as plain segments for WinForms, copied by `--winforms`. The sprite has `zoom-in` and `zoom-out`.
- Texts `nav.update`, `nav.updateAvailable`.

Nothing to change in a program that takes 0.2.0 but copying it: no public name of 0.1.0 changed. One thing reads differently: the content of `MatNav.VersionButton` is a text block
now (it trims); a test reads `MatNav.VersionText`. A program drops what it wrote around the gaps (ThixClip: the menu foot it put into the dock of
`MatNav`, its own update link and fold tooltip, its filled icon, its window icons, its control themes of a choice, a card row and a paused button).

## 0.1.0

The first version (2026-11-16): the generated tokens and icons, the palette, the theme with its control themes, the window, the title bar, the menu, the controls, the
texts in English and German, the version, the tray (menu, icons, clicks, notification), the Info window, the setup window and the single instance.
