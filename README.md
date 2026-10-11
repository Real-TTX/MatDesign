<div align="center">

<img src="src/MatDesign.Web/wwwroot/favicon.svg" width="96" alt="MatDesign" />

# MatDesign

**One look for every Mat app: the rules, the tokens, the shell, every control, and the toolkit they are built from.**

Every Mat app has its own colour and the same design. MatDesign is the reference for that design: a website with the **rulebook**
(every rule about how an app looks and behaves, with a stable ID such as `SHELL-03`), the **foundations** (colour, type, space, motion, icons),
the **shell** (menu left with submenus, badges and a switcher between menus, breadcrumb above the content), **49 components** with live examples and the markup to copy (forms, pickers, lists,
dialogs, ...), a **layout system** (twelve columns, breakpoints) and **utility classes**, the **page patterns**,
a **builder** that checks an accent colour, and **MatUi**, the toolkit itself: four files you copy into an app.

</div>

![The overview of MatDesign](docs/images/overview.png)

---

## The rules in one minute

These are the rules that make a Mat app look like a Mat app. They are in the rulebook as `Must` and the site checks what it can.

- **The menu is always on the left, the logo above it.** At the bottom: the user (a click opens a context menu with *Show account*, *Sign out* …) next to the
  **3-state theme button** (System / Light / Dark), and below them the **version**. The menu scrolls on its own, separately from the content.
- **The menu can have badges, submenus and more than one menu.** A badge at the right end of an item carries a count or a dot in the tone of its meaning; a parent opens its
  children (one level, the branch with the current page is open); an app with distinct areas such as an *Administration* has one menu per area and a **switcher under the logo**.
- **The content always has a breadcrumb above it.** A long page has an index beside it ("On this page") so that one click jumps to a section.
- **Every list and every gallery has a toolbar above it.** *Search*, *Filter* and *Sort* are three buttons, and each opens a **dialog**.
- **Buttons are always under the content** (create, edit, settings), from positive to negative: *Save*, *Back*, a space, *Delete*. The control enforces the order.
- **Confirmations, alerts and inputs are always dialogs**, never `window.confirm`. **On a phone every dialog is full screen with the X at the top right.**
- CRUD is a list plus a separate page for create and edit; list actions sit below the table, left-aligned; delete is spaced apart; pagination directly under the table;
  dependent fields appear only when needed; scrollbars show only while scrolling; every page is checked on a phone (390 px).

Each app has **one accent colour** (`--mat-app-accent`); hover, tints, links, the focus ring and the dark mode are derived from it
and checked for WCAG AA, for every app, in light and dark.

## Screenshots

| The sample app, dark | The toolbar opens a dialog | The builder checks an accent |
|---|---|---|
| ![A list with its toolbar in dark mode](docs/images/sample-list-dark.png) | ![The filter dialog](docs/images/toolbar-dialog.png) | ![The builder](docs/images/builder.png) |

| Which apps look alike? | On a phone: the menu | On a phone: a full-screen dialog |
|---|---|---|
| ![The palette](docs/images/palette.png) | ![The drawer with the logo, the user, the theme button and the version](docs/images/phone-menu.png) | ![The filter dialog on a phone](docs/images/phone-dialog.png) |

| A period with flexible dates | A colour with its contrast | On a phone: a picker is a full-screen sheet |
|---|---|---|
| ![The period picker](docs/images/picker-period.png) | ![The colour picker](docs/images/picker-colour.png) | ![The period picker on a phone](docs/images/phone-picker.png) |

| The grid of twelve columns |
|---|
| ![The layout page with the grid of twelve columns](docs/images/layout-grid.png) |

## What is on the site

| Section | What you find |
|---|---|
| **Identity** | The apps of the family and their accent, a contrast report per app, a **palette check** (which accents are too close to tell apart, with proposals) and an **audit**: how close each app's code is to the rules today, from a static scan of its repository. |
| **Rules** | The rulebook, searchable, with a page per rule. |
| **Foundations** | Colour (light and dark side by side), type, spacing and radii, motion and layers, the icon sprite, and every design token, read from the CSS. |
| **Shell** | Anatomy of the frame around every page (switcher, items with badges and submenus, foot), a live sample with an administration behind the switcher, the HTML skeleton. |
| **Components** | Every control on **one page** with an index beside it that marks the section in view (and jumps there on one click): buttons, button groups, dropdowns, **forms** (input types and sizes, selects, checks and radios, range, input groups, search/password/counter/files, floating labels, layouts, validation with a summary), **pickers** (select with pictures, descriptions and tags, date and period, time and time range, colour), toolbar, table (variants, sortable, selectable rows), pagination, cards, list group, accordion, avatars, stats and timeline, menu items with badges and submenus, the menu switcher, dialogs (also from the side), popover, alerts, toasts, spinners … each live, with the markup to copy and the rules it follows. |
| **Layout** | Breakpoints and containers, the grid of twelve columns (spans, responsive classes, offsets, order, nesting), stack, row and auto grid. |
| **Utilities** | One class, one property: spacing, display and visibility by width, flex, text, colour and tints, borders, radius, shadow, sizing, position and overflow. |
| **Patterns** | The page types (list, gallery, edit, settings, dashboard, sign-in) in real frames, on a desktop and on a phone, in the accent of any app. |
| **Builder** | Try an accent: what the toolkit derives from it, contrast in both modes, the nearest app, the real screens in that colour, the snippet. |
| **Download** | MatUi as an archive or file by file, how to use it, and the design system as JSON (`/api/rules`, `/api/tokens` …). |

## Run it

```bash
docker compose up -d          # http://localhost:8060
```

or, without Compose:

```bash
docker run -d --name matdesign --restart unless-stopped -p 8060:8060 \
  --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges:true \
  ghcr.io/real-ttx/matdesign:latest
```

The site keeps no state: no volume, no database, no secrets, no Docker socket. The image runs as an unprivileged user with a read-only file system.

**Development:** `./scripts/redeploy.ps1` rebuilds the image from the source and redeploys the dev stack (`-Port` changes the host port).
Without Docker: `dotnet run --project src/MatDesign.Web --urls http://localhost:8060`. Tests: `dotnet test` (see below).

## Use the toolkit in an app

```html
<html lang="en" data-app="yourapp">
<head>
  <script src="/matui.boot.js"></script>               <!-- applies the saved theme before the first paint -->
  <link rel="stylesheet" href="/matui.css" />
</head>
<body>
  <!-- the contents of matui-icons.svg, inline, once -->
  ...
  <script src="/matui.js"></script>
</body>
</html>
```

```css
/* the colour of the app, nothing else */
[data-app="yourapp"] { --mat-app-accent: #2f6feb; }
```

**Copy the files, do not link them.** An app carries MatUi itself and never loads it from another site (rule `CODE-03`): it has to work offline and as a PWA.
The **Download** page has the archive; [docs/ADOPTING.md](docs/ADOPTING.md) is the checklist for moving an existing app over.

## How it is built

```
src/MatUi            Razor class library: the toolkit (CSS in ten parts, matui.js, the icon sprite). No build step.
src/MatDesign.Core   Colour maths (OKLab, contrast), the app registry, the rulebook, the CSS token parser. No web types.
src/MatDesign.Web    ASP.NET Core 10 Razor Pages: the site. It uses MatUi for itself (the same layout as the sample apps).
tests/MatDesign.Tests  xUnit: colours, the rulebook, the CSS and the script against the rules, the catalogues, the whole site in memory.
tests/e2e            The behaviour in a real browser against a running site (Playwright).
tools/               icons.js (builds the sprite), audit.js (scans the other repositories).
docs/                ARCHITECTURE.md, ADOPTING.md
```

The CSS is the source of truth for the tokens: the site reads `matui.tokens.css` and shows it, so the documentation cannot go out of date.
Every example in the component catalogue is rendered live and shown as source from the same string, and a test parses every one of them.
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) records the decisions.

## Tests

`dotnet test` runs 200+ tests without Docker or a browser (and `tests/e2e` the browser checks, see Status). They check, among other things:

- the colour maths against what CSS `color-mix(in oklab, …)` gives, and WCAG AA for **every app, in light and dark**
- the two places that write the dark set are identical; no literal colour outside the tokens; no web font; every class and property is `mat-`-prefixed
- the registry equals `matui.apps.css`; the button order and the full-screen phone dialog are in the CSS; `matui.js` opens no browser dialog and every documented
  `data-` attribute is handled
- every page answers, has exactly one `h1`, the menu, the logo, the breadcrumb, the theme button, the user and the version, and no inline style; every list page has
  its toolbar above the table and its three dialogs; no link leads nowhere
- every example in the catalogue uses classes the toolkit ships, icons the sprite has, labelled table cells, typed buttons and unique ids

## Versions

`main` is the release, `dev` is development. The version is `<major>.<minor>.<build>-<date>` (release), `nightly-<build>-<date>` (dev) or `local-<date>` (a local build);
see `Directory.Build.props` and `.github/workflows/build.yml`. The version of the toolkit is the first line of `matui.css`.

## Status

- The **audit** is a static scan: it finds what is written in the files of an app (a sidebar, a breadcrumb, browser dialogs, literal colours …) and cannot tell how a page
  looks. It is a snapshot (`src/MatDesign.Web/Data/audit.json`); run `node tools/audit.js` to refresh it.
- The **accent colours** in the registry are the ones found in the apps, darkened where white text would not reach AA. Where two apps look alike the palette check
  makes *proposals*; changing an app's colour is a decision for a person.
- The site is in **English**; the apps keep the language of their own interface.
- The behaviour of the script is checked twice: the unit tests read it, and `tests/e2e` drives a real browser against a running site (the drawer, full-screen dialogs, the
  button order, the theme button, dependent fields, the keyboard). It runs in CI; locally: `cd tests/e2e && npm install && node run.js http://localhost:8060`.
