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
| 5 | **History:** per item the revisions, a diff between two, restore; the activity list filters by kind, item and person | open |
| 6 | **Checks:** one page runs every check and lists what fails and where, with a link to the item | open |
| 7 | **Releases:** the pipeline, versions, changelog, stored archives, `/releases/...` and `/api/releases/latest` | open |
| 8 | **Apps and versions:** which MatUi version each app uses, which are behind; polish, documentation, browser checks of the whole Studio | open |

## What it does not do

- It does not build, test or deploy the apps (that is their repositories' workflow). Its release is the design system: the CSS, the script, the sprite and the rules.
- It is not an editor for the toolkit's CSS and script: those are files in the repository, reviewed and tested there. The Studio edits **data**: apps, rules, components, examples.
- It has no users beyond the one administrator, and no roles.
