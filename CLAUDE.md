# CLAUDE.md

Guidance for working in this repository. Keep it in sync with the code: update it whenever architecture, interfaces or the way the rules are
checked change.

## What MatDesign is

The design and identity of **all Mat apps**, as a website and a complete UI toolkit: the **rulebook** (every rule about how an app looks and behaves, with
stable IDs), the foundations (tokens, type, icons), the shell, every control with live examples, the page patterns, an accent builder, and **MatUi**, the toolkit
itself (CSS, one script, an icon sprite) that the apps copy. It is the place to look before building or changing any UI in the family.

Stack: ASP.NET Core on **.NET 10**, C#, **Razor Pages**, plain CSS and plain JavaScript (no build step), Docker-first. Web port **8060**. The site keeps no state
(no database, no secrets); the image runs as an unprivileged user. The one exception is the **Studio** (docs/STUDIO.md, built stage by stage): it writes to `/data` and is on when
`MATDESIGN_DATA` is set (the image sets it). Its first user, an administrator, is created by the **setup assistant** on the first visit (`/Studio/Setup`; a name and a password, kept as a hash in `users.json`; the people and their roles are stage 16),
so **the release `docker-compose.yml` has no variables and no `.env` file: keep it that way, do not bring a password or a port variable back**. `MATDESIGN_ADMIN_PASSWORD` is only an optional shortcut for
automation. The browser checks create the administrator when they find none (`STUDIO_NAME` / `STUDIO_PASSWORD`, default `admin` / `studio-dev`).
The instance belongs to **one company** (docs/PRODUCT.md): the setup assistant also asks for its name and for the **template** the site starts from, `demo` (the Mat apps as sample data) or `blank`; the site names itself after the company (`IDENT-09`).
`MATDESIGN_TEMPLATE` / `MATDESIGN_COMPANY` are only for automation, like the password (the browser checks start a blank site with them); an instance without a tenant is the demo.

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before changing the design and [docs/ADOPTING.md](docs/ADOPTING.md) for what an app has to do to use MatUi; [docs/PLATFORM.md](docs/PLATFORM.md) is the platform contract of the family (what every app offers besides its pages: `/api`, health, state, actions, the sign-in through MatIdentity).

## Build / run / test

- **Build:** `dotnet build MatDesign.slnx` (note: `.slnx`). SDK 10 (see `global.json`). Expected: 0 warnings, 0 errors.
- **Tests:** `dotnet test`: xunit, no Docker, no browser. It also starts the whole site in memory and crawls it. **Browser checks:** `cd tests/e2e && npm install && node run.js http://localhost:8060` and `node white-label.js http://localhost:8060`
  (Playwright against the running container; CI runs both). Add a check there when behaviour of `matui.js` changes: reading a script proves little.
- **Run / test the site: always rebuild the container and redeploy the stack** (project rule): `./scripts/redeploy.ps1` (`-Port` changes the host port).
  Dev stack: `docker-compose.dev.yml`, container `matdesign-dev`, port 8060, explicit subnet `10.253.60.0/24` (the development PC has all Docker address pools used up:
  `all predefined address pools have been fully subnetted`; change it with `MATDESIGN_DEV_SUBNET`). CSS, JS and cshtml are baked into the image: rebuild after a change.
- **Release stack:** `docker-compose.yml`, image `ghcr.io/real-ttx/matdesign`, one service and one volume (for the Studio): keep it that simple.
- **Real tests are end to end against the container:** Playwright with the installed Edge (see the `screenshots` skill), then look at the screenshot instead of trusting
  computed styles. `MSYS_NO_PATHCONV=1` for Git Bash arguments that start with `/`. Shell strings with backticks, apostrophes or `$$` get mangled: write files with
  the editor tools, not with `node -e` or heredocs. A script that merges text into a file must use a replacer function (`text.replace(a, () => b)`): in a plain replacement string
  `$$` becomes `$` (it turned `$$(...)` of the script into `$(...)` once).
- **Browser checks of a blank start (stage 14):** start a second site on an empty data directory with the template in the environment and run `blank.js` against it:
  `MATDESIGN_DATA=<empty dir> MATDESIGN_TEMPLATE=blank MATDESIGN_COMPANY=Acme ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MatDesign.Web --no-build --urls http://localhost:8189` and
  `cd tests/e2e && node blank.js http://localhost:8189` (`SHOTS=<dir>` also takes screenshots). It goes through the setup assistant's steps by the real form, so it needs a fresh directory each run. `run.js` goes through the same steps for the demo
  (`STUDIO_COMPANY`, `STUDIO_TEMPLATE`).
- **Icon sprite:** `node tools/icons.js` rebuilds `src/MatUi/wwwroot/img/matui-icons.svg` from `tools/icons.base.json` plus the extras in the script (CI fails when it is stale).
- **Icon library (optional, ICON-05):** `node tools/iconlib.js` builds `src/MatUi/wwwroot/iconlib/` (sprite `mat-il-*`, catalogue, licence, README) from `tools/iconlib/`: `lucide.json` (vendored Lucide, written by
  `import-lucide.js <checkout>` when Lucide is updated), `custom.json` (icons drawn for MatDesign: one line each, with tags and categories; look at them on a contact sheet before keeping one) and `categories.json`
  (General, Home automation and its areas as curated lists). An icon can be in any number of categories. Nothing in `matui.css`/`matui.js` may refer to the library (a test checks); CI fails when the committed files are stale.
- **Versions of the toolkit:** `node tools/bump.js <patch|minor|major>` turns Unreleased of `CHANGELOG.md` into the next version and writes the number where it is kept (docs/VERSIONING.md); `node tools/matui-check.js <folder of an app>` shows how an app's copy stands against the changelog (and whether a file was changed by hand).
- **MatPlatform** (the platform contract for .NET apps, `src/MatPlatform/README.md`): apps copy it with `node tools/matplatform-copy.js <web project folder of the app>` (rule `CODE-03`); never edit a copy in an app.
  A change here needs a test in `tests/MatPlatform.Tests` and, when an app can see it, a new `MatPlatformVersion` (in `MatPlatform.csproj`) together with `MatPlatformInfo.Version`. Its JSON is read by Mattiverse and MatIdentity: a change of a name or a shape raises the contract version of docs/PLATFORM.md. It is not part of the site or its image.
- **Audit:** `node tools/audit.js [folder with the repositories]` rewrites `src/MatDesign.Web/Data/audit.json`, a snapshot of how close the other apps are to the rules.

## Map

```
src/MatUi                  Razor class library = the toolkit. wwwroot/css (10 parts), js/matui.js + matui.boot.js, img/matui-icons.svg
src/MatPlatform            the platform contract for .NET apps (docs/PLATFORM.md, README.md there), COPIED into the apps by tools/matplatform-copy.js; no app type in it, every file names its ASP.NET usings (System.* come from ImplicitUsings)
  MatApiDefinition.cs        the types of the contract and MatApiDefinition.Json (camelCase, nulls left out except a state value, letters not escaped)
  MatPlatformBuilder.cs      the description (App, Page, Tile, Field, LinkAction, PostAction, Role, Provides, Consumes; T(de, en)), MatPlatformOptions (language, CheckValues, rate limit)
  MatPlatformValues.cs       what the app implements: IMatPlatformState, IMatPlatformConfiguration, IMatPlatformActions, IMatPlatformHealth; MatPlatformCall
  MatPlatformEndpoints.cs    AddMatPlatform<TApp>, AddMatPlatformValues<TApp>, MapMatPlatform (the endpoints, secrets as {isSet}, 304, 503, 401, 404, 409)
  MatApiAccess.cs            X-Api-Key in constant time or a bearer token of MatIdentity (audience = client id); MatPlatformSettings.cs the MAT_API_KEY / MATIDENTITY_* variables
  MatValueChecker.cs         the checks of PUT /api/configure from the definition; MatPlatformValidator.cs the checks of a definition (for the apps' tests)
  MatIdentitySignIn.cs       AddMatIdentitySignIn<TUsers> (OIDC code + PKCE, JWT bearer), IMatIdentityUsers, CompleteSignInAsync, SignOutAtIdentity; MatIdentityBackchannelHandler.cs (MATIDENTITY_INTERNAL_URL)
  MatIdentityPendingLink.cs  an account that matches by address is linked only after its password: the pending link (protected cookie, 10 minutes), LinkRequired sends to ?local=1&link=1
  MatPlatformInfo.cs         the version (= MatPlatformVersion in MatPlatform.csproj, a test holds them equal)
src/MatDesign.Core         no web types
  Colors.cs                  OKLab, mixing like CSS color-mix, WCAG contrast, distance
  AppRegistry.cs             the apps and their accent, AccentPresets
  AppVersion.cs              the build of the site (Current) and the version of the toolkit (MatUi, from MatUiVersion in Directory.Build.props)
  Changelog.cs               the changelog of the toolkit (CHANGELOG.md, embedded in this project): parse, the versions since a version, the problems of the file
  AccentReport.cs            what the toolkit derives from an accent + the contrast checks (also on the surfaces of a colour scheme)
  ColorSchemes.cs            the colour schemes (paper, contrast, oled), SchemeSurfaces (a scheme in one mode, read from the CSS) and the checks per scheme, mode and accent
  PaletteAdvisor.cs          accents that are too close, proposals
  CssTokens.cs               parser for matui.tokens.css (tokens, description from the comment, group from the heading; Schemes = what each colour scheme sets)
  RuleBook.cs                the rules (id, area, level, source, text)
  Studio/StudioStore.cs      what the Studio saves: an append-only log per item (studio/<kind>/<id>/<revision>.json), history, restore, activity
  Studio/AdminAccount.cs     the old administrator file (admin.json, only read to migrate it) and the one place that hashes and verifies a password (HashPassword, VerifyHash, IsKnownHashFormat), what the setup assistant accepts
  Studio/StudioRole.cs       the three roles (administrator, editor, viewer), Capability, StudioRole.Can(role, capability): the one decision, data in code (CODE-10)
  Studio/UserStore.cs        the people in users.json: StudioUser, atomic writes under a lock, a file that cannot be read is an error, names, the guards (last administrator, oneself), migration of admin.json, the built-in administrator of MATDESIGN_ADMIN_PASSWORD, Problems for the check; UserEvent (what the history of a person holds)
  Studio/PasswordPolicy.cs   what a password has to be (10 characters, not the name, not one of 100 common ones), Generate
  Studio/AppValidator.cs     what an app has to be (id, name, glyph, port, contrast), Normalize, IsSafe
  Studio/RuleValidator.cs    what a rule has to be (an id of its area, title, statement, level, source), Normalize, IsSafe, NextId
  Studio/Diff.cs             what changed between two revisions: the fields (Flatten, Changes) and the lines (Lines)
  Studio/HtmlFormatter.cs    indents and wraps the markup of an example for showing it as code (white space only; a test holds that for every example)
  Studio/MarkupChecks.cs     the contract of the markup of an example (classes, icons, no inline/outside/script, labels, ids ...): the unit tests and the Studio use this one code
  Studio/BrandAssets.cs      the item `assets` (logo, word mark, favicon, app icon, font faces: only references to files), the public names (`logo-<10 hex>.svg`, `fonts/<family>-<weight>-<style>-<10 hex>.woff2`), content types
  Studio/BrandAssetValidator.cs  what a file of a brand is, by CONTENT (magic bytes, SVG parsed with a forbidden DTD and an allowlist), limits, families, the item, the files against the volume (IDENT-08)
  Studio/AssetStore.cs       `<data>/assets/<sha256>.<ext>`: written once, never overwritten or deleted (no public member can), a name is never a path
  ComponentProperties.cs     the documented `--mat-<component>-*` properties: reads the `@prop` lines of the CSS (ComponentProperty), the uses `var(--mat-x-y, fallback)`, `Problems` (what makes the list stale), Components (group id, title, the catalogue components that show it)
  Studio/Brand.cs            the brand (COLOR-11): company, short name, tagline and the tokens it sets, Light and Dark (the item `brand` of the kind `brand`; Brand.BuiltIn is the Mat demo brand)
  Studio/BrandTokens.cs      which tokens of matui.tokens.css a brand may set (the others are derived or structure, with the reason), their kind (colour, length, number, duration, font stack, easing) and limits
  Studio/BrandValidator.cs   what a brand has to be: IsSafe/IsSafeValue (nothing that can leave a declaration), Normalize, CheckValue (kind and limits), Validate (names, scale, contrast)
  Studio/BrandContrast.cs    the toolkit's contrast checks run on the surfaces of a brand, per scheme, mode and app accent (SchemeSurfaces.Pairings is the shared list of pairings)
  DesignTemplates.cs         the templates (stage 14): ITemplate = the built-in layer (apps, audit, sample apps, the site app, the brand in force); `demo` (AppRegistry as it is, the Mat brand, the matdesign app as the site) and `blank` (no apps, a neutral brand, the app `this-site`)
  Studio/Tenant.cs           the company of the instance (item `tenant`): company, short name, site name, tagline, template, accent; the demo tenant, `tenant.json` (Read/WriteFile), the words "Mat apps" / "Mat app" / "Mat family" as `Apps` / `App` / `Family`
  Studio/TenantValidator.cs  what a tenant has to be: Normalize, IsSafe, Validate (names, template, for a blank site an accent that reaches AA)
  Studio/ComponentTokens.cs  the documented component properties as values the Studio may set (PropertyToken: kind, limits by name, touch target or not, what the control uses today); FloorPx 32, TouchPx 44
  Studio/AppOverrides.cs     what an app sets on top of the brand (MatApp.Tokens and .Properties): the subset of tokens (shape, spacing, type, motion), Normalize, IsSafe, Validate
  Studio/WhiteLabel.cs       the white label build (CODE-09): WhiteLabelSetting (prefix and name, the item `whitelabel`), WhiteLabel (Validate, Rename on token boundaries in one pass, RenameJson, Leftovers, Examples): pure, no web types
  Studio/WhiteLabelChecks.cs the checks of the toolkit run on a renamed copy (leftovers, classes, properties, sprite ids, the shape of the script with JsShape, braces, replacement arithmetic, MarkupChecks on every example, file names): any finding fails the Build stage
src/MatDesign.Web          the site
  Program.cs                 /ui/* (the toolkit as files, bundle, archive), /releases/* (the releases of the Studio), /api/*, /health, Razor Pages, the cookie sign-in of the Studio
  Studio/VersionTracker.cs   which release of MatUi each app uses (written down by hand) and where that stands against the releases
  Studio/Styleguide.cs       the static styleguide page of a release (rules, components with live examples, apps), its stylesheet and the sample picture
  Studio/Releases.cs         the release pipeline (Check, Version, Build, Publish, Notify), the run records, the releases on the volume and the files they serve
  Studio/                    StudioService (on or off, the people and the setup, CurrentUser, the lock-out per name and address), StudioPeople (each change to a person with its entry in the activity list, no secret), StudioAccess (RequireCapability, the 403 page, the check for an endpoint), StudioSession (what the session cookie carries: the id and the stamp, no role), DesignData (built-in + Studio items: the apps, rules and components every page reads; Version), ComponentValidator, StudioHistory (restore checks),
                             DesignChecks (every check of the design system, one report per version of the data and of the people: the Checks page, the menu badge and the release pipeline; the check `users`), StudioFormat
  Pages/Studio/              Setup (the assistant of the first user), Login, Logout, Account (every role: who you are, what the role allows, the password), Users/ (list, Edit: administrators), _Forbidden (the 403 page), _ReadOnly (the notice of a read-only page), Index, Activity, History, Checks, Releases/ (list, New, Run), Versions/ (list, Edit), Apps/, Rules/ and Components/ (list, edit); StudioPageModel is the base of the pages that need a person and checks the capabilities the page and its handler declare (wwwroot/js/users.js: Generate password)
  Services/                  ToolkitFiles, TrustedProxies (which senders may say who the client is: X-Forwarded-*, MATDESIGN_TRUSTED_PROXIES), IconLibrary (the optional icon library: sprite, catalogue, single icons, archive, served under /ui/iconlib/), Ui (icons, logo, swatch), ShellModel, Navigation, Crumbs, ListModels (toolbar, pager, query),
                             ComponentCatalog (+ FormComponents, PickerComponents, CodeInputComponents, ContentComponents, ListComponents, LayoutComponents, ScheduleComponents, RatingComponents, TreeComponents, UploadComponents; CatalogSection = what a page shows),
                             ComponentApi (classes and attributes of an example), ContextPages (the real page and the marks of each group),
                             PatternCatalog, ShellSkeleton, SchemeDocs (the numbers and tokens of the colour schemes for the pages), AuditData (Navigation: NavSet > NavGroup > NavItem with NavBadge and children)
  Pages/                     Index, Rules, Identity, Foundations, Shell, Components, Layout, Utilities, Patterns, Builder, Download, Preview (the sample app)
  Pages/Shared/              _Layout (the shell), _Bare (no menu), _Menu (items, badges, submenus; the switcher under the logo or at the end of the menu), _MenuSwitcher (the switcher button, under the logo,
                             in the foot or at the end of the menu: `ShellModel.SwitcherPlace`), _MenuSwitchPopup (its popup; menus grouped below a `NavSetParent`), _Head, _Toolbar, _Pager,
                             _DocSection + _Toc + _Component + _ContextPage (the components, layout and utilities pages), _AccentSwatches, _ContrastCard (+ _ContrastRows, _SchemeChecks), _FoundationsTabs
  Pages/Studio/Brand         the brand editor: names, a token table per group of the CSS (an input by kind, a colour picker, a reset), a preview outside the form, the contrast, a section per colour scheme; wwwroot/js/brand.js is its script (also of the next two)
  Pages/Studio/BrandComponents  /Studio/Brand/Components: the 68 properties by component, each group with a table and the real control beside it (PropertyPreviews: the mapping to the catalogue, as data; Shared/_PropertyGroup, _BrandTabs); /Studio/Apps/Edit has the section Overrides of this app (same rows)
  Pages/Studio/Brand         the brand editor: names, a token table per group of the CSS (an input by kind, a colour picker, a reset), a preview outside the form, the contrast; wwwroot/js/brand.js is its script
  Pages/Studio/Company       the Company page: the names and the accent of a blank site, the two templates and the switch (a question first, nothing is deleted); Setup has the company and the template as steps 1 and 2 (wwwroot/js/setup.js shows one at a time)
  wwwroot/css/site.css       the few documentation-only styles (doc-*); wwwroot/js/doc.js the "Try it" buttons, where the code is shown and the group-in-a-page panels
  Studio/AssetFiles.cs       the files of the current assets as they are served and released (`/ui/brand/<name>`, `brand/` in a release, the archive, the manifests); the store; only what still has its hash
  Pages/Studio/Brand/        Assets.cshtml(.cs) = `/Studio/Brand/Assets` (a card per slot, uploads, fonts, specimen), `_AssetSlot.cshtml`; its pages are in the namespace `MatDesign.Web.Pages.StudioBrandAssets` (a namespace called Brand would hide the type Brand);
                             wwwroot/js/brand-assets.js the font specimens; `ToolkitFiles.AssetsOverlay` the @font-face rules; `Ui.BrandImage` / `Ui.AppIconLink` the logo and the icon links of the shell
tests/MatDesign.Tests      unit tests + the site in memory
tests/e2e                  run.js: the behaviour in a real browser against a running site; blank.js: a blank start on a second site (see Build / run / test); touch-audit.js + touch-hit.js: every control of every page measured for a hit area of 44 px on a touch screen
  Pages/Studio/Releases/WhiteLabel  the white label page (CODE-09): prefix and name with live validation (wwwroot/js/whitelabel.js), before and after, the trial build, save and reset; Studio/WhiteLabelBuild.cs (WhiteLabelStore; WhiteLabelBuild.Apply renames the files of a release, the JSON and the archive, writes the hash of the copy and runs the checks), Studio/WhiteLabelEndpoints.cs (POST .../WhiteLabel/rename: the renamer as an address, for the browser checks)
  wwwroot/css/site.css       the few documentation-only styles (doc-*); wwwroot/js/doc.js the "Try it" buttons, where the code is shown and the group-in-a-page panels
tests/MatDesign.Tests      unit tests + the site in memory
tests/MatPlatform.Tests    MatPlatform: a demo app in memory (TestServer, DemoApp.cs), the endpoints, the sign-in, the validator, the backchannel, the package files
tests/e2e                  run.js: the behaviour in a real browser against a running site; white-label.js: the white label build (the Studio page, the renamed toolkit behind the renamer of the site, the look of the original and the copy compared pixel by pixel)
tools/                     icons.js, iconlib.js (+ iconlib/: the sources of the icon library), audit.js, bump.js (makes a version of the toolkit), matui-check.js (checks an app's copy of it),
                           matplatform-copy.js (+ .ps1: copies MatPlatform into an app)
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
- Colour schemes (`COLOR-09`): `data-scheme="paper|contrast|oled"` on `<html>`, opt-in, at the END of `matui.tokens.css` (not a part of its own). A scheme sets surfaces, lines, text, status texts, shadows, the dim layer and
  the scrollbar, **never an accent token**; its selectors start with `:root[data-scheme=...]` (so `CssTokens.Tokens` never reads them as base tokens or as the dark set; `CssTokens.Schemes` does), its dark values are written
  twice and identical, it sets in dark every token it sets in light, and `oled` has no light values (light = standard). Links follow the surfaces through `--mat-accent-text-keep`. `SchemeTests` holds every scheme, mode, app and
  accent preset to AA; the standard look is unchanged and the pairings it was never held to are shown as advice. `MatUi.scheme`, `data-scheme-set`, `data-scheme-save-url`, `mat-scheme` in localStorage, `matui.boot.js`.
- Touch targets (`MOBILE-04`): the **hit area** is 44 px on `(pointer: coarse)`, never the drawn mark. A small mark (check, switch, the X of a chip, a dot) sits in a label with `min-height: 44px` and padding, or a centred `::before` of
  44 px, or the script forwards the tap; everything of it is written inside `@media (pointer: coarse)` so a mouse sees no change, and it never reads `--mat-check-size` or `--mat-switch-*`. Only `--sm` and `--compact` variants stay at 36 px.
  A new control: run `node tests/e2e/touch-audit.js <url>` (0 findings outside its allowlist, each exception with a reason) and add it to the "Touch targets" block of `run.js`; `TouchTargetTests` holds the rules of the CSS.
- Order inside a button row, a list block and the toolbar is enforced by CSS (`order`), not by the author. A selector in a later part must be more specific than a base class it
  overrides (the bundle puts `controls` after `shell`).
- Validation (`FORM-05`): the script checks `form.mat-form` and `form[data-validate]` on submit and writes the messages itself. A submit button with `formnovalidate` skips the check (`e.submitter`), also after a
  `data-confirm` on the form, which submits again with the same button.
- Toasts (`SHELL-12`): one host `.mat-toasts`, at the end of `<body>` or, while a dialog is open, inside the topmost one of `dialogStack` (the dialogs `openDialog` opened): a modal dialog is in the top layer, so nothing
  outside of it is seen above it. The host moves in `openDialog` and in the `close` handler, which runs before a dialog the script built removes itself, so no toast is lost with it.
- Selection (`data-selectable`): a check per item (`data-select-row`), maybe `data-select-all` and a `[data-bulk-bar]` with `[data-bulk-count]`. The script marks the item of a ticked check `is-selected`: the nearest `tr`,
  `.mat-tile` or `[data-select-item]` inside the box. A selected row is `--mat-accent-soft-strong`, a selected `[data-select-item]` is `--mat-accent-soft` with the accent as its line (like a ticked `.mat-choice`): a tint, no bar (`COLOR-08`).
- The menu: a main action (`SHELL-19`) is the first item and filled with the accent (`mat-nav__item--primary`, `NavItem.Primary`; the current page keeps the fill, a step darker). The switcher (`SHELL-15`, `ShellModel.SwitcherPlace`) is under the logo (`Top`),
  in the foot directly above the user (`Bottom`, `mat-nav__switch--foot`; `Both` draws it in both places), or, with `mat-nav__switch--end` (`End`, `ShellModel.SwitcherAtEnd`), after the panels and above the foot; from the foot and the end its popup opens upwards (`data-menu-place="top"`). In the header bar a main action is a `mat-btn--primary mat-btn--icon` in
  `mat-topbar__actions` (`SHELL-17`, one icon in the accent colour, last; a second one beside it is `mat-btn--secondary mat-btn--icon`). The sample app shows the menu pieces with `/Preview?...&action=1&switcher=end`.
- Pickers (`FORM-15`): a wrapper `mat-picker[data-picker="select|date|daterange|time|timerange|colour|tags|slot"]` around the real input `mat-picker__input`, which posts one value in a fixed
  format (`2026-10-10`, `..` for a range, `09:30`, `#b4235f`, `2026-10-14T09:30`, the tags joined by a comma); the script adds the button and the panel (`position: fixed`, full screen with an X on a phone; `tags` is
  `inline`: a field of chips, no button, its suggestions a dropdown under it). Texts in English and German
  (`PK_TEXT`: both languages must have every key, a test checks; it also holds the label of the theme button and the AM and PM, so the script writes no text an app cannot change: `data-pk-*`,
  `data-theme-label`, `data-dlg-*`, `data-msg-*`, `data-copied` on `<html>`, see ADOPTING.md 3a). A hidden element that has a `display` in the CSS needs its own `[hidden]` rule (tests list them).
  A select can create what is missing and suggest (`FORM-16`): `data-create-url` (POST `name=`, answer `{ value, name, description? }`) and `data-suggest-url` (GET, `[ { value } | { name } ]`) on the wrapper, `MatUi.picker.set`,
  `.addOption`, `.suggest`; the create row and the suggested rows are never options (`isReal`), what comes back is made with `textContent`, the demo endpoints are `/api/demo/create` and `/api/demo/suggest` (stateless).
- Colours a user chose (`COLOR-10`): `data-colour="#rrggbb"` (or `#rgb`) on `mat-tag`, `mat-dot`, `mat-icon` or a picker option; the script writes `--mat-swatch` and `is-coloured` (at the start and in `MatUi.refresh`), the CSS draws a
  dot or colours the icon. It is never the colour of the text and never the only signal; `MarkupChecks.Colour` flags a value the script would ignore. No inline style, no literal colour.
- Code input (`FORM-19`): `.mat-otp[data-otp data-length data-chars data-case data-mask data-group data-submit]` around the real input `.mat-otp__input` (a text field without the script; it is what is validated, posted and reset). The script hides it
  and draws one `.mat-otp__box` per character in a `role="group"` named by the label (`Digit 3 of 6`), **one tab stop** (roving tabindex, the arrow keys walk inside), no `maxlength` (an SMS code arrives whole on the first box, which has the
  `autocomplete` of the real input) and no `name` on a box. An incomplete code is not valid (`setCustomValidity`), so `required`, `pattern` and `data-msg-*` work; the boxes follow `disabled`, `readonly` and `aria-invalid` of the real input
  (a `MutationObserver`). Completion fires `mat:otp-complete` (cancelable); the form is sent only with `data-submit` (`requestSubmit()`). 16 px type or more in every size, boxes shrink in width and never overflow. API `MatUi.otp.clear/focus/setInvalid/value`.
  Texts in `OTP_TEXT` (English and German, a test checks).
  A dropdown with a list of checkboxes is `data-picker="select"` with `data-multiple` and plain options; `data-select-all` adds a first row, a `role="checkbox"` button with `aria-checked` true, false or
  mixed that acts on the options that are shown (the search narrows it). Texts `selectAll`, `selectFound`, `allSelected` in `PK_TEXT`.
- Number stepper (`FORM-20`): `.mat-stepper[data-stepper]` around a native `input type="number"`; the script adds the two buttons, `is-ready` (which hides the browser's spinners: without the script it is the
  native field), `data-unit` and `data-precision`. Whole-number arithmetic with the step base of the browser, a press on `pointerdown` (hold repeats and accelerates), a click with `detail` 0 steps once, the keys
  of the field (arrows, Page up and down, Shift = ten steps). A button at a limit is `aria-disabled`, never `disabled`; the buttons are not in the Tab order. `mat-stepper--stacked` only with `(pointer: fine)`.
  Texts in `STEP_TEXT` (English and German, a test checks). Size comes from the input (`mat-input--sm` / `--lg`).
- Calendar (`FORM-18`): `.mat-calendar[data-calendar]` around the real input `mat-calendar__input` (`data-mode="single|range|multiple"`, `data-min`/`data-max` as a date, `today` or `+3d`, `data-disabled`,
  `data-disabled-weekdays`, `data-events` JSON with `data-agenda`, `data-week-numbers`). It draws the **month grid that the date picker shares** (one `calMonth`, `calPaint`, `calKeys`, driven by a state
  object): never copy the grid, change it there. Its texts are in `CAL_TEXT` (both languages, a test checks), found through `pkText`. API: `MatUi.calendar.setEvents/setDisabled/setValue/go`.
- Appointment picker (`FORM-18`): `data-picker="slot"` (`KINDS.slot`) posts `2026-10-14T09:30` (`..09:45` with `data-duration`). Its panel is the same grid plus the times of the chosen day. Availability is
  `data-slots` JSON or, without it, asked per month with the event `mat:slots-needed` and answered with `MatUi.picker.setSlots(detail.picker, data, detail.month)`; the examples of the site are answered by
  `doc.js`, the browser checks stop the event and answer it themselves. A taken time says "Taken" in words (`COLOR-04`); a time in the past is never offered.
- Carousel (`LIST-13`): `.mat-carousel[data-carousel]` around `.mat-carousel__track` > `.mat-carousel__slide`. The strip scrolls and snaps in CSS (a scrollable strip without the script); the script names the slides,
  adds the bar (Previous, a dot per page, Next, Pause with `data-autoplay`), makes the slides out of view `inert` and says which page is shown. The buttons at the ends are `aria-disabled`, never `disabled` (the focus stays).
  Autoplay waits for the pointer and the focus, stops for good after any use of a control and never starts under `prefers-reduced-motion`. Texts in `CAR_TEXT` (English and German, a test checks).
- Groups in a table (`LIST-16`): `<table class="mat-table" data-groups="key">` with one `<tbody class="mat-group" data-group="name">` per group: the heading row `.mat-group__head` (a cell `.mat-group__cell` with `colspan`,
  `.mat-group__bar` around the toggle `button.mat-group__toggle[data-group-toggle][aria-expanded]` and optional `.mat-group__actions`), the items `.mat-group__item` and, last, the add row `.mat-group__add`. The script only toggles
  `is-collapsed` (the CSS hides the other rows) and `aria-expanded`, remembers it in `localStorage` per app, table key and group (`mat-groups`) and leaves a group with `data-group-fixed` as the server drew it (a search hit);
  `data-groups-set="open|closed|toggle"` on a button works on all groups. The line of the tree is the `::before` and `::after` of the first cell of an item row (`::before` is also the label of the cell on a phone: the phone
  rule gives it back, a test and a browser check hold that). The add row is always the last row of its group (`LIST-16`: not in the heading).
- Slider and range slider (`FORM-17`): `.mat-slider[data-slider]` around a native `input.mat-slider__input[type=range]`, or `.mat-slider--range` around two (`from`, `to`). The native inputs stay the truth (value, form, validity,
  `aria-valuetext`, keyboard, Tab stop) and are invisible, never `display: none`, once the script has drawn the rail, the thumbs (44 px targets), the bubbles, marks and labels (`is-ready`); without the script they are native controls.
  The script handles the pointer and the keys itself and sends `input` and `change` like the native control. `data-format="{0} GB"` is the template of every value shown; the names of the two inputs of a range come
  from the label (`SL_TEXT`, English and German, a test checks). Options: `data-min-gap`, `data-bubble="always"`, `data-ticks`, `data-tick-labels`, `data-labels="none"`, `data-output`, `data-range-format`.
- Schedule (`LIST-14`): `.mat-schedule[data-schedule]` is an empty root with `data-events` (JSON: `id`, `title`, `start`, `end`, `tone`, `location`, `note`, `url`, `status`); the script draws the bar, the five views
  (Agenda, Day, Week, Month, Year) and the jump to a month and year. It is **not** the date picker (`FORM-15`). The layout follows the width of the schedule (`is-wide` from 640 px, set by a `ResizeObserver`), not the window;
  a length or a column that is data is a `--mat-sch-*` property set by the script (`data-sch-*` in the markup it draws), never an inline style. A tone is a tint with a line all around plus an icon and a text (`COLOR-04`, `COLOR-08`).
  Events: `mat:schedule-select`, `mat:schedule-range` (lazy loading with `data-lazy`), `mat:schedule-day`; API `MatUi.schedule.setEvents|go|setLoading|get`. The calendar weeks are ISO 8601 (`isoWeek`; a test ports the formula and
  checks it against .NET for 40 years). The examples pin the clock (`data-now`) and write their dates, so that nothing depends on the day: do the same in a test. Texts in `SCH_TEXT` (English and German, a test checks).
- Lists of rows (`LIST-15`, like ion-list): `ul.mat-itemlist` of `li.mat-item`; a row is `mat-item__main` (a link, a button, a label or a div: the whole row is the target) with `__lead`, `__body` (`__title`, `__text` lines) and
  `__end`; what else is pressed (switch, button, handle) is `mat-item__aside` beside it, never inside a link. The look is the list's (`--lines-inset|none`, `--inset` card, `--sticky` dividers with `--scroll`, `--choices`, `--tasks`,
  `--lead-lg`, `--compact`); do not call anything `mat-list*` (that is the list block of a page). `data-itemlist` adds, by what the markup has: sliding actions (`mat-item--sliding` > `__row` > `__main` + `__options--start|end` of
  `__option` buttons; swipe, a More button the script adds, arrow keys; options are `inert` while hidden; `mat:item-action`), reordering (`data-reorder`, `__handle`; pointer and Space/arrows/Escape; `mat:reorder`; the DOM changes on drop),
  `data-item-filter` (a field hides rows, `.mat-itemlist__empty` shows) and the `data-selectable` of tables around a list. Texts in `IL_TEXT` (English and German, quoted keys: the picker test takes the first unquoted `en:` table).
- Rating (`FORM-22`): `fieldset.mat-rating[data-rating]` (usually also `mat-field mat-fieldset`) of native radios `.mat-rating__input` with their labels `.mat-rating__label`, one `name`: a form posts it, the radio keys and one Tab stop are the browser's,
  and without the script it is a row of radio buttons. The radios say what can be chosen (half stars are 0.5, 1, 1.5 ...; the highest value is the number of shapes, clamped to 3 to 10; value 0 is "no rating"; there is no `data-step`).
  The script adds `is-ready` (radios and labels invisible, never `display: none`) and draws two layers of the same row (`.mat-rating__layer--empty` outlines, `--fill` filled shapes clipped to `--mat-rating-fill`, a share of the whole row
  that the script writes; every cell has the same width so the share is exact). A mouse or a pen previews on `pointermove` (touch does not), a click chooses by the position (`ratAt`: the first radio value at or above it) by checking the radio and sending
  `input`, `change`, then `mat:rating-change`; `data-clearable` clears on a click on the chosen value. `mat-rating--readonly` + `data-value` (+ `data-max`, `data-count`, `data-show-value`) is drawn by the script, `role="img"`, `aria-label` in words
  unless the author gave one. Shapes `mat-rating--heart` (icon `heart`) and `--dot` (icon `circle`), sizes `--sm` and `--lg`; the cell is as high as a control (`--mat-control-h`: 40 px, 44 on a touch screen). Texts in `RT_TEXT` (English and German, a test checks),
  `data-rt-<name>`. API `MatUi.rating.init/get/set`. Examples are written out by a helper in `RatingComponents.cs`.
- Tree view (`LIST-17`): `ul.mat-tree[data-tree][aria-label]` of `li.mat-tree__item` = a row `.mat-tree__row` (`__icon`, `__label` (a span or a link), `__count`, `__actions`) and, in a parent, a `ul.mat-tree__group`; a folder icon is the pair
  `.mat-tree__icon--folder` (closed, open) of which `aria-expanded` shows one. Markup is a nested list that is all open without the script; the script writes `role` tree/treeitem/group, `aria-level`/`setsize`/`posinset` (counted over the items
  shown), `aria-expanded` (parents; **the CSS reads it, there is no open class**), `aria-selected` (one) or `aria-checked` true/false/mixed (`data-multiple`: ticking a parent ticks all, a parent follows its children; a visual `.mat-tree__check`,
  not a native input), `aria-labelledby` (label and count), draws the toggle chevron and `--mat-tree-level` (the CSS turns it into the row's padding and the guide lines `::before`, one gradient stripe per ancestor). The focus is on the `li` (roving tabindex,
  one Tab stop; the buttons of `__actions` are tabbable only in the row with the stop); keys are only handled when `e.target` is the item. A filter (`data-tree-filter`) hides with `hidden`, keeps and opens parents of a match and restores
  what was open when cleared (`_matWas`); `data-lazy` fires `mat:tree-load`, `MatUi.tree.setChildren(item, html | null | '')`; `data-tree-key` remembers open/closed per app, tree and item (`mat-tree` in localStorage, lazy and `data-tree-fixed` items
  are skipped). Texts `TR_TEXT` (English and German, a test checks; `data-tr-<name>` on `<html>`). The CSS part must stay **before** the schedule part of `matui.lists.css` (the schedule test reads to the end of the file). The site's demo server for
  the lazy example is in `doc.js` (answers `mat:tree-load` of `#tr-lazy`); a page that answers itself stops the event.
- File upload (`FORM-21`): `.mat-upload[data-upload data-max-size data-max-files data-paste data-preview]` around the real `input.mat-upload__input[type=file]` (the native file field without the script; what is validated and posted).
  The script hides it (`is-ready`) and draws `.mat-upload__zone` (icon, **one** `button.mat-upload__browse` whose `::after` covers the zone, `.mat-upload__limits` built from `accept` and the data attributes), `ul.mat-upload__list`
  of `li.mat-upload__item` (thumbnail, name, size or progress, `.mat-upload__remove` named by the file) and the live region `.mat-upload__status` (a refusal in words with an icon, never `hidden`: a hidden live region is not read).
  Every file (dialog, drop, paste, `MatUi.upload.add`) goes through `upAdd`: folder, `accept`, size, duplicate, room; a refusal never stops the other files; without `multiple` a new file replaces the old one. The real input
  holds the list through a `DataTransfer` (no `DataTransfer`: the control is not drawn); the dialog's own `change` is handled in the capture phase and is stopped when nothing was added, the other sources dispatch `input` and `change`
  with `u.busy` set. **No `/*` anywhere in the section of the script, not even in a string or a comment** (`ScriptTests` strips block comments with a regex: it swallowed the code after it). Events `mat:upload-change`,
  `mat:upload-refused`; API `MatUi.upload.init/files/add/remove/clear/setProgress/setError`. The width of a bar is the property `--mat-up-progress`. Texts in `UP_TEXT` (English and German, a test checks; `data-up-<name>` on `<html>`).
  The CSS sits between "Files" and "Layouts" in `matui.forms.css`, not at the end: the slider tests read the stylesheet from the slider to the end.
- Layout and utilities: mobile first with `sm` 600, `md` 900, `lg` 1200 px; no `!important`; values from the tokens; the grid spans by `grid-column-end`. An example in the catalogue uses
  toolkit classes only, never `doc-*` (it is copied into other apps).
- Icons come from the sprite (`Ui.Icon("name")` / `#mat-i-name`); a name that is not in the sprite fails a test. No emoji, no icon font, no CDN.
- Nothing is loaded from another site; apps copy the toolkit (`CODE-03`).
- Component properties (`SPACE-06`): the shape of a control is changed through `--mat-<component>-<aspect>` (radius, height, pad-x/pad-y, border-width, gap, font size/weight; `btn`, `input`, `card`, `dialog`, `menu`, `tag`, `pill`, `badge`, `tabs`, `table`,
  `alert`, `toast`, `switch`, `check`, `nav`, `topbar`), never by overriding a class. **Every use is `var(--mat-btn-radius, var(--mat-radius))`: the fallback is a global token (a literal of the type only where there is none), the property
  has no value anywhere**, so an app that sets none computes what it always did. Declare each one with ONE line above the rule that uses it: `/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */`
  (component, name, type colour|length|number|duration|font, fallback, description ending with a full stop), and add the component to `ComponentProperties.Components` with the catalogue components that show it. A new property of a control
  that is used with a fallback and has no `@prop` line fails `ComponentPropertiesTests`, so does a line with no use. No colour, no shadow, no `!important`; a height property is a floor (default `--mat-control-h`, 44 px on a touch screen: do not
  add a smaller coarse value). Listed on Foundations > Tokens, on the page of each component, at `/api/properties`; `tests/e2e/run.js` sets every property to its default (same computed styles as none) and sets properties on `:root` and on one element; `node tests/e2e/properties-before-after.js <url> [git ref]` compares ~160,000 elements with the CSS from before the properties (served from git): run it again when a rule that uses a property changes.

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
- **The brand is tokens, never CSS (`COLOR-11`, docs/STUDIO.md "Brand")**: `ToolkitFiles.BrandOverlay` appends it to the apps part (the part count stays ten): `:root` for light, the dark set twice and identical, nothing when the brand sets nothing. A new token that a brand may set needs a
  kind the editor knows (`BrandTokens.Describe`), and a limit if usability depends on it; a brand is never held below the contrast the toolkit holds (`BrandContrast`, the `brand` check). The page of the editor sets the unsaved values on `[data-brand-preview]` through the CSSOM, never as a style attribute.
- **The site names itself after its company, never after another (`IDENT-09`, docs/STUDIO.md "Company and templates")**: a page never writes `MatDesign`, `Mat apps` or an app of the family; it reads `DesignData.Tenant` (`SiteName`, `Company`, `Apps`, `App`, `Family`) and `DesignData.SiteApp`, and
  asks `DesignData.Apps` / `Pick("matpaper")` (the sample app, or the site when this instance has no such app) instead of `AppRegistry` or `Design.App("matdesign")`. The built-in layer comes from the tenant's template (`ITemplate`); `demo` is the site as it always was
  and an instance with no tenant is the demo, so a change for a company must leave the demo's pages as they were (`TemplateSiteTests` compares them with the ones in `tests/MatDesign.Tests/Golden`, served by main before the tenants). A list that can be empty has an empty state with the
  way out (`LIST-11`), not a broken page. The toolkit's own names (`MatUi`, `mat-`, the text of the rules and components) stay until stage 15.
- **Properties, apps and schemes (stage 12, docs/STUDIO.md "Components, apps and schemes")**: the order in `matui.css` is toolkit, colours of the apps, brand (tokens, properties, schemes), overrides of each app (`ToolkitFiles.Build`); light on the selector, the dark set
  twice and identical, nothing set writes nothing. **A height of a control that is pressed under 44 px is written inside `@media (pointer: fine)` only** (`ComponentTokens.AppliesToFinePointerOnly`), never under 32 px: do not write one into `:root` or `[data-app]` directly. A new `@prop` needs no Studio code (limits by name); an
  app owns shape, spacing, type, motion and its accent, never surfaces. A preview inside a form has `form="pv-none"` on its controls (`PropertyPreviews.Detach`). `data-*` attributes with a null value render as `data-x=""` in Razor: write them conditionally.
- **A company's prefix and name are made by the build, never by hand (`CODE-09`, docs/STUDIO.md "White label")**: the source keeps `mat-` and `MatUi`; the item `whitelabel` makes the Build stage rename the assembled files (`WhiteLabelBuild.Apply`), on token boundaries, in one pass (`WhiteLabel`), and `WhiteLabelChecks` holds the copy to the toolkit's checks (a finding fails the stage). A new kind of name in the toolkit (a new data attribute, a storage key, a global, a file) needs a rule in `WhiteLabel` or a reason it is covered, and a test; never build a name from two strings (`'mat' + '-x'`), never cut the prefix off by its length. A release without a setting stays byte for byte what it was (a test compares it with the toolkit's own files). `tools/matui-check.js` reads the product and file names from the manifest.
- **The browser checks make one release per run** (a release never changes): the development volume grows; `docker compose -f docker-compose.dev.yml down -v` then `up -d --build` starts it clean (with the setup assistant again; the checks create the administrator).
- **Brand assets (`IDENT-08`, stage 13).** The Studio item `assets` only refers to files; the files are `<data>/assets/<sha256>.<ext>` and **never overwritten or deleted** (nothing public in `AssetStore` can). A file is judged by its CONTENT (`BrandAssetValidator.Inspect`: PNG, ICO, WOFF2, WOFF by magic bytes, SVG parsed with a forbidden DTD against an allowlist), never by its name or the type a browser sent;
  every new refusal has a test (the hostile SVG corpus in `BrandAssetValidatorTests`). An upload, a restore, the check `assets` and the pipeline all use that one code. The public name carries the first ten digits of the hash, so `/ui/brand/<name>` may be `immutable`; SVGs are served with `Content-Security-Policy: default-src 'none'; style-src 'none'`
  and everything with `nosniff`. `@font-face` is written by `ToolkitFiles.AssetsOverlay` with a URL relative to the stylesheet (`brand/fonts/...`; `../brand/fonts/...` in the part `css/matui.apps.css`) and nothing for a brand without fonts; a brand without assets leaves every file and every page byte for byte as it was (tests hold it).
  The shell uses the logo, the word mark, the favicon and the app icon only on the site's own pages (not in a sample app); a dark variant is a second `img` (`mat-brandmark--light` / `--dark`, CSS only, no inline style). The assets page is `/Studio/Brand/Assets`: the folder is called Brand, so its pages live in the namespace
  `MatDesign.Web.Pages.StudioBrandAssets` (a namespace `...Studio.Brand` would hide the type `Brand`). A test that builds a PNG or a font builds it itself (`AssetFixtures`, `tests/e2e/brand-asset-files.js`): no binary file in the repository.

- **People and roles (`CODE-10`, stage 16, docs/STUDIO.md "People and roles").** Who may change what is decided on the server from a role: **every page model of the Studio has `[RequireCapability(...)]` and every `OnPost*` handler exactly one of its own** (a new page or handler without one fails `StudioCapabilityDeclarationTests`, which also holds the list of what each needs: change that list in the same commit, a reviewer reads it); a minimal endpoint that writes uses `.RequireCapability(...)` on the route. The role is read from the people on every request (`StudioService.CurrentUser`, `StudioPageModel.Me` / `Who` / `Can` / `CanEdit`), **never from a cookie or a claim** (the cookie carries the id and the stamp). The name that goes into a revision, a release or the activity list is `Who`, never a literal. A control the person may not use is left out or turned off with the reason (`fieldset class="mat-fieldset" disabled` and `_ReadOnly`), which is a courtesy: `StudioAccessTests` posts every changing address as each role, so a handler that forgets its declaration is found. The guards (the last active administrator, oneself, the name that was used) are `UserStore`'s, tested there. A secret (a password, a hash, a salt, a stamp) **never enters the history, the activity list, a log or a page**; `UserEvent` holds the id, the name, the role and the disabled flag. A password is hashed and checked only through `AdminAccount.HashPassword` / `VerifyHash`, and held to `PasswordPolicy`. The first user is always an administrator; automation that signs in as `admin` / `studio-dev` (setup assistant or `MATDESIGN_ADMIN_PASSWORD`) must keep working unchanged.
**Code**
- Microsoft C# guidelines, file-scoped namespaces, `.editorconfig`. Few nested types; several functions per class are fine. Code, identifiers and comments are English.
- Anything that parses text (the CSS tokens) has a unit test.
- Generated files use LF (`.gitattributes`).

**Git**
- Commit and push **under the user's name**, never add a Claude/Anthropic co-author trailer or "generated with" line (global rule of the user, it overrides any default).
- Branches: `main` = release, `dev` = development. Versions: release `<major>.<minor>.<build>-<date>`, development `nightly-<build>-<date>`, local `local-<date>`
  (`Directory.Build.props`, `.github/workflows/build.yml`).
- **The toolkit has a version of its own** (`MatUiVersion` in `Directory.Build.props`, semantic versioning, docs/VERSIONING.md: the number an app compares with its copy). Every change under `src/MatUi` that an app can see gets a line under `## [Unreleased]` of `CHANGELOG.md` **in the same commit** (CI fails a pull request that changes `src/MatUi` without it); a change that makes an app change something is a **Breaking** entry with a **Migration**. A version is made with `node tools/bump.js`, never by editing the number: `MatUiVersion`, the newest entry of the changelog and the literals in `matui.js` and `matui.boot.js` are held to each other by tests. **No test names a version of the toolkit**: the versions of the releases a test makes come from `tests/MatDesign.Tests/Ver.cs` (`Ver.First` = the toolkit's, `Ver.Next(n)`, `Ver.Below` for the refusal tests), a page that shows the version is masked in the golden compare and a browser check reads `/api/version`, so a bump never needs a test edit.
- Push without asking (the user's standing instruction, 2026-10-11); a push to `main` or `dev` starts the GitHub action, which builds and publishes the image. The audit snapshot and the screenshots in `docs/images` show real content of this site only, never user data.
