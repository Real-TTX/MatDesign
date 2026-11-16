# Desktop programs, the tray, the setup and the agents

MatDesign was about web apps. This guide is about the programs of the family that are not web pages: a **desktop program** with windows (the model is ThixClip, Avalonia, Windows and
macOS), its **tray icon** and its **setup**, and the **agents**, programs that run as a service on a machine and show themselves through a tray and a setup (the Matmon Agent, the Windows
instance of Matmon, the Matgate Agent, the MatHub Agent). The rules are in the rulebook in four areas, `DESK`, `TRAY`, `SETUP` and `AGENT` (decided by the owner on 2026-11-15); the site
explains them under **Desktop and services** (`/Desktop`, `/Desktop/App`, `/Desktop/Tray`, `/Desktop/Setup`, `/Desktop/Agents`). This file says what each rule means in practice.

Nothing of it is in `src/MatUi`: the toolkit is CSS and a script for web pages. What a desktop program needs is **MatDesktop** (`src/MatDesktop`, section 1a): the theme with the tokens,
the window chrome, the menu, the tray, the Info window, the setup window and the controls as one Avalonia library, which programs copy like web apps copy MatUi.

## 1. What applies to which program

| Program | DESK | TRAY | SETUP | AGENT |
| --- | --- | --- | --- | --- |
| A desktop program (ThixClip, ThixCalendar) | all | when it has a tray | per user, no rights | no |
| An agent or a service (Matmon Agent, Matgate Agent, MatHub Agent) | its windows: tokens, icons, keyboard (`DESK-02`, `DESK-07`, `DESK-09`) | all | administrator rights once | all |

Where the programs stand on 2026-11-16, and what they still have to change, is at the end of this file.

## 1a. MatDesktop, the toolkit for desktop programs

MatDesktop is to a desktop program what MatUi is to a web page: one Avalonia library (Avalonia 11.3.22, .NET 10) in `src/MatDesktop`, built and tested in this repository
(`tests/MatDesktop.Tests`, headless with Skia), not part of the site or its image. Its README has the details and the code; in short:

- **Copy, do not reference** (`CODE-03`). `node tools/matdesktop-copy.js <folder of the program's project>` writes the files into `<folder>/MatDesktop/`, each with the line
  `// MatDesktop <version> – a copy from MatDesign, do not edit here` (an XML comment in `.axaml`), a manifest `matdesktop.version` with the SHA-256 of each file, and removes the files of an
  older copy. The version is `MatDesktopVersion` (0.2.0; its README lists what each version brought).
- **Generated from MatUi, never by hand.** `node tools/desktop.js` writes `Tokens/MatTokens.g.cs` from `matui.tokens.css` (the colours of light and dark, the sizes, the type, the motion and
  the rules by which the accent tokens are derived) and `Themes/MatIcons.axaml` from the sprite (every symbol as a geometry `MatIcon.<name>`), and the same icons as plain segments for WinForms (`Tokens/MatIconPaths.g.cs`: MoveTo, LineTo, CubicTo, Close; a test holds them to the geometries). CI runs it and fails on a difference.
  **A desktop window is used with a mouse, so the sizes are those of MatUi's compact density with a fine pointer**: controls 34 px, body type 14 px, the radii and the spacing of the tokens.
  Colour schemes (`COLOR-09`) are not part of 0.2.
- **The theme** (`DESK-02`). `MatTheme.Install(app, new MatThemeOptions(accent))` in `Application.Initialize` adds Fluent in the colours of MatUi, a brush per colour token in a light and a
  dark dictionary (`--mat-surface-2` is `Mat.Surface2`; the accent tokens computed from the app's accent like the CSS computes them, `MatPalette`), the sizes (`Mat.Radius`,
  `Mat.ControlHeight`, `Mat.FontSize`, `Mat.Font` ...) and the control themes of the flat look: the default button (MatUi's secondary), `MatButton.Primary`, `.Danger`, `.Warning`, `.Ghost`, `.Icon`,
  `.Link`, the text looks `MatText.PageTitle` ..., `MatCard`, `MatKeyCap`, `MatBadge`, `MatBadge.Soft`, `MatSearchBox`, `MatProgress`, the tabs, and the lists `MatList` with `MatChoiceRow`
  or `MatCardRow` (chosen: a tint with the accent line all around, `COLOR-08`). `MatThemeOptions.Icon` is the icon of every window. `MatTheme.SetAppearance` is System, Light or Dark.
  A window derives from `MatWindow` and uses `{DynamicResource Mat.Surface}`, never a literal colour.
- **The chrome and the controls**: `MatTitleBar` (`DESK-03`: logo and name, a slot in the middle, the caption buttons of Windows 11 drawn by the program, room for the traffic lights on
  macOS, drag and double click), `MatNav` and `MatNavItem` (`DESK-04`: the current item a tint, Settings below a line, the version at the foot that opens About, the fold to a rail of
  68 px; a place of the program's own at its foot, a link while an update waits, the fold shortcut in the tooltip), `MatIcon` (`DESK-09`, `IsFilled` for a chosen star), `MatStateDot` (a dot and a word), `MatSettingRow`, `MatFormField`, `MatFacts`, `MatCopyField`, `MatVersion` (`SETUP-07`), `MatText` (every text of the library
  in English and German, `DESK-08`), `MatSingleInstance` (`DESK-05`).
- **The tray**: `MatTrayMenu` builds the menu of `TRAY-01` in its order whatever order a program adds its items in (and has no Uninstall), `MatTray` shows it with Avalonia's tray icon and
  handles the clicks of `TRAY-03`, `MatTrayIcons` draws the dot of the state (`TRAY-02`), `MatNotification` is the notification of `TRAY-04` and `MatInfoWindow` the Info window of `TRAY-05`; `MatDialogChrome` is the header and the footer of Info and the setup, for a program's own dialogs too.
- **The setup**: `MatSetupWindow` is the one window of `SETUP-01` for Setup, Update and Uninstall, with the steps of `SETUP-02` as building blocks (`MatSetupStep.Welcome`, `Role`, `Connect`,
  `WithOptions`, `Finish`, `Uninstall`; Progress is the window's) and the work as a callback that reports its steps.
- **A tray that stays WinForms** (`AGENT-05`) takes the part without Avalonia: `tools/matdesktop-copy.js <folder> --winforms` copies the tokens (`MatPalette.For(accent, dark).Surface` ...),
  the icons of the sprite as segments (`MatIconPaths`, stroked into a `GraphicsPath`: the README has the lines, instead of Segoe Fluent glyphs, `DESK-09`), `MatText`, `MatVersion`, `MatState`,
  `MatTrayMenu` and `MatSingleInstance`; `--tokens-only` takes the three files of the tokens.

## 2. Desktop programs (DESK)

- **Avalonia, once for every system** (`DESK-01`, Should). C#, Avalonia 11, the current .NET (10), one project for Windows, macOS and Linux. What differs (title bar, tray, autostart,
  paths, installer) sits behind `OperatingSystem.IsWindows()` / `IsMacOS()` in one class each, never in a second project. WinForms and WPF are not used for new programs.
- **Colours from the tokens** (`DESK-02`). `matui.tokens.css` is the source of names and values; MatDesktop generates them into its theme, a light and a dark dictionary whose brushes are
  named after the tokens (`Mat.Surface`, `Mat.Surface2`, `Mat.Border`, `Mat.Text`, `Mat.TextMuted`, `Mat.Accent`, `Mat.AccentSoft`, `Mat.Success`, `Mat.Warning`, `Mat.Danger` ...); a window uses
  `{DynamicResource ...}` and never a literal colour. A program writes no theme file of its own. The appearance is a setting **System / Light / Dark** ("Erscheinungsbild": Wie das System, Hell, Dunkel),
  saved per user; System follows the operating system and changes with it. Selected is a tint, never a bar (`COLOR-08`).
- **A title bar of its own** (`DESK-03`). `ExtendClientAreaToDecorationsHint` with the chrome hints that the system needs. Left: the logo (32 px) and the name or word mark. Middle: the search
  or the main actions (ThixClip: the search, Pause, the global shortcut as a chip). Windows: minimise, maximise/restore and close at the right, drawn by the program in the shape of the
  system, each with a tooltip and a name (`Schließen (Esc)`). macOS: the traffic lights belong to the system; the program hides its own caption buttons and leaves room at the left. The bar is dragged with `BeginMoveDrag`, a double click
  maximises. `Title` is still set (task bar, Alt+Tab, screen readers).
- **The menu of the web** (`DESK-04`). The left menu of the shell (`SHELL-01`): items with icon and label, the current one tinted, a separator, Settings. At the foot the version
  (`Version 1.4.2 · release`, `SETUP-07`), which opens About, and the button that folds the menu to a **rail of 68 px** (remembered; the open menu is 248 px, 210 px in a window narrower than 900 px).
  Settings and About are pages of the main window. Settings are rows of a title, a sentence and a control at the right, grouped in tabs when there are many (ThixClip: General, Recording,
  Cloud, Updates, Advanced).
- **One instance** (`DESK-05`). A named mutex and a named pipe per user (ThixClip: `ThixClip.ShowWindow.<user>`); the second process writes to the pipe (show the window) and ends. On
  macOS and Linux a lock file in the data folder and a Unix socket do the same.
- **Close hides, Quit ends** (`DESK-06`). `ShutdownMode.OnExplicitShutdown`. Close, Escape in the main window and Alt+F4 hide the window. Only **Quit / Beenden** in the tray menu (and the app
  menu of macOS, ⌘Q) ends the program. Started by autostart (`--autostart`, `--tray`) the program starts hidden in the tray.
- **The keyboard** (`DESK-07`). Tab order, arrows in lists, Enter for the default action, Escape to close or go back, a visible focus ring in the accent. A shortcut is in the tooltip:
  `Anheften (Strg+P)` / `Pin (Ctrl+P)`, on macOS `⌘P`. A list may name its keys in a line at its foot (ThixClip: ↵ Einfügen, ↑↓ Auswählen, Strg D Favorit ...). Icon-only buttons have
  `AutomationProperties.Name` and a tooltip with the same words.
- **German and English** (`DESK-08`, Should). Resources in both languages, chosen by `CultureInfo.CurrentUICulture` (German for `de-*`, English otherwise); a setting may override it.
- **Icons from the sprite** (`DESK-09`). MatDesktop carries every symbol of `matui-icons.svg` as a geometry (`MatIcon.search`, `MatIcon.pin` ..., generated by `tools/desktop.js`), which the
  control `MatIcon` strokes like the web does (2 units on the grid of 24, round caps). No Segoe Fluent Icons, no Font Awesome, no emoji. An icon the sprite lacks is added to MatDesign first.
  The app icon, the tray icon and the logo are drawn from the same mark (`tools/render-icons.js`).
- **Policies** (`DESK-10`, May). Windows: `HKLM\SOFTWARE\Policies\<Product>` (DWORD values), macOS: a configuration profile for the bundle id. Names say what they do: `LockConfiguration`,
  `DisableCloud`, `DisableUpdates`. A locked setting is shown, turned off, with "Von deiner Organisation festgelegt" / "Set by your organisation".

### Window sizes

Sizes in device-independent pixels.

| Window | Size | Notes |
| --- | --- | --- |
| Main window | 1180 × 760 at the first start, at least 760 × 460 | size and place remembered |
| Title bar | 68 high | logo 32 |
| Menu | 248 wide (210 under 900), rail 68 | |
| Setup | 600 × 560, fixed | `SETUP-01` |
| Info window of a tray | about 630 × 500 | `TRAY-05` |
| Notification of the program's own | 360 wide, 7 s | bottom right above the task bar, a click opens the program |
| A small dialog (Pause) | 400 wide | Cancel and the primary button at the bottom |

## 3. The tray (TRAY)

**The menu** (`TRAY-01`), in this order; items a program does not need are left out, the order stays:

| # | English | German | When |
| --- | --- | --- | --- |
| 1 | `<App> 1.4.2` (disabled) | `<App> 1.4.2` | always |
| 2 | the state in words (disabled) | `Läuft`, `Wartet`, `Gestoppt`, `Nicht eingerichtet` | always |
| | separator | | |
| 3 | **Open `<App>`** (bold, default) | **`<App>` öffnen** | always: the window, or the web page, or Info |
| 4 | the program's own items (recent entries, Pause / Resume) | `Pausieren` / `Fortsetzen` | between separators |
| 5 | `Info …` | `Info …` | |
| 6 | `Set up …` / `Settings …` | `Einrichten …` / `Einstellungen …` | |
| 7 | `Open log folder` | `Log-Ordner öffnen` | |
| 8 | `Install update …` | `Update installieren …` | only while an update waits |
| | separator | | |
| 9 | `Start service …`, `Stop service …`, `Restart service …` with the shield | `Dienst starten …`, `Dienst beenden …`, `Dienst neu starten …` | services only |
| | separator | | |
| 10 | `Quit` | `Beenden` | always, last |

There is **no Uninstall** in a tray (owner, 2026-10-30): a program is removed in Installed apps, which opens its setup (`SETUP-04`). An agent's tray may offer "Hide icon" ("Symbol
ausblenden") before Quit; it ends the tray like Quit and is undone in the setup.

**The states** (`TRAY-02`) are a dot at the bottom right of the icon, with a gap between the dot and the mark (a cut-out, so that it works on any task bar), drawn at runtime: green running (`--mat-success`), amber waiting or attention (`--mat-warning`), red stopped or error (`--mat-danger`), grey not set up. A desktop program may add its own (ThixClip:
paused, a pause sign in amber). The state is also in words: the second line of the menu, the tooltip (`Monitoring Agent: connected`) and the Info window. macOS uses a template image (black
on transparent, `@2x` too); a state there is a black shape (a pause sign), not a colour. Windows uses an `.ico` with 16, 20, 24, 32, 40, 48, 64 and 256 px.

**Clicks** (`TRAY-03`): Windows, left click = the default item, right click = the menu; macOS and Linux, any click = the menu.

**Notifications** (`TRAY-04`) only for what the user did not cause: the connection was lost or came back, an update is ready, an approval came through. Never for Pause, a restart or a
setting the user just changed; never with a token. A notification of the program's own (Avalonia has none of the system on every platform) is 360 px, stays 7 s, and a click opens the
program.

**Info** (`TRAY-05`) is the one window of a tray: a header (logo, name, "Info"), the state as a dot, a word and one sentence, the facts as rows (machine, server or port, mode, started,
version, data folder, what else matters), and the footer with `Version 1.4.2 · release` at the left and **Open** (if there is a page), **Log folder**, **Close** at the right. Further
windows (connection details, set up) are reached from the menu or from a button of Info.

## 4. The setup (SETUP)

**One window** (`SETUP-01`) for Setup, Update and Uninstall ("Einrichtung", "Update", "Deinstallation"): 600 × 560, fixed. Header: logo (44), the name, the mode. Footer: `Version x ·
channel` at the left, the buttons at the right: the primary action, Back, Cancel. The look is a wizard of the web (`FORM-26`): one step, its title, what it asks.

**The steps** (`SETUP-02`): **Welcome** (what will happen, where to, with which rights) → **Role** (only if the program has roles: the Matmon instance asks for Server or Probe) →
**Connect** (only where it pairs, `AGENT-02`: the address and the code, checked at once) → **Options** (switches with the safe choice set: start at sign-in, desktop shortcut, automatic
updates; for a service the port, the firewall rule, run as user / system / service) → **Progress** (a bar, the list of steps with ✓; no Cancel, the window cannot be closed) →
**Finish** ("`<App>` ist bereit", "`<App>` jetzt starten" on; an agent that others connect to shows host, port, password and fingerprint, each with Copy). An **update** asks nothing:
Welcome says which version is installed and which comes, then Progress and Finish; the settings are kept.

**Rights** (`SETUP-03`). A desktop program installs **per user without administrator rights** to `%LocalAppData%\Programs\<Product>` (macOS `~/Applications`), the single file is its own
setup: started from anywhere else it shows the setup, `--uninstall` removes it. The Welcome step says it: "Wird für dein Benutzerkonto installiert – ohne Administratorrechte – nach ...".
An agent or service needs administrator rights, says so on Welcome and elevates once when the install starts (UAC); the tray and Info never run elevated.

**Installed apps** (`SETUP-04`). Every program writes its uninstall entry (per user under HKCU for a desktop program, HKLM for a service): DisplayName, Publisher, DisplayVersion,
DisplayIcon, EstimatedSize, UninstallString, QuietUninstallString. The uninstall opens the same window in the mode Uninstall: what will be removed, **"Auch meine Daten löschen" /
"Also delete my data"** off (ThixClip: "Auch meinen Verlauf löschen"), a danger button ("Deinstallieren" / "Entfernen"), Cancel.

**Without a window** (`SETUP-05`):

| Command | What it does |
| --- | --- |
| `<product>.exe --quiet` | a desktop program installs itself for the user with the default options |
| `<product>.exe --uninstall [--quiet]` | removes it (quiet: without a window, the data kept) |
| `<product>.exe --autostart` / `--tray` | how autostart starts it: hidden, in the tray |
| `<product>-agent install --quiet --url <server> --code <code>` | an agent enrols, then installs the service and the tray |
| `<product>-agent uninstall [--delete-data]` | removes the agent; its data (token, certificate, log) stays unless `--delete-data` (`SETUP-04`) |
| `msiexec /i <product>-agent.msi /qn URL=<server> KEY=<rollout key>` | the same as an MSI, for Intune, GPO and the like |
| the Matmon instance: `setup.exe /silent ...` | the existing form; new setups use `install --quiet` |

Exit code 0 when it worked, anything else with a message on stderr and in the log. A quiet install that lacks a value fails at once.

**Updates** (`SETUP-06`). A manifest per channel (`release`, `nightly`):
`{ "version": "1.4.2", "channel": "release", "buildNumber": 212, "packages": { "win-x64": { "file": "<Product>Setup.exe", "sha256": "..." }, "osx-arm64": { ... } } }`. The program downloads,
checks the SHA-256, runs nothing that differs, starts the new version, which has to confirm its start (a file or a call) within a set time; otherwise the old version is put back and
the version is marked failed, so that it is not tried again and again (the agents keep `pending.json` and `last-update.json` for it). Users and the policy `DisableUpdates` can switch automatic updates off.

**The version** (`SETUP-07`) reads `Version 1.4.2 · release` (`Version nightly-212-20261115 · nightly`, `Version local-20261115 · local`) at the foot of the menu, in About, in the footer of
the setup, in Info, and in Installed apps; the first line of the tray menu is the name and the number. A local build says that it gets no updates.

## 5. Agents (AGENT)

**Names** (`AGENT-01`), for a product called Matmon:

| What | Name |
| --- | --- |
| Service and display name | `Matmon Agent` |
| Program | `matmon-agent.exe` (`matmon-agent` on Linux and macOS) |
| Autostart value of the tray (HKLM\...\Run) | `Matmon Agent Tray` |
| Data on Windows | `%ProgramData%\Matmon Agent` (an ACL of SYSTEM and Administrators, not inherited) |
| Data on Linux | `/etc/matmon-agent`, `/var/lib/matmon-agent`; the unit `matmon-agent.service` |
| Status channel | the pipe `matmon-agent-status` |
| Start menu, Installed apps | `Matmon Agent` |

**Pairing** (`AGENT-02`). The server gives out an **enrolment code** (single use, short-lived, `K7QM-4TXC-9RHA-B2NW-P6FE`) or a **rollout key** with a prefix (`mmr_...`) for many machines,
which may wait for an administrator's approval (the tray shows Waiting, amber). The setup **enrols first and installs after**: a bad code installs nothing. An agent that is itself the
server (the Matgate Agent: Matgate connects to it like to an RDP host) shows host, port, password and the SHA-256 fingerprint of its certificate on Finish and in "Connection details",
each with Copy.

**Secrets** (`AGENT-03`). The token stays in the service and its data folder. The status pipe carries a snapshot (state, version, server, last contact) and never the token, so any local
user may read it. No secret in a notification, a log, Info or the tooltip. Where the user must copy one, it stands in one window with Copy and the sentence "Das Token ist der einzige
Schlüssel zu diesem Rechner – nur in Matgate eintragen."

**Service and tray** (`AGENT-04`). The work is a Windows service, a systemd unit or a launchd daemon. The tray is a separate process in the user's session (autostart `<Product> Agent Tray`),
it reads the pipe and asks the service, elevated, to start, stop or restart. Quit ends the tray only.

**WinForms** (`AGENT-05`, Should). The trays and setups of the Matmon Agent, the Matmon instance and the Matgate Agent are WinForms and may stay so. They share one chrome (a header
of 84 px on the surface with the mark, a footer of 58 px on the second surface with the version at the left and the buttons at the right) and follow TRAY, SETUP and AGENT; their colours come from the tokens. A new tray,
setup or agent is Avalonia.

## 6. Pictures of the windows

Every desktop program and agent can **draw its windows into PNG files without a screen**, from fixed sample data and a pinned clock, **never from the data of the machine it runs on** (no real
computer name, user name, path or token). The pictures in each README, in `docs/images` and on the pages of Desktop and services are made that way, again after every change, in light and
dark mode:

- ThixClip (on MatDesktop): `ThixClip.exe --snapshot <folder>` writes `setup-welcome|options|progress|finish|update|uninstall-{light,dark}.png`, `main-history|settings|about|folded|paused-{light,dark}.png`,
  `dialog-pause|clear-history|viewer-{light,dark}.png`, `toast-{light,dark}.png` and the tray icons `tray-icon-windows[-paused].png`, `tray-icon-macos[-paused].png` (sample computer `ANNA-PC`, version 1.0.0).
  The headless UI tests (`Avalonia.Headless`) use the same sample data.
- Matgate Agent: `matgate-agent setup-preview <folder>` draws the setup steps, the uninstall, Info, the connection details and the tray menu (WinForms `DrawToBitmap`), light and dark,
  from sample data only (computer `ANNA-PC`, user `Anna`, a sample password, version 0.8.0.42). This site shows its Info, Finish with the connection details and Copy, the connection
  details, the update and the uninstall, each light and dark.

- MatDesktop: its tests draw every window of the library with a sample agent (`Matmon Agent`, computer `ANNA-PC`, version 1.4.2) when `MATDESKTOP_SHOTS=<folder>` is set:
  `dotnet test tests/MatDesktop.Tests --filter GalleryTests` writes `matdesktop-{info,setup-welcome,setup-connect,setup-options,setup-progress,setup-finish,setup-uninstall,main-open,main-folded,notification}-{light,dark}.png`
  and the tray icons `matdesktop-tray[-macos]-{running,waiting,stopped,notsetup}.png`.

On this site the pictures are data: `src/MatDesign.Web/Services/DesktopFigures.cs` (page, file, alt text, caption, size, rules, the dark twin) and the files in
`src/MatDesign.Web/wwwroot/img/desktop` (named `app-*` for the desktop program, `agent-*` for the agent, `matdesktop-*` for MatDesktop). A new picture is a line there and its files; `DesktopPagesTests` holds the list, the
files and their sizes together. A pair is shown by the theme of the site (two `img` with `mat-brandmark--light` / `--dark`, lazy, so the hidden one is not loaded). The pictures are sample
material of the demo: on another template the pages show only their drawn specimens, with the company's names (`IDENT-09`).

## 7. Where the programs stand (2026-11-16)

- **MatDesktop** 0.2.0 (the toolkit, section 1a). ThixClip is on it.
- **ThixClip** is on MatDesktop (branch `feature/matdesktop`) and follows DESK, TRAY and SETUP. Left: German only (`DESK-08`); the DisplayVersion in Installed apps lacks the channel
  (`SETUP-07`); two notifications are for events the user caused (`TRAY-04`); macOS is not checked on a Mac.
- **ThixTunnel** differs: dark only, the Fluent icon font, its own colours, separate windows, a tray icon drawn in code with a status dot. It needs the tokens (`DESK-02`), the sprite
  (`DESK-09`), the menu in pages (`DESK-04`).
- **Matmon Agent and the Matmon instance**: the tray and the setup take their colours from the tokens. Left: light only (`DESK-02`); an update still asks through Options; administrator
  rights are asked at the start; the setup of the agent is not a wizard (`SETUP-02`); the progress bar is the green one of the system. The tray of the instance has no log folder, by design.
- **Matgate Agent** (branch `feature/agent-desktop-rules`): the tray menu in the order of `TRAY-01`, the amber state (`TRAY-02`), Quit, the tokens in light and dark; `setup-preview`
  draws sample data only (section 6). Left: German only (`DESK-08`); it knows no address of Matgate, so the default item is Info instead of Open (`TRAY-01`); the mode as the signed-in user
  is a sign-in task, not a service (`AGENT-04`).
- **MatHub Agent** (branch `feature/agent-desktop-rules`): an entry in Installed apps (`SETUP-04`), a tray with the state, Info and Quit (`TRAY-01`, `TRAY-02`, `TRAY-05`), the tokens.
  Left: light only (`DESK-02`); Segoe glyphs instead of the sprite (`DESK-09`; MatDesktop 0.2.0 has the sprite's icons as plain C# segments for WinForms, `MatIconPaths`); no setup
  window, the command line only (`SETUP-01`); no MSI (`SETUP-05`). Its start menu entry is "MatHub Kiosk": a decision of the owner against `AGENT-01`.
