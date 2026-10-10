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
`--mat-app-accent-text`). Hover, the tints, the focus ring, the brand gradient and the dark variant follow. In dark mode the accent is lightened (74 % accent, 26 % white) and the
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

### Long pages and one page for the components

The components are not a list of pages to click through but one page, grouped, with an index beside it (`mat-with-toc`, `mat-toc`): a sticky column from 1100 px, a "Jump to" dialog below that
(full screen on a phone). `data-scrollspy` marks the link of the section at the top; it computes per index from the position of the sections, so the index beside the content and the one in the dialog
agree and two indexes on one page do not disturb each other. Because every example of every component is on the same page, **all ids share one page**: a test checks that no id appears twice and
that every `#link` leads to a section that exists.

## 5. The documentation cannot drift from the toolkit

- **Components** are data (`ComponentCatalog`): each example is one raw string, rendered live and shown as source. A test checks every one: the classes exist in the toolkit's CSS,
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

Plain CSS in seven parts that are bundled in the order of the cascade (`tokens`, `base`, `shell`, `controls`, `lists`, `dialogs`, `apps`) and one dependency-free script that wires
everything with delegated listeners and `data-` attributes, so server-rendered and dynamically added markup behave the same. System fonts only. Icons are one inline SVG sprite
(`tools/icons.js` builds it; CI fails when it is stale).

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

The site is stateless: no database, no cookies, no secrets, no volume, no Docker socket. The image runs as the unprivileged user of the base image, the file system is read-only (a tmpfs for
`/tmp`), all capabilities are dropped and `no-new-privileges` is set. `Preview` and `Builder` take an app id, a theme and an accent from the query string and accept only known ids, `light`/`dark`
and a `#rrggbb` value; everything else is ignored. A preview frame never saves its theme choice for the whole site (`data-preview`).

## 11. Versions and delivery

`Directory.Build.props` is the one place for the version: release `<major>.<minor>.<build>-<date>`, nightly `nightly-<build>-<date>`, local `local-<date>`. CI (`.github/workflows/build.yml`)
checks the icon sprite, builds, runs the tests and pushes a multi-arch image to GitHub Packages (`main` → `latest`, `dev` → `nightly`). The Docker build runs on the builder's own platform, so the
arm64 image needs no emulation for the compile.

## What it does not do

- It does not change the other apps. Adoption is a decision and a piece of work in each repository ([ADOPTING.md](ADOPTING.md)); the audit shows where each one stands.
- It does not run browser tests in the test project: the behaviour of the script is checked by reading it, and end to end by a real browser against the container (`tests/e2e`).
- It does not pick colours for the apps. The palette check proposes, a person decides.
