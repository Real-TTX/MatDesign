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
| Every icon of the sprite as a geometry, and the control that strokes it | `Themes/MatIcons.axaml` (generated), `Controls/MatIcon.cs` | `DESK-09` |
| The window, the title bar with the caption buttons of the system, the menu that folds to a rail | `Controls/MatWindow.cs`, `MatTitleBar.cs`, `MatNav.cs` | `DESK-03`, `DESK-04` |
| Setting rows, form fields, facts, a value to copy, the state as a dot and a word | `Controls/MatSettingRow.cs`, `MatFacts.cs`, `MatCopyField.cs`, `MatStateDot.cs`, `MatState.cs` | `DESK-04`, `TRAY-02`, `COLOR-04`, `AGENT-02` |
| The version, the texts in English and German | `MatVersion.cs`, `MatText.cs` | `SETUP-07`, `DESK-08` |
| The tray: the menu in the order of the rule, the icons with the dot of the state, the clicks, a notification of its own, the Info window | `Tray/MatTrayMenu.cs`, `MatTray.cs`, `MatTrayIcons.cs`, `MatNotification.cs`, `Windows/MatInfoWindow.cs` | `TRAY-01` … `TRAY-05` |
| The setup window for Setup, Update and Uninstall, its steps as building blocks | `Setup/MatSetupWindow.cs`, `MatSetupStep.cs` | `SETUP-01`, `SETUP-02`, `SETUP-04`, `SETUP-07` |
| One instance per user session (a named mutex and a named pipe; .NET makes them a lock file and a Unix socket on macOS and Linux) | `MatSingleInstance.cs` | `DESK-05` |

`Tokens/MatTokens.g.cs` and `Themes/MatIcons.axaml` are written by `node tools/desktop.js` from `src/MatUi/wwwroot/css/matui.tokens.css` and
`src/MatUi/wwwroot/img/matui-icons.svg`; never edit them, change the CSS or the sprite and run the script (CI fails when they are stale). An icon the sprite lacks is
added to the sprite first (`tools/icons.js`), never drawn here.

**The sizes are those of MatUi's compact density with a mouse** (`data-density="compact"` with `pointer: fine`): controls 34 px, body type 14 px, the radii and the
spacing of the tokens. A desktop window is used with a mouse; the touch sizes of the web (44 px) do not apply.

**No colour schemes yet**: `data-scheme` (paper, contrast, oled, `COLOR-09`) is not part of MatDesktop 0.1; the accent text keeps all of the accent, as in the standard look.

## Use it in a program

1. Copy it: `node tools/matdesktop-copy.js <folder of the program's project>` (in MatDesign). Every file gets the first line
   `// MatDesktop 0.1.0 – a copy from MatDesign, do not edit here` (an XML comment in an `.axaml` file), `MatDesktop/matdesktop.version` lists the SHA-256 of each file,
   and the files of an older copy that the package no longer has are removed. The project needs `Avalonia` and `Avalonia.Themes.Fluent` (the tool says so when they
   are missing) and `ImplicitUsings`. Never edit a copy: change MatDesktop here and copy again.
2. Install the theme in `Application.Initialize`, with the app's accent from `matui.apps.css` (`IDENT-01`):

   ```csharp
   public override void Initialize() =>
       MatTheme.Install(this, new MatThemeOptions("#295cff", Appearance: settings.Appearance));
   ```

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
| Control themes | the default `Button` (MatUi's secondary button), `MatButton.Primary`, `.Danger`, `.Ghost`, `.Icon`, `.Link`, `.Caption`, `.CaptionClose`; `MatText.PageTitle`, `.SectionTitle`, `.Muted`, `.Faint`, `.FieldLabel`; `MatCard`, `MatKeyCap`, `MatBadge`, `MatSearchBox`, `MatProgress`; `TabControl` and `TabItem`; `MatNavItem`, `MatSettingRow`, `MatFormField` |
| Icons | `MatIcon.<name>` for every symbol of the sprite (`MatIcon.<name>.Fill` where the sprite fills a part); draw them with `<mat:MatIcon Icon="search" Size="16"/>` (the property is `Icon`: `Name` is the name of an element in Avalonia) |

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
window.UseOwnTitleBar();                                           // in the constructor of the MatWindow
```

The menu is 248 px wide, 210 px in a window narrower than 900 px and 68 px folded; the title bar is 68 px high, its caption buttons 46 × 32 in the shape of Windows 11.

### The tray (TRAY-01 … TRAY-04)

```csharp
var menu = new MatTrayMenu("Example Agent", "1.4.2", quit: () => app.Shutdown())
{
    Open = ShowMainWindow, Info = ShowInfo, Settings = ShowSettings, LogFolder = OpenLogs, State = MatState.Running,
};
menu.AddService(MatServiceAction.Restart, RestartService);         // with the shield (administrator rights)
var tray = new MatTray(app, menu, mark);                           // mark: the app's mark, 64 px or more
menu.State = MatState.Waiting; tray.Refresh();                     // the dot, the tooltip, the second line of the menu
MatNotification.Show("Connection lost", "It tries again every minute.", isWarning: true, onClick: ShowMainWindow);
```

`MatTrayMenu.Build()` puts the items in the order of `TRAY-01` whatever order the program gives them, leaves out what it does not have, never shows a separator twice and has
no Uninstall item; `Install update …` is there only while `InstallUpdate` is set. Avalonia's menu cannot draw the default item in bold: it is the first item that acts, and
a left click on Windows runs it (`TRAY-03`). `MatTrayIcons` draws the dot of the state into a cut-out of the mark (in colour, or as a shape on the template image of macOS).

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
    { Logo = logo, Finish = MatSetupStep.Finish("Its icon is in the notification area.") });
setup.Finished += (_, _) => { if (setup.StartNow) StartProgram(); };
```

The window is 600 × 560 and cannot be resized; the footer shows `Version 1.4.2 · release` at the left and the primary action, Back and Cancel at the right. Progress cannot
be cancelled or closed. `MatSetupMode.Update` asks nothing and names the installed and the new version (`InstalledVersion`); `MatSetupMode.Uninstall` needs
`MatSetupStep.Uninstall("what goes")` and adds "Also delete my data" (off) and a danger button.

### A tray that stays WinForms (AGENT-05)

`node tools/matdesktop-copy.js <folder> --tokens-only` copies `Tokens/MatTokens.g.cs`, `Tokens/MatColor.cs` and `Tokens/MatPalette.cs`, which need nothing but System:

```csharp
var palette = MatPalette.For("#295cff", dark: false);
header.BackColor = Color.FromArgb(palette.Surface.R8, palette.Surface.G8, palette.Surface.B8);
var line = palette.Border.Over(palette.Surface);                   // WinForms has no transparency: paint a line over its surface first
```

`MatText`, `MatVersion`, `MatState` and `Tray/MatTrayMenu.cs` need nothing but System either: a WinForms tray may build its `ContextMenuStrip` from `MatTrayMenu.Build()`.

## Pictures

`MATDESKTOP_SHOTS=<folder> dotnet test tests/MatDesktop.Tests --filter GalleryTests` draws every window of this library without a screen from fixed sample data (the sample
program "Matmon Agent", the computer `ANNA-PC`, version 1.4.2), in light and dark: the site shows them on the pages of Desktop and services (`matdesktop-*.png`).

## Versions

The version is `MatDesktopVersion` in `MatDesktop.csproj` and `MatDesktopInfo.Version` (a test holds them equal). A change that a program can see raises it and gets a
line here.

## 0.1.0

The first version (2026-11-16): the generated tokens and icons, the palette, the theme with its control themes, the window, the title bar, the menu, the controls, the
texts in English and German, the version, the tray (menu, icons, clicks, notification), the Info window, the setup window and the single instance.
