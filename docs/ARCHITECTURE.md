# Architecture

The decisions behind MatDesign and the reasons for them. When one of them changes, change this file in the same commit.

## 1. One place for the design, as a website and a toolkit

The Mat apps are separate projects with their own repositories, stacks and teams of one. Left to themselves they drifted: different menus, different dialogs, a
`window.confirm` here, a literal white there. MatDesign is the one place that says how an app looks and behaves, and ships the code that does it.

- **A website** because a rule is easier to follow when you can look at it: the rulebook, live examples, real frames of the sample app, the numbers (contrast, distance
  between accents).
- **A toolkit** (`MatUi`) because a rule that has to be re-implemented in every app is a rule that gets broken. The toolkit makes the right thing the easy thing: the button
  row puts *Delete* last whatever order you write the buttons in.
- **Its own container** (port 8060) so it can be opened next to any app, and so the design can be read without installing the apps.

## 2. The CSS is the source of truth

`matui.tokens.css` is read by the site at start (`CssTokens`): the tokens page, the colour page, the accent report and the tests all come from it. There is no second list
to keep in sync. A token's description is the comment behind its declaration, its group the last `/* ---- Name ---- */` heading before it.

The colour maths of the accent derivation (`hover = 86 % accent + 14 % black` …) exists twice: in the CSS as `color-mix(in oklab, …)`, and in `Colors.cs`, because the
site and the tests need the numbers without a browser. A test pins `Colors.Mix` to what CSS gives for a known pair, and the report is calibrated so that **every app in the
registry reaches AA in light and dark**. If the CSS changes, the report must change with it; the contrast test is the alarm.

**One accent per app, everything else derived.** An app sets `--mat-app-accent` (and, only if white text on it is unreadable, `--mat-app-on-accent`, and for the link colour
`--mat-app-accent-text`). Hover, the tints, the focus ring and the dark variant follow. In dark mode the accent is lightened (74 % accent, 26 % white) and the
text on it is dark. This keeps 22 colours consistent without 22 stylesheets.

## 3. The registry is data, the rulebook is code

`AppRegistry` lists the apps with the accent the toolkit uses, the accent found in the app (so the site can say what changed and why), the logo colour, the class prefix and
the repository. `matui.apps.css` has to equal it (a test), so the family's colours are written once in the registry and once as CSS, and cannot differ.

`RuleBook` holds the rules in C#, not in Markdown: a rule has an ID, an area, a level (Must / Should / May), a source (the owner's own words, or the app whose documentation
already had it) and, where a test enforces it, a `Check`. In code they can be searched and linked, counted, cited by other rules and verified (a rule that cites an unknown
ID fails a test). `/api/rules` gives the same list as JSON for tools and agents.

## 4. The site eats its own dog food

The site's layout is the shell of the toolkit (`Pages/Shared/_Layout.cshtml`), driven by a `ShellModel`. The **Preview** page draws a *sample app* of the family with the same
layout, the same partials (toolbar, pager) and the same controls, in the accent of any app, in light or dark. The frames on the pattern, shell, builder and app pages are
this page. So the examples are not mock-ups: if the shell is wrong, the site is wrong.

The toolbar and the pager are Razor partials driven by models (`ToolbarModel`, `PagerModel`): one implementation for every list on the site and in the samples. The toolbar's three
dialogs belong to **one GET form** through the `form` attribute, so each dialog sends the complete state and a result is a link that can be shared.

### The menu is data

The items of the menu are not markup an app writes but a model the partial `_Menu` draws: a `NavSet` (one menu: id, title, icon) holds `NavGroup`s (a small heading) of `NavItem`s
(title, address, icon, an optional `NavBadge`, optional children). A parent has children and no address: it is a button that opens them, one level deep (`NavSet.IsValid` and a test
check this). A `NavBadge` is a count (above 99 it reads `99+`) or a dot, in a tone that follows the status colours; a closed parent shows the sum of its children's badges in the most
urgent tone. An app with more than one `NavSet` gets the switcher under the logo; the set that has the current page is the one drawn visible, the others are in the page with `hidden`, so
choosing another one in the switcher swaps them without a request. The popup of the switcher is drawn at the end of the page like the user menu, because the menu has a backdrop filter and
that would make it the frame of a fixed popup; it names the element that holds the panels (`data-nav-scope`) so that an example in the documentation can show a menu without touching the menu
of the page. The choice of open and closed branches is remembered in the browser per app and never overrides the branch of the current page.

### Forms are a part of their own

The form controls live in `matui.forms.css`, not in `matui.controls.css`: fields and inputs, selects, checks and radios, switches, range, input groups, floating labels, the drop zone, layouts and the
validation summary. The catalogue of the form components is its own file (`FormComponents`) and is merged into `ComponentCatalog.All`, which sorts by group and by an explicit reading order where it matters (the
forms read like a manual). The script adds only what CSS cannot do: the filled part and the output of a range, a counter, the eye of a password, the names in a drop zone, an indeterminate check and the
summary of the errors of a form; validation itself stays the browser's (`required`, `type`, `pattern`) with the messages of `data-msg-*`.

### Pickers

A picker is a field whose value is chosen in a panel: a select with pictures, descriptions and tags, a date or a period, a time or a time range, a colour (`FORM-15`). They are a part of their own
(`matui.pickers.css`) and one section of `matui.js`; the select and the calendar were taken over from MatCMS, the time and the colour were made in the same style. The markup is a wrapper
`mat-picker[data-picker]` around the real input (`mat-picker__input`), which is what is validated, posted and reset. It holds the value in a **fixed format** (`2026-10-10`,
`2026-10-10..2026-10-14~2`, `09:30`, `09:00..17:30`, `#b4235f`, the keys of the options), whatever the language of the page; the script adds the button that shows it in the language of the
page (`Intl`) and the panel. The panel is `position: fixed` next to the button (below, above, or covering it when neither side has room) and full screen with an X on a phone, like every dialog.
It lives inside the wrapper, so a picker in a dialog is not cut off by it, and Escape closes the picker first and the dialog second. The real input is invisible but lies over the button, so a
browser message points at the right place, and whoever focuses it (a label, the validation) lands on the button. The texts are English and German (`data-pk-*` on `html` changes them; a test checks that
both languages have every text).

### Layout and utilities

`matui.layout.css` is the last part before the apps: containers, a grid of twelve columns (`mat-cols`, `mat-col-N`, with `sm`, `md` and `lg` variants from 600, 900 and 1200 px, mobile first) and
utility classes for spacing, display, flex, text, colour, borders and sizing. They come after every component, so a utility wins over a component's value of the same specificity, and none of them uses
`!important`; every value is a token. A child of the grid spans twelve columns by its **end line** (`grid-column-end`): the shorthand would make the start line a span as well, and then no
`mat-col-N` could win (a test pins this).

The pages Layout and Utilities are made of the same catalogue as the components (the groups `Layout` and `Utilities` of `ComponentCatalog`), so everything that is tested for a component example is
tested for them. A `CatalogSection` says which groups a page shows, and one partial (`_DocSection`) draws the three pages. An example uses only classes of the toolkit, because it is copied into other
apps: the demo cells of the layout pages are made of the very utilities that are shown.

### A flat look, on purpose

The first version looked like what a code assistant produces by default: a brand gradient on the logo and behind the hero, a glow around the primary button, glass bars with blur,
a tinted blue-grey page, an icon in a tinted tile next to every title, 18 px corners with soft shadows, cards that lift on hover, upper-case spaced labels. None of that carries
information, and together it makes every app look like every other app that was generated. So the toolkit is plain on purpose:

- **Flat surfaces.** The logo, the avatar and the primary button are the accent colour, nothing more. No gradient backgrounds, no glow, no blur behind a bar or a dialog (`COLOR-06`).
  A gradient is allowed only where it is data: the ramps of the colour picker, the track of a range.
- **Lines, not shadows.** A card is a surface with a border. Only what floats (a menu, a popover, a dialog) has a shadow (`SPACE-03`). Nothing moves on hover; a link card darkens its border.
- **Small corners.** 8 px for cards and dialogs, 6 px for buttons, inputs and menu items, 3 to 4 px for small marks; only badges, status pills and switches are round (`SPACE-02`).
- **Neutral greys.** The page and the surfaces are warm greys in light and neutral dark greys in dark, not a tint of the accent. The accent is for what is actionable or current:
  a button, a link, the tint behind the current menu item and its icon (`SHELL-02`). What is selected is never marked by a bar at one side of a rounded item (`COLOR-08`).
- **Words instead of decoration.** Group labels and table headings are sentence case, not upper case with letter spacing; a page starts with its title, without an icon tile (`SHELL-09`);
  an explanation is a paragraph, not an alert box; a coloured edge on a toast is the icon's colour instead.

A test (`ToolkitCssTests`) pins the parts that are easy to slide back into: no `radial-gradient`, no `backdrop-filter`, no brand gradient token, no upper-case table headings.
All of this is values in `matui.tokens.css` and a few rules per file, so a different decision is a small change, but it is a decision about how every app looks.

### What is deliberately not in the toolkit

Compared with a general kit such as Bootstrap there is **no navbar** (the shell replaces it, `SHELL-01`), **no carousel**, **no jumbotron or hero** (an app is not a landing page), **no off-canvas as a
component of its own** (a dialog from the side, `mat-dialog--end`, does it), and **no theming with Sass variables** (the tokens are custom properties and an app sets one colour). What an app needs
beyond that is a component of its own with the app's prefix (`CODE-04`).

### Long pages and one page per section

The components are not a list of pages to click through but one page, grouped, with an index beside it (`mat-with-toc`, `mat-toc`): a sticky column from 1100 px, an icon in the header bar that opens it in a dialog below that
(full screen on a phone); the layout and the utilities are two more pages of the same kind. `data-scrollspy` marks the link of the section at the top; it computes per index from the position of the sections, so the index beside the content and the one in the dialog
agree and two indexes on one page do not disturb each other. Because every example of every component is on the same page, **all ids share one page**: a test checks that no id appears twice and
that every `#link` leads to a section that exists.

## 5. The documentation cannot drift from the toolkit

- **Components** are data (`ComponentCatalog`): each example is one raw string, rendered live and shown as source, **beside the example on a wide window and below it otherwise, always visible and coloured**
  (`CodeHighlighter` wraps tags, attributes, strings and comments in spans on the server; the text stays the code, so Copy copies it; `ComponentExample.Wide` puts the code below for tables, bars and grids).
  Under the title of a component the classes and `data-` attributes of its examples are listed (`ComponentApi`, read from the markup). Each group also has a panel "See … in a page"
  (`ContextPages`, `_ContextPage`): a frame of a view of the sample app (`/Preview`), loaded when the panel is opened, with marks that outline a component in it. A test checks every one: the classes exist in the toolkit's CSS,
  the icons exist in the sprite, every `td` has `data-label`, every button has a type, every control has an id, ids are unique and references resolve, tags are balanced, and
  there is no inline style.
- **Pages** are scanned by a test for classes that no stylesheet defines, inline styles, inline scripts, outside resources and icons that are not in the sprite.
- **The whole site** is started in memory and crawled: every page answers, has one `h1`, the shell, and no inline style; every list page has its toolbar above the table; no
  link or frame leads nowhere.
- **The script** is read by a test: no browser dialog, no module syntax, every `data-` attribute its header documents is handled.

## 6. Copy, do not link

An app never loads MatUi from this site (`CODE-03`): it must work offline, as a PWA and under a strict content security policy, and it must not break when this site is down. The
Download page serves the files (and an archive with a README) for copying; `/ui/*` has `Access-Control-Allow-Origin: *` so a tool can read them. The first line of `matui.css` carries
the version and the hash.

## 7. No build step, no web fonts, no icon font

Plain CSS in ten parts that are bundled in the order of the cascade (`tokens`, `base`, `shell`, `controls`, `forms`, `pickers`, `lists`, `dialogs`, `layout`, `apps`) and one dependency-free script that wires
everything with delegated listeners and `data-` attributes, so server-rendered and dynamically added markup behave the same. System fonts only. Icons are one inline SVG sprite
(`tools/icons.js` builds it; CI fails when it is stale). SVG and not an icon font (`ICON-01`): the icons are strokes, a font would need them turned into outlines, and an icon in a page is a `<use>` of
a symbol that takes its colour from the text.

**The icon library** is the optional second set (`ICON-05`): `src/MatUi/wwwroot/iconlib/` holds a sprite of its own (`matui-iconlib.svg`, ids `mat-il-NAME`, so both sprites can be on one page), a
catalogue (`iconlib.json`: categories, and every icon with its categories, tags and origin) and the licence. Nothing in `matui.css` or `matui.js` knows it (a test checks), so an app can do without it, and an
app that wants it takes the whole sprite or single icons (the site serves each as a file of its own and all of them as an archive under `/ui/iconlib/`; a release carries the sprite, the catalogue and the
licence in `iconlib/`). The icons are Lucide's (ISC; no brand logos) plus the ones drawn for MatDesign in the same style where a smart home needs one that Lucide has not (radiator, curtains, motion sensor …).
**An icon can be in any number of categories**: Lucide gives each icon its categories, `tools/iconlib/categories.json` adds *General* and *Home automation* with its ten areas as curated lists (and
*Home automation* is the union of its areas), and `custom.json` names the categories of the drawn ones. `tools/iconlib/import-lucide.js` takes a checkout of Lucide into `lucide.json` (run when Lucide is updated,
the result is committed), and `node tools/iconlib.js` builds the files from the three sources without a network and checks them (valid names, only shapes and `currentColor`, known categories, none empty); CI
fails when what is committed is stale. The page *Icon library* (Foundations) carries only the symbols of the icons it shows, not the whole sprite.

A later part must be more specific than a base class it overrides, because the bundle puts `controls` after `shell` (the drawer's close button had to be `.mat-nav__brand > .mat-nav__close`
to stay hidden on a desktop).

## 8. The audit is a snapshot, and says so

`tools/audit.js` reads the layouts, pages, stylesheets and scripts of the other repositories and records what is *written* there: a sidebar, a breadcrumb, a theme switch, browser
dialogs, literal colours, inline styles. It cannot see how a page looks, so a tick means the code is there, and `unknown` means it could not be told. The result is committed
(`Data/audit.json`) so the site works without the other repositories; running the script again refreshes it. The audit is advice for the owner of an app, not a gate.

## 9. English, invariant

The site is in English; the apps keep the language of their own interface (`CODE-05`). The site formats numbers and dates with the invariant culture, so a developer's machine and a container
show the same text (a German PC printed `46,9 KB`, the container `46.9 KB`).

## 10. Security and operation

The site is stateless unless the Studio is on (docs/STUDIO.md): no database, no secrets in the image, no Docker socket. Without the Studio there is no cookie either. The Studio is the one thing that
writes: into `/data` (a volume), and only where a data directory is set (the image sets it). Its one account is created by the setup assistant on the first visit and kept as a salted PBKDF2 hash
(`admin.json`, created once and never overwritten); whoever opens the assistant first becomes the administrator, which is the price of a compose file without a password. Its session is a cookie that is
HttpOnly, SameSite Lax and ends after eight hours or when the password changes, a sign-in that fails costs half a second and five of them lock the client out for a minute, and signing out is a POST with an
anti-forgery token. The image runs as the unprivileged user of the base image and needs nothing else; the file system can be made read-only (a tmpfs for `/tmp`, the volume for `/data`) and the capabilities
dropped (`read_only`, `cap_drop: [ALL]`, `no-new-privileges`) by whoever wants that, but the compose file leaves it out to stay simple. `Preview` and `Builder` take an app id, a theme and an accent from the query string and accept only known ids, `light`/`dark`
and a `#rrggbb` value; everything else is ignored. A preview frame never saves its theme choice for the whole site (`data-preview`).

## 11. Versions and delivery

`Directory.Build.props` is the one place for the version: release `<major>.<minor>.<build>-<date>`, nightly `nightly-<build>-<date>`, local `local-<date>`. CI (`.github/workflows/build.yml`)
checks the icon sprite, builds, runs the tests and pushes a multi-arch image to GitHub Packages (`main` → `latest`, `dev` → `nightly`). The Docker build runs on the builder's own platform, so the
arm64 image needs no emulation for the compile.

## 12. The Studio

The design data used to be written in C# (the registry, the rulebook, the catalogue of components) and a change was a commit. The **Studio** ([STUDIO.md](STUDIO.md)) lets a person maintain it in the browser and
turns the data into releases, without giving up what the site is: the decisions are these.

- **State only where a data directory is set.** The site itself stays stateless. The Studio is on when a data directory is writable (the image sets `/data`, a volume), so the release compose file has no
  variable at all. One administrator, created by the setup assistant on the first visit (a name and a password, kept as a salted hash in the volume): the price is that whoever opens the assistant first
  becomes the administrator, and the answer to that is to do it right after the start. `MATDESIGN_ADMIN_PASSWORD` remains as an optional shortcut for automation.
- **The code stays the base, the Studio puts items on top.** An item with the id of a built-in app, rule or component replaces it; deleting the item brings the built-in one back. `DesignData` is the one place every
  page reads from, and `ToolkitFiles` rebuilds what depends on it (the colours of the apps, the bundle, its hash) when its version moves.
- **An append-only log per item.** A save writes a file with who, when, why and the whole item, atomically; the newest is the current state. History, diff and restore need nothing else, and a restore is a new revision
  that has to pass the same validator as a save, so an old state that is wrong today is refused with the reason.
- **One code for the contract.** The checks that hold the built-in examples in the unit tests (`MarkupChecks`) are the checks that hold a typed example in the Studio; markup that could harm a page is never drawn.
  The validators, the checks page and the release pipeline all call the same code, so a release cannot pass what the page fails.
- **Releases are immutable.** A release is a directory with its files (the toolkit, the design data with every example, and a static styleguide that draws the components with the release's own files), their hashes and a manifest, moved into place whole. The site offers no way to change or delete one: apps may depend on it. Apps learn of the
  newest from `/releases/latest.json` and copy the files; the Studio records by hand which release each app has.
- **It edits data, not the toolkit.** The CSS and the script are files of the repository, reviewed and tested there; the Studio never writes them. It is also not a CI system for the apps: its release is the design system.

## What it does not do

- It does not change the other apps. Adoption is a decision and a piece of work in each repository ([ADOPTING.md](ADOPTING.md)); the audit shows where each one stands.
- It does not run browser tests in the test project: the behaviour of the script is checked by reading it, and end to end by a real browser against the container (`tests/e2e`).
- It does not pick colours for the apps. The palette check proposes, a person decides.
