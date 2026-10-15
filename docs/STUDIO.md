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
- **People with roles, the first one created by the setup assistant** (stage 16, "People and roles" below). A Studio that has nobody leads every visitor to `/Studio/Setup`: a name and a password (at least 10 characters, typed twice).
  The accounts are in `<data>/users.json`: a name, a role and a salted PBKDF2 hash each, written to a temporary file that replaces the old one under a lock (the file is whole or it is the old one). The assistant
  creates the first user, **always an administrator**, and signs them in at once (the file is written once; two visitors at the same moment cannot both win). **Whoever opens the page first becomes the administrator**, so it is done right
  after the start; that is the price of needing no password in the compose file. A file that cannot be read switches the Studio off with the reason instead of offering the assistant to a stranger. A cookie session, a short delay and a
  lock after repeated failures (per name and per address) protect the sign-in; the page does not say whether the name or the password was wrong, or whether the account exists or is disabled.
  If the only administrator's password is lost: delete `users.json` in the volume and restart the container, and the assistant shows again (the other people have to be created again; the history keeps their names).
  `MATDESIGN_ADMIN_PASSWORD` is an optional shortcut for automation (CI, a throwaway stack): when it is set, a built-in administrator named `admin` has that password, the assistant is skipped, that user is not written to
  the volume, and it wins over a stored user of the same name. A data directory from before stage 16 has an `admin.json`: it becomes the first user (see "Migration from admin.json").
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

Every item has a history page (`/Studio/History?kind=apps|rules|components|brand|assets&id=...`), reached from its edit page (the History button, once the Studio has saved a revision of it) and from the item
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
| 12 | **Token editor per component, per app and per colour scheme** (docs/PRODUCT.md, stage 12): the 68 component properties next to the real control with a live preview, overrides on `[data-app]`, tokens inside each colour scheme, the 44 px rule for touch targets | **done** |
| 14 | **Demo data and templates** (docs/PRODUCT.md, stage 14): the item `tenant`, the setup assistant's company and template steps, the Company page, the template layer under `DesignData`, a site that names itself after its company, `IDENT-09` | **done** |
| 15 | **White label build** (docs/PRODUCT.md, stage 15): the item `whitelabel` (prefix and product name), the renamer, the Build stage with checks on the renamed copy, a trial build, history, restore and a check | **done** |
| 16 | **People and roles** (docs/PRODUCT.md, stage 16): `users.json` (more than one user, a role each), the three roles and their capabilities, a capability declared by every page and every handler that changes something and checked on the server, the people page, the account page, a lock-out per name and address, sessions that follow the person, history and activity that name them, a check, `CODE-10` | **done** |

| 13 | **Brand assets** (docs/PRODUCT.md, stage 13): the item `assets`, files in the volume under their SHA-256, logo, word mark, favicon, app icon and fonts checked by content, served under `/ui/brand/`, `@font-face` in `matui.css`, `brand/` in every release and the archive, history, restore and checks | **done** |

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

### Components, apps and schemes (stage 12)

The brand item and its history grow by three maps; nothing else is new in the store.

- **`Components`** on the brand: the name of a documented property (`--mat-btn-radius`, read from the `@prop` lines of the CSS by `ComponentProperties`) to one value, the same in light and dark. `ComponentTokens` turns each of the 68 into a
  `PropertyToken` (a `BrandToken` for the kind and the limits, whether it is a touch target, what the control uses today). The limits are in code: a radius of 0 to 24 px, padding and gaps of 0 to 48 px, a border or the indicator of a tab of 0 to 4 px,
  the width of a dialog or a toast of 200 to 960 px, a weight of 100 to 900, the text of a table or the breadcrumb of 12 to 22 px and of a small label (tag, pill, badge) of 11 to 18 px, the parts of a switch and a check within a range (and the knob smaller than its track).
  A new property needs no code: it gets the limits of its name (`-radius`, `-pad-x`, `-font-size` ...), and a test fails while a length or a number has none.
- **The 44 px rule.** The heights of the controls that are pressed (`--mat-btn-height`, `--mat-input-height`, `--mat-tabs-height`, `--mat-menu-item-height`, `--mat-nav-item-height`) fall back to `--mat-control-h`, which the tokens make 44 px under `(pointer: coarse)`. They take
  32 to 72 px. A value of **44 px or more** is written on `:root` and applies everywhere; a value **under 44 px** is written inside `@media (pointer: fine) { :root { ... } }`, so on a touch screen the property is unset and the control keeps its 44 px (`MOBILE-04`). Under 32 px is refused.
  The editor says so at the field, and its preview does what the CSS does: a phone ignores a height under 44 px. `(pointer: fine)` is the primary pointer of the device (a laptop with a touch screen counts as fine); a hybrid that also takes touches keeps the small control for the touches.
- **`Schemes`** on the brand: a colour scheme id to a light and a dark map (`SchemeValues`). Only a token that the scheme itself sets in that mode can be changed (paper and contrast in both, black in dark only), with the kind and limits of the token. It is written after the scheme blocks of
  `matui.tokens.css` with the same selectors (`:root[data-scheme="paper"]`, `... [data-theme="dark"]` and the system block), so it wins over them, the dark set twice and identical. `BrandContrast` reads the surfaces of a scheme with its override over the scheme's own values, so every pairing and the accent of every app are checked
  in that scheme; a failing one is an error, with the scheme named. A value that equals the scheme's own is not stored.
- **Per app.** `MatApp` gets `Tokens` (light and dark maps) and `Properties`, both null (and absent from the stored JSON) for an app that sets nothing. The tokens open to an app are the **non-colour tokens of the groups Shape, Spacing, Type and Motion** (`AppOverrides.Allows`): the app owns its accent and its shape, the surfaces,
  lines and text are the brand's so that the apps of a company stay one family, and the layout widths and control heights belong to the brand (the heights of single controls are properties and carry the 44 px rule). Every component property is open. The limits are the brand editor's, and the type scale is checked with the brand's sizes under the app's. A dark value is accepted only for a token that has one (none of the
  subset has today; the writer and the validator handle it for the day one does). It is written on `[data-app="id"]`, the dark set on `:root[data-theme="dark"][data-app="id"]` and in the system block, and the properties likewise (a height under 44 px inside `@media (pointer: fine)`).
- **The order the values win in** is the order of `matui.css`: the toolkit's defaults, the colours of the apps, the brand (tokens, properties, schemes), then the overrides of each app, with the colour scheme the user chose last (a scheme is a more specific selector). `ToolkitFiles.Build` puts them together in that order; ten CSS parts as before.
- **Pages.** `/Studio/Brand/Components` (a tab of the brand next to *Names and tokens*) lists the properties by component with the description, the type and limits, the default, an input of the type and a reset, and beside each group the first example of the documentation with a live preview (`PropertyPreviews`: the
  mapping from a group to an example of the catalogue as data; a dialog, a toast and a context menu, which are on a page only while something opens them, are drawn still). `/Studio/Brand` has a section per colour scheme. `/Studio/Apps/Edit` has the section *Overrides of this app* (the same rows and previews, the previews wear the `data-app` of the app).
  `brand.js` sets the typed values on `[data-brand-preview]` through the CSSOM and tells a value that is outside its limits at the field. A control of a preview is cut off from every form (`form="pv-none"`), so Enter in it can never save the editor.
- **Checks, history, release.** The `brand` check validates the properties and the schemes, the `apps` check the overrides of every app; the restore of a revision that fails is refused with the reason; the release pipeline refuses on either; the release (bundle, apps part, archive, `design.json` with `brand.components`, `brand.schemes` and `overrides` of an app, the changelog lines
  `component --mat-btn-radius`, `paper light --mat-bg`, `property --mat-btn-radius`) carries all of it.

Not done here: the static styleguide of a release does not show the company name or the property tables (it lists the rules, the components and the apps as before).
### White label

One item, `whitelabel` of the kind `whitelabel` (`studio/whitelabel/whitelabel/<revision>.json`), kept like the others: append-only, with history, a restore and an entry in the activity list. It holds a class **prefix** and a **product name**
(`WhiteLabelSetting`); until one is saved, or while it keeps the toolkit's own `mat` and `MatUi`, a release is what it always was, byte for byte. The page is `/Studio/Releases/WhiteLabel` (menu: Release, and a button on the Releases page).
Rules of the setting (`WhiteLabel.Validate`, run again in the browser while you type and for an item read from the disk): a prefix is 2 to 8 lower-case letters and digits starting with a letter, none of `data`, `aria`, `js`, `is`, `has`, `on`,
`ng`, `v`, `x`, `matui`, nothing that starts with `xml`; a name is 3 to 24 letters starting with a capital (it need not end in `Ui`), not `MatUi` (unless the prefix is `mat`), not `Mat` or `MatUi` in other capitals.

**The renamer** (`WhiteLabel`, in `MatDesign.Core`, pure) works on token boundaries in one pass: `mat-xxx` (classes, ids, animation names, the keys of `localStorage`, the sprite ids `mat-i-` and `mat-il-`, the bare `"mat-"` of a selector),
`--mat-xxx`, `data-mat-xxx`, `mat:xxx` (events), `_matXxx` (the private properties the script puts on elements), `MatUi` (the global, comments, the first line of the bundle, the README) and `matui` in a file name (it becomes the name in lower case:
`acmeui.css`, `acmeui.boot.js`, `acmeui-icons.svg`, `acmeui.manifest.json`, `acmeui-<version>.zip`). Nothing is renamed when a letter, digit, `_`, `$` or `-` stands in front of it (`format-`, `automat-`, `Mattiverse`, `Matdo`, `x-mat-y`) or a letter, digit
or `_` follows `MatUi` (`MatUiVersion`); addresses with a scheme and the repository's tool `matui-check.js` are never touched. The design data (`design.json`) is renamed as JSON strings and keys, not as text, because the serializer writes a quote as `"`,
which would hide the name behind a digit. The traps found in `matui.js` and how each is handled are listed in docs/ARCHITECTURE.md, "White label build".

**In the pipeline.** The Build stage assembles the files as before (`ReleaseService.AssembleFiles`), and when a setting is in force `WhiteLabelBuild.Apply` renames every file and its path, writes a hash of the copy into the first line of the bundle, the manifest
and the README, opens the archive, renames its entries, writes its manifest again from the renamed entries and writes the archive again (fixed time stamps, so the same input is the same archive). The log of the stage says which rename was applied and how
many replacements each file got. Then `WhiteLabelChecks` runs on the result, in C# because the image has no node, and **a finding fails the stage** (nothing is published): no name of the source left in any file or file name; every class of the source
stylesheet is in the renamed one and every class the script, the examples and the style guide use exists; every custom property that is used is defined, and every renamed class and property is what the source had; every icon that is named is in the sprite;
the script has the same strings, comments, regular expressions and brackets as the source, all well formed, and defines its global once; the braces of the stylesheets balance; the length of every file differs from the source by exactly what its replacements add;
every renamed example passes `MarkupChecks` against the renamed stylesheet and sprite; the files are named after the product; the archive holds the renamed files and a manifest with their hashes. Only what the rename broke is reported: what was already
wrong in the source is found by running the same look at the original and subtracting. The manifest of the release names the setting (`whiteLabel`).

**The page** shows the setting, a table of before and after that follows the typing, the list of what is renamed, and **Check a trial build**: the renamer and every check on the files of the next release, with the findings and the replacements per
file, without making a release. Saving a setting whose trial fails is refused. The check `whitelabel` of `/Studio/Checks` runs the same trial for a saved setting (an error stops a release at Check; a setting on the volume that is not valid is reported too).
`POST /Studio/Releases/WhiteLabel/rename?prefix=&name=` (administrator only) answers a text renamed by the same function; the browser checks (`tests/e2e/white-label.js`) put the browser behind it to run the renamed toolkit.

### Brand assets

One more item, `assets` of the kind `assets` (`studio/assets/assets/<revision>.json`), with its own history, restore, activity and check. It holds the **logo** (with an optional variant for dark surfaces and an alt text, which is required), the **word mark** (the same),
the **favicon** (SVG, PNG or ICO), the **app icon** (a PNG of 512 px) and a list of **font faces** (family, weight 100 to 900, style, file). It holds no bytes: the files live in `<data>/assets/<sha256>.<ext>` (`AssetStore`), are written once under the hash of
their content and **never overwritten or deleted** (the class has no way to), so every revision can be restored; a replaced logo stays on the volume. `DesignData.Assets` is what the pages read, and a save ends with `DesignData.Reload()` like every other save.

**Checked by content, not by name (`IDENT-08`).** `BrandAssetValidator` (in `MatDesign.Core`, no web types) decides the type of a file from its first bytes (PNG, ICO, WOFF2, WOFF) or by parsing it as XML (SVG); the name of the file and the type the browser sent are never looked at,
so a program called `logo.png` is refused. Limits: SVG 100 KB, PNG 500 KB and 2048 px (a PNG is walked chunk by chunk, with its checksums, and nothing may follow its end), ICO 200 KB (its directory is read), a font 400 KB (the length in its header must be the length of the file),
at most 12 faces in 3 families, 6 MB together; a logo may be wide (up to 8 to 1), a PNG favicon is square (16 to 512 px), an app icon is exactly 512 x 512. **An SVG is read with an XML reader that forbids a DTD** (no entity, so no external file and no entity bomb) **and an allowlist**
of elements (shapes, gradients, clip paths, text, `use`) and attributes: no script, foreignObject, style element, image, link, animation or filter; no attribute that starts with `on`; a `href` is only a `#fragment`; a `url(` only points into the file; no `javascript:` or `data:`
anywhere (also split by white space or written as character references); a `style` attribute only with presentation properties and plain values; no namespace but SVG and xlink (an Inkscape file is saved as plain SVG); a size over 4096 px or no size at all, and nesting or `use` bombs, are refused.
A refused file says why. A font family is letters, digits, spaces and dashes, is not a generic family and not a name the toolkit's own stacks (`--mat-font`, `--mat-mono`) use, and a face of the same family, weight and style is refused twice.

**The page** `/Studio/Brand/Assets` (the menu item *Logo and fonts*, and a link on the brand page): a card per slot with a preview on a light and on a dark surface (the browser's own `Canvas` colour under `color-scheme`, no literal colour), the file, its address, the checks run on its bytes now (type by content, size,
dimensions, hash, alt text), and a normal multipart form with the anti-forgery token and the toolkit's file upload (`FORM-21`) to upload, replace or remove. The request limit of this page is its own (a megabyte). Every change is one new revision, saving the same thing again is none. The fonts have a table with a live **specimen**:
`wwwroot/js/brand-assets.js` loads each file from its served address under a temporary family name (`mat-specimen-N`) and says "Loaded", or in words that the browser could not read it. A family is used on the site by naming it in the brand editor, in the stack of `--mat-font` or `--mat-mono`
(`"Acme Sans", system-ui, sans-serif`, shown on the page; the brand editor already accepts it). A restore is refused when a file the revision refers to is missing from the volume or no longer valid. *Remove all* deletes the item (the files and the history stay).

**Serving and releasing.** The site serves `/ui/brand/<name>` (the name is the slot and the first ten digits of the hash, `logo-1a2b3c4d5e.svg`, `fonts/acme-sans-400-normal-1a2b3c4d5e.woff2`, so it follows the content and is cached for good: `public, max-age=31536000, immutable`), with the type of its
extension, `X-Content-Type-Options: nosniff` and, for an SVG, `Content-Security-Policy: default-src 'none'; style-src 'none'`; only what the current assets name, and only while the file still has its hash. `ToolkitFiles.AssetsOverlay` writes one `@font-face` per face after the brand's tokens
(`font-display: swap`, `src: url("brand/fonts/<name>") format("woff2")`, **relative to the stylesheet**; the part `css/matui.apps.css` has `../brand/fonts/...`), and nothing for a brand without fonts. A release writes the files under `brand/` with their hashes in `manifest.json`, the
archive (`matui/brand/`, its `matui.manifest.json` and its README, which says where the files go) and the manifest of the toolkit list them, and `design.json` has an `assets` section (files as they lie in the release, alt text, fonts); without assets none of this is written and every file is what it was.
The shell uses them on the site's own pages: the logo replaces the glyph tile (a wide one carries the name itself), the word mark replaces the name, the favicon and the app icon (`apple-touch-icon`) replace the favicon link; a dark variant is a second image that the theme shows (`mat-brandmark--light` / `--dark`).
A sample app in a preview keeps its own logo. The check `assets` (every file is there, has its hash, is still valid, the alt texts, the families, the budget) is a check of `/Studio/Checks` and of the pipeline, so a release is refused while it fails.

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

### People and roles

More than one person works in an instance (docs/PRODUCT.md, stage 16; rule `CODE-10`). The decisions: **who may change what is decided on the server, from a role**; the role is read again on every request; and no secret enters the history.

**Users.** `<data>/users.json` (`UserStore` in `MatDesign.Core`, `StudioUser`): per person a stable `Id` (the slug of the name and six hex digits, a valid folder name), a `Name`, a `Role`, the salted PBKDF2 hash (`Salt`, `Hash`, `Iterations`: the scheme
`AdminAccount.HashPassword` always had, 210,000 iterations of SHA-256, so an old hash verifies), `MustChange`, `Created`, `CreatedBy`, `LastSignIn`, `Disabled` and a `SessionStamp`. The file also holds `Retired`: the names of removed people.
A name is 2 to 40 letters (also with accents), digits, single spaces, dots, dashes, underscores or `@`, starts with a letter or digit, ends without a space, is unique **without regard to case**, and **never given again** once a person with it was removed
(the history names people, so a name has to mean one person). A name cannot change. Writes are atomic (a temporary file, flushed, then moved over the old one, under a lock, a few tries when a scanner holds the file); readers open the file so that a writer may replace it.
**A file that cannot be read is an error, never an empty list**: the Studio then stays off with the reason (an empty list would show the setup assistant to the first visitor), and the check `users` reads the file again and reports it if it changes into nonsense, or is gone,
while the Studio runs. A password is at least 10 characters, not the name, not one of 100 common ones (`PasswordPolicy`), at most 200; the same rule holds for the setup assistant, a new person, a reset and a change. It is shown nowhere, never logged and compared in constant time
(also for a name that does not exist: a dummy hash is computed).

**Roles** (`StudioRole`, `Capability`, `StudioRole.Can(role, capability)`: data in code, one function decides; a role that does not exist, or none, may do nothing):

| Capability | What it is | Administrator | Editor | Viewer |
|---|---|:-:|:-:|:-:|
| `ViewStudio` | open and read every page except the people | yes | yes | yes |
| `EditDesign` | create, change and delete apps, rules, components, the brand and its files | yes | yes | no |
| `RestoreHistory` | restore an old revision (of an item the person may edit: an editor cannot restore the company or the white label setting) | yes | yes | no |
| `RunChecks` | run the checks again | yes | yes | no |
| `ManageReleases` | make a release (it publishes files that apps copy: administrators only, by decision) | yes | no | no |
| `ManageVersions` | write down which release an app uses | yes | no | no |
| `ManageCompany` | the company, its names, the template | yes | no | no |
| `ManageWhiteLabel` | the class prefix and product name of the releases (and the rename endpoint) | yes | no | no |
| `ManageUsers` | the people | yes | no | no |

**Enforcement.** Every page model of the Studio carries `[RequireCapability(...)]` (what opening it needs) and every `OnPost*` handler its own (what the change needs); `StudioPageModel` checks them on the server before the handler runs, together with
`ViewStudio`, from the person as the people have them now, and answers a person who lacks one with the page `_Forbidden` (the shell, one h1, which role would do, the way out; **status 403**), never the handler and never a bare error. A GET of an empty form for a new item needs
the right to create it. The minimal endpoint that writes (the renamer of the white label build) uses `RequireCapability(...)` on the route (403 too, an unauthenticated request is sent to the sign-in). The anti-forgery check of Razor Pages runs first and stays.
A control the person may not use is **left out or turned off with the reason**: the lists draw no New button, a form of the design is a `fieldset disabled` with the notice `data-read-only` ("Read only. You are signed in as ..., viewer. This needs the role editor.") and no Save, Check or Delete;
the menu holds every page the role may open (the people only for administrators). **Hiding is never the protection**: the tests post every changing address as each role. The tests that keep it so: `StudioCapabilityDeclarationTests` reads the assembly (every page model has a class-level declaration, every
`OnPost*` handler exactly one of its own, the list of what each needs is written out in the test, every endpoint that is not a page and changes something declares one), and `StudioAccessTests` crawls every page and every changing address as an administrator, an editor and a viewer
(a 403 for the roles that lack the capability, and the data directory byte for byte as it was).

**Session.** The cookie carries **the id of the person and the stamp of the account** (and the name, for the shell and the anti-forgery token), no role (`StudioSession.PrincipalOf`). On every request `StudioService.CurrentUser` reads the person again: a role that changed takes effect at once (also in a session that is open), a person who is disabled or removed is
out at the next click, and a changed password, a reset or a disabled account changes the stamp, so every older session ends (a person who changes their own password is signed in again with the new stamp). A person whose password an administrator chose (`MustChange`) lands on `/Studio/Account` after the sign-in and
nothing else opens until they have chosen their own (the account page and signing out are the only things open to them; the endpoint that changes something refuses them with 403). The **lock-out** counts failures per user name **and** remote address (five in a minute lock that pair for a minute, as before) and per address alone
(thirty): one person cannot lock another out by guessing a name from another address, and a name that does not exist is counted the same, so the lock does not tell whether it exists. The message of a failed sign-in is one sentence for a wrong name, a wrong password and a disabled account.

**The pages.** `/Studio/Users` (administrators): the list with the toolbar of every list (search, filter by role and state, sort), name, role as a pill and as text, last sign-in, state (*Active*, *Must change password*, *Disabled*), a pager, and a table of what each role allows. `/Studio/Users/Edit`: a new person (name, role, a first password: **Generate**
fills a random one in the browser, `wwwroot/js/users.js`, `crypto.getRandomValues`, four groups of four characters, with a Copy button; it is shown only there and has to be changed at the first sign-in), or an existing one with a form per change: the role, the password (a reset: every session of the person ends, `MustChange` is set), disable or enable, remove (`data-confirm`).
**The guards are the store's, the page leaves out what would be refused and says why:** the last active administrator cannot be deleted, disabled or demoted (not even by themselves), nobody can delete or disable themselves, nobody changes their own role or resets their own password here, the built-in administrator of the environment cannot be changed.
`/Studio/Account` (every role, also while the password must change): who you are, your role and what it allows (every capability in words, *Allowed* or *Not allowed*), change the password (the current one is required; the new one is held to the policy and differs from the old; every other session ends) and sign out. The shell shows the name and role (user menu, "Your account"),
the overview says who is signed in.

**History and activity.** Each change to the people is an entry in the append-only store, kind `users`, one item per person (`StudioPeople` pairs each change with its entry, so a page cannot change a person without leaving one): *Created as editor*, *Role changed from editor to viewer*, *Disabled*, *Enabled*, *Password reset by an administrator*,
*Changed their own password*, *Deleted the user ...*. **Who did it is `by`, whom is the item and the note; the data holds only the id, the name, the role and whether the account is disabled: never a password, a hash, a salt or a stamp** (a test scans the volume, the pages and the log after a scenario). The activity list filters by kind and by person, and shows the name of a
person for these items. What a person saved stays under their name when they are removed. A release names who started it (`by`) as before.

**The check `users`** (area Studio; the menu badge and the release pipeline read it like every check): nobody can sign in (the Studio waits for its setup), no active administrator, a file that cannot be read or is gone, a password hash that is not of the known format, a role that does not exist. An error stops a release at Check.

### Migration from admin.json

A data directory from before stage 16 has `admin.json` (name, salt, hash, iterations, created). When the Studio starts and finds no `users.json`, it turns that file into the first user: an **administrator**, the same name, the **same hash** (nobody changes a password), `Created` kept, `CreatedBy` "migration from admin.json".
`users.json` is written first, then `admin.json` is renamed to `admin.json.migrated` and never read again, so the hash is never lost and a later `admin.json` is not taken for an account; the activity list gets the entry "Migrated from admin.json" by `migration`. A broken `admin.json` is an error, as a broken `users.json` is.
Sessions from before the upgrade carry no id: everybody signs in once. **Going back to a release from before stage 16 is not supported** (that program would find no `admin.json` and offer its setup assistant). While `MATDESIGN_ADMIN_PASSWORD` is set the old file is left alone, as before. The browser checks and the other automation that sign in as `admin` / `studio-dev`
work unchanged: through the setup assistant (the first user is an administrator) or through the variable (a built-in administrator).

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
The block **People and roles** of `run.js` makes an editor, a viewer and an administrator through the form, signs each in in a browser of its own (separate contexts) and sees what each sees and cannot do (real forbidden requests give 403 and save nothing), a role that changes at the next click of an open session, a disabled person out at once,
the first password that must be changed, a password change that ends the other sessions, the guards for oneself and the activity that names them, at 390 px too; it removes what it made (`PEOPLE_SHOTS=<folder>` writes screenshots). The unit tests add the whole role matrix, the store (atomic writes, corruption, migration from an `admin.json` of the old code, names, guards, passwords), the lock-out,
the reflection test of the declarations and the crawl as each role (`StudioRoleTests`, `UserStoreTests`, `StudioCapabilityDeclarationTests`, `StudioAccessTests`, `StudioPeopleTests`).

## What it does not do

- It does not build, test or deploy the apps (that is their repositories' workflow). Its release is the design system: the CSS, the script, the sprite and the rules.
- It is not an editor for the toolkit's CSS and script: those are files in the repository, reviewed and tested there. The Studio edits **data**: apps, rules, components, examples.
- It has three fixed roles and no more: a role cannot be edited, nobody can be given a single capability, and there is no "owner" above the administrators, no groups, no two-factor sign-in and no single sign-on. It does not
  send a message to a new person (the administrator hands the first password over), and there is no "forgot my password" mail: an administrator sets a new one, and the only administrator starts over with `users.json` (see above).
