# The Studio

MatDesign is a site with the rulebook, the foundations, the components and the toolkit. The **Studio** is the part of it where all of that is
maintained and created: apps, rules, components and their examples, and where a change goes through a small pipeline (check, version, release)
before apps can take it. It is what makes MatDesign a CI/CD studio *for the design system*; it does not build or deploy the apps themselves
(see "What it does not do").

## How it fits

- **The site stays what it is** and works without the Studio: no volume, no password, read-only file system. The Studio is **on** when it has a writable data
  directory (`MATDESIGN_DATA`; the image sets `/data`, so the release compose file needs no setting at all). Without `MATDESIGN_DATA` (a plain `dotnet run`) it is off,
  and the Studio pages say what is missing.
- **Built-in data stays in code** (the registry, the rulebook, the catalogue). What the Studio saves are **items** in the data directory, and an item with
  the id of a built-in entry **replaces** it, the same way a file in `/data/catalog` replaces a built-in entry of Mattiverse. Deleting such an item brings the built-in one back.
- **Storage is an append-only log per item**: `studio/<kind>/<id>/<revision>.json` holds who, when, why and the whole item (or a deletion mark). The current
  state is the newest revision; history, diff and restore need nothing else. Writes go to a temporary file and are renamed.
- **One administrator, created by the setup assistant.** A Studio that has none leads every visitor to `/Studio/Setup`: a name and a password (at least 10 characters, typed twice).
  The account is a name and a salted PBKDF2 hash in `<data>/admin.json`, written once and never overwritten (two visitors at the same moment cannot both win), and the new
  administrator is signed in at once. **Whoever opens the page first becomes the administrator**, so it is done right after the start; that is the price of needing no password
  in the compose file. A file that cannot be read switches the Studio off with the reason instead of offering the assistant to a stranger. A cookie session, a short delay and a
  lock after repeated failures protect the sign-in; the page does not say whether the name or the password was wrong.
  If the password is lost: delete `admin.json` in the volume and restart the container, and the assistant shows again.
  `MATDESIGN_ADMIN_PASSWORD` is an optional shortcut for automation (CI, a throwaway stack): when it is set it is the password of the administrator `admin`, the assistant is skipped,
  nothing is written, and it wins over the account in the volume.
- **The checks of the pipeline are the checks of the tests** (classes that exist, icons that exist, labelled cells, unique ids, contrast of every accent,
  rules that exist). They live in one place and are used by both.

## How an app flows through the site

`DesignData` (a singleton) is the one place the pages read the apps from: the built-in registry with the Studio's items on top. Every save and delete in the Studio ends with
`DesignData.Reload()`, which raises its `Version`. `ToolkitFiles` compares that version on every use and, when it moved, rebuilds what depends on the apps: the apps part of the CSS
(the colours of the added and changed apps are written after the built-in ones, so they win), the bundle, its hash (the `?v=` of every page, so browsers fetch the new file) and the
archive. The palette, the menu badge of the palette, the preview frames, the builder and `/api/apps` read `DesignData` too. `AppValidator` holds for a typed app what the unit tests
hold for the built-in ones (id, name, glyph, port, contrast), and `IsSafe` runs again when an item is read from the disk, because the volume is a place a person can edit by hand:
an item that could not be written into a stylesheet is left out.

### Rules

A rule from the Studio stands in the book **after the last rule of its area** (the order of the book is the order of the areas, then the order inside each), is cited by an id of its area
(`FORM-17`), and is checked by a reviewer: only code can enforce a rule, so the check is not typed, and a built-in rule keeps what its code says. The id and the area of a rule that exists
do not change, because rules are cited by id in reviews, commits and the catalogue. `RuleValidator` holds the id (the area, a dash, two or three digits, an id nobody else has), the title,
the statement, the level and the source; `IsSafe` runs again for what is read from the disk. The level is stored by name, so a file can be read by a person. When stage 4 lets a component name
the rules it follows, a rule that a component of the Studio names cannot be deleted before the component lets go of it.

### Components

A component of the Studio is a `ComponentDoc`: an id (the anchor on its page), a name, a group (which decides the page and the place in its index), a summary, the rules it follows, "good to know"
lines and examples. An example has a title, markup, a note and a switch for "live" (drawn above its markup); the examples that a built-in component has with a script or a frame carry their
fields along when the component is edited. The form is one page with the preview beside it, outside the form element so that a button inside an example cannot submit the editor; examples are
added and removed by buttons that answer with the form as it is, nothing saved.

**One piece of code for the contract.** `MarkupChecks` (in `MatDesign.Core`) is what the unit tests hold the built-in catalogue to and what the Studio holds a typed example to: only classes the
toolkit ships, only icons of the sprite, no inline style, no handler of any name (`on...=`), no script or script address, no frame, object, link, meta or style element, nothing from another site,
labelled table cells, typed buttons, controls with an id, dialogs that are named and can be closed, icon-only buttons with a name, balanced tags, unique ids and resolved references. The checks of
the kinds `inline`, `elements` and `outside` are the ones that keep a page safe: markup that breaks one is never drawn, not even in the preview, and a component read from the disk that breaks one
is left out. `ComponentValidator` adds the rest: an id the page does not use itself, a name nobody has, a group, rules that exist, and ids inside live examples that no other component and no part of
the page has (all examples share one page; an example shown as markup only puts no id on it). `DesignData` merges the built-in catalogue with the items, placing an added component after the last
component of its group, and `NavFacts` carries the counts of the menu. A rule that a component of the Studio names cannot be deleted before the component lets go of it.

### History

Every item has a history page (`/Studio/History?kind=apps|rules|components|brand&id=...`), reached from its edit page (the History button, once the Studio has saved a revision of it) and from the item
in the activity list. It lists the revisions (who, when, why, the current one marked), compares any two (the fields that differ, read field by field, and for each the lines that differ: `-` gone,
`+` new, and the sign is in the text, not only in the colour) and restores one. **A restore is a new revision** that copies the old one, so nothing is lost and a restore can itself be undone. It goes
through only when the old revision would be accepted as it is today: `StudioHistory.CanRestore` runs the same validator as a save on it, with the other items as they are now (a port that another app
has taken since, a rule that is gone, a name somebody else has), and says why when it refuses. A deletion can be restored (the item is deleted again) while the item lives, and a rule that a component of
the Studio names is not deleted that way either. The activity list filters by kind, change and person.

### Checks

`DesignChecks` is the list of everything the design system holds itself to, run on the data as it is now (the built-in items too): every app, rule and component and the brand through its validator (contrast of every accent
in light and dark, the contract of the markup of every example, rules that exist), the palette (advice, never an error), the ids of the pages of the documentation (no id twice across the live examples of a page,
none that the page makes itself), groups that have components, the rules and apps that the page patterns cite, tokens with a description and a dark value, the naming of the toolkit's classes, the menus, and the
items on the volume that the site had to leave out (edited by hand into something unsafe or wrong). A check is a named record (`Id`, `Area`, `Name`, `What`, and code that yields `Finding`s); a finding is an
error or advice, and names the item it is about, so the page links to its edit page (or its history when it is gone from the site). The report is computed once for a version of the data and again when the data
changes; **the page `/Studio/Checks`, the badge of the menu item and the pipeline of stage 7 read the same report**. A release needs every check to pass; advice does not stop it.

## The pipeline

`Check` (everything above, on the built-in data plus the items) -> `Version` (the next number, the changelog text from the revisions since the last release)
-> `Build` (the toolkit files and the archive of that version) -> `Publish` (the release is stored, `/releases/<version>/...` and `/api/releases/latest` serve it)
-> `Notify` (the apps that use MatUi see that there is a newer version). A run is a record with a log per stage; a failed stage stops the run.

## Stages

Each stage ends with the build clean, the tests and the browser checks green, the container rebuilt and a local commit.

| # | Stage | State |
|---|---|---|
| 1 | **Foundation:** data directory and the revision store, sign-in, the Studio menu and overview, an activity list, volume and password in the stacks | **done** |
| 2 | **Apps:** create, edit, delete apps (name, id, accent, glyph, port, tagline, category), live contrast check; they appear in the registry, the palette, the apps CSS and the API | **done** |
| 3 | **Rules:** create, edit, delete rules (area, title, statement, level, source, why, do, don't); validation; they appear in the rulebook and the API | **done** |
| 4 | **Components:** create components with examples (title, note, markup, live or not) and the rules they follow; preview next to the editor; the contract checks of the tests run on save | **done** |
| 5 | **History:** per item the revisions, a diff between two, restore; the activity list filters by kind, item and person | **done** |
| 6 | **Checks:** one page runs every check and lists what fails and where, with a link to the item | **done** |
| 7 | **Releases:** the pipeline, versions, changelog, stored archives, `/releases/...` and `/api/releases/latest` | **done** |
| 8 | **Apps and versions:** which MatUi version each app uses, which are behind; polish, documentation, browser checks of the whole Studio | **done** |
| 9 | **Setup assistant:** the first visit creates the administrator (name and password, kept as a hash in the volume), so the release compose file has no password and no variables | **done** |
| 10 | **Brand and tokens** (docs/PRODUCT.md, stage 10): the item `brand`, a token editor with a live preview, the brand overlay in `matui.css`, releases and the archive, history, restore and checks | **done** |
| 14 | **Demo data and templates** (docs/PRODUCT.md, stage 14): the item `tenant`, the setup assistant's company and template steps, the Company page, the template layer under `DesignData`, a site that names itself after its company, `IDENT-09` | **done** |

### Brand

One item, `brand` of the kind `brand` (`studio/brand/brand/<revision>.json`), kept like the others: append-only, with history, a restore and an entry in the activity list. It holds the names of the company
(company, short name, tagline) and two maps from the name of a token to a value, **Light** and **Dark**. Until the Studio saves one, the built-in brand is in force (Mat, no token changed); *Reset to built-in* deletes the item.
`DesignData.Brand` is what every page reads, and a save ends with `DesignData.Reload()` like every other save.

**A brand changes tokens, never CSS (`COLOR-11`).** `BrandTokens` decides from `matui.tokens.css` which tokens a brand may set: the ones that are look (colours, radii, spacing, type sizes, fonts, line heights, durations, the
easing, the widths and heights of the layout) and not the derived or structural ones (the accent family, the layers, the safe areas, shadows, the pill radius, the colour picker's model). Each editable token has a kind
and limits, and `BrandValidator` holds every value to them: a colour (`#rgb`, `#rrggbb`, `rgb()`, `rgba()`), a length in px, rem or em, a number, a duration in ms or s, a font stack (names from a safe set of characters, ending in a generic family) or
an easing; no `;`, braces, comments, `url(`, `@import` or `expression` ever reaches a stylesheet (`IsSafeValue`, which also runs again for an item read from the disk). The limits are the ones of the rulebook: touch targets of 44 px
(36 px for compact controls, `MOBILE-04`), a body text of 14 px or more, a line height of 1.2 to 2, radii of 0 to 24 px, a type scale that only grows, spacing of 2 to 96 px. A token has a Dark value only if the dark set of the CSS has one for it.
A value that is the toolkit's own is not stored, so a brand that sets nothing writes nothing.

**Contrast is checked before a save and a release.** `BrandContrast` builds the surfaces of the brand (the toolkit's tokens with the brand's values over them: its light values over light, its dark values over dark) and runs the pairings the toolkit already holds
(`SchemeSurfaces.Pairings` for the text, the muted text, the status texts and the border of a control; `AccentReport.Checks` for the accent of every app), in the standard look and in every colour scheme, in every mode it has. A required pairing that is under AA is an error that
blocks the save, with its numbers and, for an accent, the app that is weakest; advice stays advice. A pairing that fails in the toolkit's own look already (an app added in the Studio that a scheme does not suit) is only reported when the brand makes it worse. The same check is
the `brand` check of `/Studio/Checks`, so a release is refused while the brand fails it.

**The overlay.** `ToolkitFiles.BrandOverlay` appends the brand to the apps part, after the apps' colours, so it wins: `:root { ... }` for the light values and the dark values twice, identical (`:root[data-theme="dark"]` and the system
`@media (prefers-color-scheme: dark)` block), exactly as `matui.tokens.css` writes its dark set. The number of CSS parts stays ten, and the bundle, its hash, `/ui/css/matui.apps.css`, the archive, the manifest, every release and its `design.json` (`brand`) follow `DesignData.Version`.
A colour scheme (a more specific selector) still wins over the brand on the tokens it sets. A value that is not safe, or not a value of its token within its limits, is not written.

**The page** `/Studio/Brand`: the names, then the tokens grouped by the groups of `matui.tokens.css` (name and description, the toolkit's value, an input for Light and one for Dark, a reset), a preview beside the form and the contrast of the brand below. The input of a colour is
a colour picker and a text field kept in step by `wwwroot/js/brand.js` (no inline script, no inline style); the script applies the unsaved values to `[data-brand-preview]` through the CSSOM (a few real controls: buttons, a field, a card, a tag, pills, a switch, a table, alerts, the switcher),
shows the preview in light or in dark whatever the page is in, and works out the contrast of the pairings that do not depend on the accent while you type, from the same list of pairings (`data-pairings`) the server uses. Check and Save run the whole check on the server, with the accent of every app.

### Versions

The Studio does not look into the apps. A person writes down which release of MatUi an app carries (Studio → Versions → the app; one button takes the latest release over), with a note such as the branch
of the app that has the copy. `VersionTracker` compares that with the releases: **Latest**, **N releases behind**, **not from a release** (a copy from before the first release, or an app's own build),
**no release yet**, or **not written down**. Being behind is advice (a check says so and links to the app's page), never an error: updating an app is the work of its own repository. The overview counts
the apps that are behind. The record is an item of the kind `usage`, one per app, with a history of its own.

### Company and templates

One item, `tenant` of the kind `tenant` (`studio/tenant/tenant/<revision>.json`), kept like the others with a history and a restore. It holds the **company** (full name), the **short name** (the word in "the Mat apps"), the **site name** (what the site calls itself: under the logo, in
the title of every page, in the footer and on the version line; `<short name> Design`, and exactly `MatDesign` for the demo), the **tagline** (under the site name in the menu), the **template** and, for a blank start, the **accent of the site**. `DesignData.Tenant` is what every page reads.
It comes from the first of: the Studio's item, the file `tenant.json` that the setup assistant wrote once, the environment (`MATDESIGN_TEMPLATE=demo|blank`, `MATDESIGN_COMPANY`; documented here and used by the browser checks to start a blank site), and the demo: an instance without any of
them (every volume from before the tenants) is the demo, unchanged. `TenantValidator` holds what is saved (names of a sensible length without markup or control characters, a template that exists, and for the blank template an accent that reaches AA in light and dark like the accent of any app),
and again for what is read from the disk; the check `tenant` of `/Studio/Checks` and the pipeline run it, and a company on the volume that is not valid is left out and reported.

**Templates.** `demo` is everything as it always was: the Mat apps with their accents, the audit snapshot, the Mat brand and the sample apps that the patterns and the colour page draw. `blank` is the toolkit's own content only: the rules and the documentation of the components stay (they document MatUi), the
Mat apps, the audit and the Mat brand are not offered, the brand is neutral (the names of the company, no token changed), the accent of the site is `#2563eb` unless the company chose another, and the app list starts empty: the company adds its apps in the Studio. The built-in `[data-app]` rules stay in
`matui.apps.css` (a file of the toolkit); they are simply not offered. Studio items sit on top of both. **Switching the template deletes nothing** (the Company page says so and asks first): the items the Studio saved stay, the apps a company added stay in the blank template, and switching back brings the demo layer back.

**The page** `/Studio/Company` (the menu, between Apps and Brand): the names and, for a blank start, the accent, with Check and Save and a History button; below them the two templates as cards and one button that switches to the other, behind a question (`data-confirm`) that says what the site stops offering.
**The shell and the pages** read the names from the tenant: the logo and name, the tagline and the version line, the title of every page, the footer, the sign-in and the setup pages, the start page, the Download page, the README of the archive, and in a release the changelog line, the manifest (`company`), `design.json` (`tenant`)
and the styleguide. Where a blank site has no apps its lists are empty states with the way out (the start page, the apps page of the site and of the Studio, the versions, the palette), and the audit page is not there. What still says Mat on a blank site is the toolkit's: the text of the rules, the components and their examples, the `mat-` classes and `MatUi` (stage 15 renames them).

### Setup

The assistant has three steps in one form: **your company** (the name, and an optional short name that is the first word when left empty), **where to start** (the two templates as cards: *Demo*, "The Mat apps as a sample, to look around and change", and *Blank*, "Start with your own brand and no apps"), and **the administrator** (as before).
With the script it shows one step at a time (Next checks the step it leaves, Back keeps what was typed, Enter goes on instead of sending an unfinished form); without it all three are on the page and the form is sent as it is. The server checks everything and shows the step that has the first problem. The company and the template are written to `tenant.json` once,
together with the administrator. A post that has only the name and the password (a script, an old client) creates the administrator and leaves the site what the environment says, the demo by default. The release `docker-compose.yml` still has no variable; `MATDESIGN_TEMPLATE` and `MATDESIGN_COMPANY` are for automation (a throwaway stack, the browser checks):
they are where the assistant starts, and what the site is when it never runs (`MATDESIGN_ADMIN_PASSWORD` skips it).

## Releases in practice

**Making one:** Studio → Releases → New release. The form suggests the next patch number (0.1.0 for the first), checks the version before anything runs (three numbers, not released yet, higher than the last
release), shows how the checks stand and what the Studio changed since the last release, and asks before it runs. The pipeline runs in the request (it takes milliseconds) and leads to the run, which is kept
whatever happened: five stages with their log, a failed stage (the checks, a version that is taken) stopping the run and skipping the rest. Only one run at a time.

**What a release is:** a directory `<data>/releases/<version>/` with `matui.css` (the bundle, naming the version in its first line, with the colours of the apps as the Studio left them), `matui.js`,
`matui.boot.js`, `matui-icons.svg`, the CSS parts under `css/`, `matui-<version>.zip`, `design.json` (apps, rules with their reasons, every component with its examples, the markup and the notes, and what is good to know), the **styleguide** (`styleguide.html` with `styleguide.css` and `sample.svg`: one static page with the rules, every component with its examples drawn live by the files of the release and their code beside them (coloured, with Copy; below on a narrow window), and the apps; it needs nothing from the site, so it opens from the directory) and `manifest.json` (version, date, who, note, run, the hash of the
toolkit, errors and advice at that moment, counts, the changelog, and every file with its size and SHA-256). The release is also saved as an item of the kind `releases`. It is published by moving the finished
directory into place, so a release is whole or not there. The site offers no way to change or delete one: apps may depend on it, so the volume is the only place that could.

**Taking one:** `GET /releases/<version>/<file>` (public, read-only, any origin, cached for good because it never changes), `GET /releases/latest.json` (the manifest of the highest version, never cached),
`GET /api/releases` (the list) and `GET /api/releases/latest`. An app compares the `version` of `latest.json` with what it carries, and copies the files of that release if it is newer; it checks them
against the hashes of the manifest. The changelog says what changed.

## Tests of the whole Studio

The unit tests cover each piece (the store, the sign-in, the validators, the shared markup checks, the diff, the restore, the checks, the pipeline, the versions) and the pages with a Studio switched on in memory
(`StudioHarness`). `StudioPagesTests` signs in with something in every list and holds **every page of the Studio** to the shell rules (one title, the menu with the Studio in it, the breadcrumb, the user,
the version, no inline style, no id twice, a label on every table cell), crawls the Studio for links that lead nowhere, and checks that every page of the menu marks itself as the open one. `tests/e2e/run.js`
drives a real browser through the Studio: the setup assistant (when the site has no administrator yet, it creates one with `STUDIO_NAME` / `STUDIO_PASSWORD`, default `admin` / `studio-dev`), sign-in and lock, creating, refusing, saving and deleting an app, a rule and a component, the history and a restore, the checks, a release and the files it serves, the
versions, and every page at the width of a phone. **Each run of the browser checks makes one release** (they never change), so the development volume grows by one release per run;
`docker compose -f docker-compose.dev.yml down -v` followed by `up -d --build` starts it clean.
`tests/e2e/blank.js` is the blank start in a second site (`MATDESIGN_TEMPLATE=blank MATDESIGN_COMPANY=Acme`, its own port and an empty data directory): the assistant's three steps by the real form, the shell saying the company and no Mat app anywhere, a first app, and the Company page switching to the demo and back.
`run.js` goes through the same steps for the demo (`STUDIO_COMPANY`, default Mat, and `STUDIO_TEMPLATE`, default demo) and has a short block for the Company page.

## What it does not do

- It does not build, test or deploy the apps (that is their repositories' workflow). Its release is the design system: the CSS, the script, the sprite and the rules.
- It is not an editor for the toolkit's CSS and script: those are files in the repository, reviewed and tested there. The Studio edits **data**: apps, rules, components, examples.
- It has no users beyond the one administrator, and no roles. It has no page to change the password either: to start over, delete `admin.json` (see above).
