# CLAUDE.md

Guidance for working in this repository. Keep it in sync with the code: update it whenever architecture, interfaces or the way the rules are
checked change.

## What MatDesign is

The design and identity of **all Mat apps**, as a website and a complete UI toolkit: the **rulebook** (every rule about how an app looks and behaves, with
stable IDs), the foundations (tokens, type, icons), the shell, every control with live examples, the page patterns, an accent builder, and **MatUi**, the toolkit
itself (CSS, one script, an icon sprite) that the apps copy. It is the place to look before building or changing any UI in the family.

Stack: ASP.NET Core on **.NET 10**, C#, **Razor Pages**, plain CSS and plain JavaScript (no build step), Docker-first. Web port **8060**. The site keeps no state
(no database, no secrets); the image runs as an unprivileged user. The one exception is the **Studio** (docs/STUDIO.md, built stage by stage): it writes to `/data` and is on when
`MATDESIGN_DATA` is set (the image sets it). Its first administrator is created by the **setup assistant** on the first visit (`/Studio/Setup`; a name and a password, kept as a hash in `admin.json`),
so **the release `docker-compose.yml` has no variables and no `.env` file: keep it that way, do not bring a password or a port variable back**. `MATDESIGN_ADMIN_PASSWORD` is only an optional shortcut for
automation. The browser checks create the administrator when they find none (`STUDIO_NAME` / `STUDIO_PASSWORD`, default `admin` / `studio-dev`).

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before changing the design and [docs/ADOPTING.md](docs/ADOPTING.md) for what an app has to do to use MatUi.

## Build / run / test

- **Build:** `dotnet build MatDesign.slnx` (note: `.slnx`). SDK 10 (see `global.json`). Expected: 0 warnings, 0 errors.
- **Tests:** `dotnet test`: xunit, no Docker, no browser. It also starts the whole site in memory and crawls it. **Browser checks:** `cd tests/e2e && npm install && node run.js http://localhost:8060`
  (Playwright against the running container; CI runs it too). Add a check there when behaviour of `matui.js` changes: reading a script proves little.
- **Run / test the site: always rebuild the container and redeploy the stack** (project rule): `./scripts/redeploy.ps1` (`-Port` changes the host port).
  Dev stack: `docker-compose.dev.yml`, container `matdesign-dev`, port 8060, explicit subnet `10.253.60.0/24` (the development PC has all Docker address pools used up:
  `all predefined address pools have been fully subnetted`; change it with `MATDESIGN_DEV_SUBNET`). CSS, JS and cshtml are baked into the image: rebuild after a change.
- **Release stack:** `docker-compose.yml`, image `ghcr.io/real-ttx/matdesign`, one service and one volume (for the Studio): keep it that simple.
- **Real tests are end to end against the container:** Playwright with the installed Edge (see the `screenshots` skill), then look at the screenshot instead of trusting
  computed styles. `MSYS_NO_PATHCONV=1` for Git Bash arguments that start with `/`. Shell strings with backticks, apostrophes or `$$` get mangled: write files with
  the editor tools, not with `node -e` or heredocs. A script that merges text into a file must use a replacer function (`text.replace(a, () => b)`): in a plain replacement string
  `$$` becomes `$` (it turned `$$(...)` of the script into `$(...)` once).
- **Icon sprite:** `node tools/icons.js` rebuilds `src/MatUi/wwwroot/img/matui-icons.svg` from `tools/icons.base.json` plus the extras in the script (CI fails when it is stale).
- **Icon library (optional, ICON-05):** `node tools/iconlib.js` builds `src/MatUi/wwwroot/iconlib/` (sprite `mat-il-*`, catalogue, licence, README) from `tools/iconlib/`: `lucide.json` (vendored Lucide, written by
  `import-lucide.js <checkout>` when Lucide is updated), `custom.json` (icons drawn for MatDesign: one line each, with tags and categories; look at them on a contact sheet before keeping one) and `categories.json`
  (General, Home automation and its areas as curated lists). An icon can be in any number of categories. Nothing in `matui.css`/`matui.js` may refer to the library (a test checks); CI fails when the committed files are stale.
- **Audit:** `node tools/audit.js [folder with the repositories]` rewrites `src/MatDesign.Web/Data/audit.json`, a snapshot of how close the other apps are to the rules.

## Map

```
src/MatUi                  Razor class library = the toolkit. wwwroot/css (10 parts), js/matui.js + matui.boot.js, img/matui-icons.svg
src/MatDesign.Core         no web types
  Colors.cs                  OKLab, mixing like CSS color-mix, WCAG contrast, distance
  AppRegistry.cs             the apps and their accent, AccentPresets
  AccentReport.cs            what the toolkit derives from an accent + the contrast checks
  PaletteAdvisor.cs          accents that are too close, proposals
  CssTokens.cs               parser for matui.tokens.css (tokens, description from the comment, group from the heading)
  RuleBook.cs                the rules (id, area, level, source, text)
  Studio/StudioStore.cs      what the Studio saves: an append-only log per item (studio/<kind>/<id>/<revision>.json), history, restore, activity
  Studio/AdminAccount.cs     the administrator: a name and a salted PBKDF2 hash in admin.json (created once, never overwritten), what the setup assistant accepts, Verify
  Studio/AppValidator.cs     what an app has to be (id, name, glyph, port, contrast), Normalize, IsSafe
  Studio/RuleValidator.cs    what a rule has to be (an id of its area, title, statement, level, source), Normalize, IsSafe, NextId
  Studio/Diff.cs             what changed between two revisions: the fields (Flatten, Changes) and the lines (Lines)
  Studio/MarkupChecks.cs     the contract of the markup of an example (classes, icons, no inline/outside/script, labels, ids ...): the unit tests and the Studio use this one code
src/MatDesign.Web          the site
  Program.cs                 /ui/* (the toolkit as files, bundle, archive), /releases/* (the releases of the Studio), /api/*, /health, Razor Pages, the cookie sign-in of the Studio
  Studio/VersionTracker.cs   which release of MatUi each app uses (written down by hand) and where that stands against the releases
  Studio/Styleguide.cs       the static styleguide page of a release (rules, components with live examples, apps), its stylesheet and the sample picture
  Studio/Releases.cs         the release pipeline (Check, Version, Build, Publish, Notify), the run records, the releases on the volume and the files they serve
  Studio/                    StudioService (on or off, the administrator and the setup, the lock-out), StudioSession (what the session cookie carries), DesignData (built-in + Studio items: the apps, rules and components every page reads; Version), ComponentValidator, StudioHistory (restore checks),
                             DesignChecks (every check of the design system, one report per version of the data: the Checks page, the menu badge and the release pipeline), StudioFormat
  Pages/Studio/              Setup (the assistant of the first administrator), Login, Logout, Index, Activity, History, Checks, Releases/ (list, New, Run), Versions/ (list, Edit), Apps/, Rules/ and Components/ (list, edit); StudioPageModel is the base of the pages that need the administrator
  Services/                  ToolkitFiles, IconLibrary (the optional icon library: sprite, catalogue, single icons, archive, served under /ui/iconlib/), Ui (icons, logo, swatch), ShellModel, Navigation, Crumbs, ListModels (toolbar, pager, query),
                             ComponentCatalog (+ FormComponents, PickerComponents, ContentComponents, LayoutComponents; CatalogSection = what a page shows),
                             ComponentApi (classes and attributes of an example), ContextPages (the real page and the marks of each group),
                             PatternCatalog, ShellSkeleton, AuditData (Navigation: NavSet > NavGroup > NavItem with NavBadge and children)
  Pages/                     Index, Rules, Identity, Foundations, Shell, Components, Layout, Utilities, Patterns, Builder, Download, Preview (the sample app)
  Pages/Shared/              _Layout (the shell), _Bare (no menu), _Menu (items, badges, submenus; the switcher under the logo), _MenuSwitcher (the switcher button, under the logo or in the foot:
                             `ShellModel.SwitcherPlace`), _MenuSwitchPopup (its popup; menus grouped below a `NavSetParent`), _Head, _Toolbar, _Pager,
                             _DocSection + _Toc + _Component + _ContextPage (the components, layout and utilities pages), _AccentSwatches, _ContrastCard, _FoundationsTabs
  wwwroot/css/site.css       the few documentation-only styles (doc-*); wwwroot/js/doc.js the "Try it" buttons, where the code is shown and the group-in-a-page panels
tests/MatDesign.Tests      unit tests + the site in memory
tests/e2e                  run.js: the behaviour in a real browser against a running site
tools/                     icons.js, iconlib.js (+ iconlib/: the sources of the icon library), audit.js
```

## Conventions

**The rules are the product.** A rule has an ID (`AREA-NN`), a level (Must / Should / May), a source and, where a test enforces it, a `Check`. The rules the owner stated
in their own words have the source `Owner` and are not weakened without asking. When a rule changes, change `RuleBook.cs`, the CSS or script that implements it and the test that
checks it in one commit. Rule text that cites another rule must cite one that exists (a test checks).

**The toolkit**
- One code base per control: the site draws itself with the same layout and partials as the sample apps (`/Preview`). A page is built only from toolkit classes: **no inline
  styles, no inline scripts, no event-handler attributes, no literal colours outside `matui.tokens.css`/`matui.apps.css`**. A colour that is data (a swatch) is an SVG `fill`
  attribute (`Ui.Swatch`); a width that is data is set by a class or by the script (`data-value` on the meter).
- Names: classes `mat-*` (block, element, modifier: `mat-card__head`, `mat-btn--primary`), state classes `is-*`, properties `--mat-*`, behaviour by `data-*` attributes.
  Site-only classes are `doc-*` and live in `site.css`.
- The dark set is written twice (`:root[data-theme="dark"]` and the system media query) and the two must stay identical. `data-app` and `data-theme` belong on `<html>`.
- Order inside a button row, a list block and the toolbar is enforced by CSS (`order`), not by the author. A selector in a later part must be more specific than a base class it
  overrides (the bundle puts `controls` after `shell`).
- Pickers (`FORM-15`): a wrapper `mat-picker[data-picker="select|date|daterange|time|timerange|colour|tags"]` around the real input `mat-picker__input`, which posts one value in a fixed
  format (`2026-10-10`, `..` for a range, `09:30`, `#b4235f`, the tags joined by a comma); the script adds the button and the panel (`position: fixed`, full screen with an X on a phone; `tags` is
  `inline`: a field of chips, no button, its suggestions a dropdown under it). Texts in English and German
  (`PK_TEXT`: both languages must have every key, a test checks). A hidden element that has a `display` in the CSS needs its own `[hidden]` rule (tests list them).
- Layout and utilities: mobile first with `sm` 600, `md` 900, `lg` 1200 px; no `!important`; values from the tokens; the grid spans by `grid-column-end`. An example in the catalogue uses
  toolkit classes only, never `doc-*` (it is copied into other apps).
- Icons come from the sprite (`Ui.Icon("name")` / `#mat-i-name`); a name that is not in the sprite fails a test. No emoji, no icon font, no CDN.
- Nothing is loaded from another site; apps copy the toolkit (`CODE-03`).

**The site**
- Every page has exactly one `h1`, the shell (menu, logo, breadcrumb, theme button, user, version) and no inline style; every list page has its toolbar above the list and
  its dialogs (Search, Filter, Sort), or, with `ToolbarModel.SearchField`, the search as a field in the bar and Filter and Sort at its right end (`LIST-03`; the rulebook page does it).
  A page that is not a list of items (a few fixed facts) needs no toolbar (see `LIST-02`). A long page sets `ViewData["IndexDialog"]` and gets the index icon in the header bar (`SHELL-16`).
- A list: toolbar, table or gallery, pager directly under it, actions below left-aligned. CRUD = list + separate page. Buttons under the content, positive to negative.
- Components and patterns are **data** (`ComponentCatalog`, `PatternCatalog`): add an entry, not a page. An example is a raw string: it is rendered live and shown as source from
  the same string; the tests parse all of them (classes that exist, icons that exist, `data-label` on every `td`, typed buttons, unique ids, balanced tags). The layout classes and the
  utilities are documented the same way (the groups Layout and Utilities); a `CatalogSection` decides which page shows which groups, so a new group needs a section.
- **The look is flat on purpose** (docs/ARCHITECTURE.md, "A flat look"): no gradients, glow or blur, borders instead of shadows, nothing lifts on hover, small radii, sentence-case
  labels, no icon tile next to a title, explanations as text instead of alert boxes, **what is selected is a tint behind the whole item, never a bar at one side of a rounded item**
  (`COLOR-08`). Do not bring any of it back to make something "look nicer"; `ToolkitCssTests` and `OneSidedAccentTests` pin part of it.
- Numbers and dates use the invariant culture (the site is English; a developer PC and a container must show the same text).
- **Mobile matters:** check 390 px for every new page; secondary table columns get `mat-hide-sm`; on a phone dialogs are full screen with the X at the top right.
- After a change to CSS, JS or cshtml the container must be rebuilt.
- **The browser checks make one release per run** (a release never changes): the development volume grows; `docker compose -f docker-compose.dev.yml down -v` then `up -d --build` starts it clean (with the setup assistant again; the checks create the administrator).

**Code**
- Microsoft C# guidelines, file-scoped namespaces, `.editorconfig`. Few nested types; several functions per class are fine. Code, identifiers and comments are English.
- Anything that parses text (the CSS tokens) has a unit test.
- Generated files use LF (`.gitattributes`).

**Git**
- Commit and push **under the user's name**, never add a Claude/Anthropic co-author trailer or "generated with" line (global rule of the user, it overrides any default).
- Branches: `main` = release, `dev` = development. Versions: release `<major>.<minor>.<build>-<date>`, development `nightly-<build>-<date>`, local `local-<date>`
  (`Directory.Build.props`, `.github/workflows/build.yml`).
- Push without asking (the user's standing instruction, 2026-10-11); a push to `main` or `dev` starts the GitHub action, which builds and publishes the image. The audit snapshot and the screenshots in `docs/images` show real content of this site only, never user data.
