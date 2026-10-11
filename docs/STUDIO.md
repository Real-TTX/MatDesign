# The Studio

MatDesign is a site with the rulebook, the foundations, the components and the toolkit. The **Studio** is the part of it where all of that is
maintained and created: apps, rules, components and their examples, and where a change goes through a small pipeline (check, version, release)
before apps can take it. It is what makes MatDesign a CI/CD studio *for the design system*; it does not build or deploy the apps themselves
(see "What it does not do").

## How it fits

- **The site stays what it is** and works without the Studio: no volume, no password, read-only file system. The Studio is **off** until a data
  directory (`MATDESIGN_DATA`, default `/data`) is writable **and** `MATDESIGN_ADMIN_PASSWORD` is set. With the default release stack it is off, and the
  Studio pages say how to switch it on.
- **Built-in data stays in code** (the registry, the rulebook, the catalogue). What the Studio saves are **items** in the data directory, and an item with
  the id of a built-in entry **replaces** it, the same way a file in `/data/catalog` replaces a built-in entry of Mattiverse. Deleting such an item brings the built-in one back.
- **Storage is an append-only log per item**: `studio/<kind>/<id>/<revision>.json` holds who, when, why and the whole item (or a deletion mark). The current
  state is the newest revision; history, diff and restore need nothing else. Writes go to a temporary file and are renamed.
- **One administrator.** The password comes from the environment, so there is no account to register and nothing to claim on a first visit. A cookie
  session, a short delay and a lock after repeated failures protect the sign-in.
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

Every item has a history page (`/Studio/History?kind=apps|rules|components&id=...`), reached from its edit page (the History button, once the Studio has saved a revision of it) and from the item
in the activity list. It lists the revisions (who, when, why, the current one marked), compares any two (the fields that differ, read field by field, and for each the lines that differ: `-` gone,
`+` new, and the sign is in the text, not only in the colour) and restores one. **A restore is a new revision** that copies the old one, so nothing is lost and a restore can itself be undone. It goes
through only when the old revision would be accepted as it is today: `StudioHistory.CanRestore` runs the same validator as a save on it, with the other items as they are now (a port that another app
has taken since, a rule that is gone, a name somebody else has), and says why when it refuses. A deletion can be restored (the item is deleted again) while the item lives, and a rule that a component of
the Studio names is not deleted that way either. The activity list filters by kind, change and person.

### Checks

`DesignChecks` is the list of everything the design system holds itself to, run on the data as it is now (the built-in items too): every app, rule and component through its validator (contrast of every accent
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

### Versions

The Studio does not look into the apps. A person writes down which release of MatUi an app carries (Studio → Versions → the app; one button takes the latest release over), with a note such as the branch
of the app that has the copy. `VersionTracker` compares that with the releases: **Latest**, **N releases behind**, **not from a release** (a copy from before the first release, or an app's own build),
**no release yet**, or **not written down**. Being behind is advice (a check says so and links to the app's page), never an error: updating an app is the work of its own repository. The overview counts
the apps that are behind. The record is an item of the kind `usage`, one per app, with a history of its own.

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
drives a real browser through the Studio: sign-in and lock, creating, refusing, saving and deleting an app, a rule and a component, the history and a restore, the checks, a release and the files it serves, the
versions, and every page at the width of a phone. **Each run of the browser checks makes one release** (they never change), so the development volume grows by one release per run;
`docker compose -f docker-compose.dev.yml down -v` followed by `up -d --build` starts it clean.

## What it does not do

- It does not build, test or deploy the apps (that is their repositories' workflow). Its release is the design system: the CSS, the script, the sprite and the rules.
- It is not an editor for the toolkit's CSS and script: those are files in the repository, reviewed and tested there. The Studio edits **data**: apps, rules, components, examples.
- It has no users beyond the one administrator, and no roles.
