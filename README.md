<div align="center">

<img src="src/MatDesign.Web/wwwroot/favicon.svg" width="96" alt="MatDesign" />

# MatDesign

**One look for every Mat app – the rules, the toolkit and the studio that maintains them.**

A rulebook with stable IDs, every control with live examples and its code beside it, a layout system,
and **MatUi**: the CSS, one script and an icon sprite that each app copies.
One container, no cloud, nothing loaded from another site.

</div>

![The components page: every example with its code beside it](docs/images/components-code.png)

---

## What this is about

Every Mat app has its own colour and the same design. Without a reference the design drifts: one app puts its
menu on top, another asks with a browser dialog, a third forgets the phone. MatDesign is that reference – as a
website you can open, as a rulebook you can cite (`SHELL-03`, `LIST-03`, `COLOR-08` …), and as the toolkit the
apps are built from. Where the rules can be checked by a test, a test checks them.

On top sits a **Studio**: apps, rules and components are created and changed there, every change is kept as a
revision, and a change becomes a numbered, immutable **release** of the design system that the apps copy from.

## At a glance

**The rulebook**
- **110 rules in 14 areas** – shell, lists, forms, dialogs, colour, mobile, code …
- Each rule has a **stable ID**, a level (*Must*, *Should*, *May*), a source and, where a test enforces it, a check
- Searchable, a page per rule, and as JSON at `/api/rules`

**The toolkit – MatUi**
- **Four files you copy**: `matui.css` (ten parts), `matui.js`, `matui.boot.js` and the icon sprite –
  no framework, no build step, no CDN
- **One accent colour per app.** Hover, tints, links, the focus ring and the dark mode are derived from it
  and checked for **WCAG AA, for every app, in light and dark**
- **90 design tokens**, read from the CSS itself, so the documentation cannot go out of date
- A **flat look on purpose**: borders instead of shadows, no gradients, and what is selected is a tint behind
  the whole item, never a bar at one side
- An optional **icon library** in files of its own: **1,925 icons in 54 categories** – an icon can be in several –
  with *General* and **Home automation** (302 icons: lighting, climate, doors and windows, alarm, appliances,
  energy, sensors, rooms, garden, scenes) first. SVG, as one sprite, as single files or as an archive; MatUi works without it

**The documentation**
- **51 components with 118 live examples**, each with its **code beside it** – coloured, with *Copy*, the classes
  it uses and the rules it follows
- **Every group in a real page**: the page of a sample app next to the components, on a desktop or a phone,
  the component outlined
- **Layout** (twelve columns, breakpoints) and **utility classes**, the **page patterns**, the **foundations**
  (colour, type, space, motion, 106 icons)
- A **builder** that checks an accent colour, a **palette check** for the 22 apps and an **audit** of how close
  each app is to the rules

**The Studio**
- **Apps, rules and components** with live examples, held to the same checks as the built-in ones
- **History** for every item: each change is a revision, with a diff and a restore
- **Checks**: everything the design system holds itself to, on the data as it is now
- **Releases** through a pipeline – *Check → Version → Build → Publish → Notify* – with the toolkit files, the
  design data and a **styleguide** that opens straight from the release directory
- **Versions**: which release of MatUi each app carries, and which one is behind

## The rules in one minute

- **The menu is always on the left, the logo above it.** At the bottom: the user next to the **3-state theme
  button** (System / Light / Dark), and the version below them. The menu scrolls on its own.
- **The menu can have badges, submenus and more than one menu**, with a **switcher** under the logo, above the user at
  the foot, or both; the menus in its popup can be **grouped** below a parent. The header bar
  can carry **icons at its right end**, and a page can end with a **footer**.
- **The content always has a breadcrumb above it.** A long page has an index ("On this page") – on a small window
  it is an icon in the header bar that opens a dialog.
- **Every list and gallery has a toolbar above it.** *Filter* and *Sort* are buttons that open a **dialog**; *Search*
  is one too, or a field right in the bar with the dialog buttons at its right end.
- **Buttons are always under the content**, from positive to negative: *Save*, *Back*, a space, *Delete*.
  The control enforces the order.
- **Confirmations, alerts and inputs are always dialogs**, never `window.confirm`. **On a phone every dialog is
  full screen with the X at the top right.**
- CRUD is a list plus a separate page; pagination directly under the table; dependent fields only when needed;
  every page is checked on a phone (390 px).

## Screenshots

### The rulebook

![The rulebook with the search field in the toolbar](docs/images/rules.png)

Every rule with its level, source and how it is checked. The toolbar holds the search as a field and puts *Filter*
and *Sort* at its right end.

### Every example shows its code

![The dialogs in dark mode with their code beside them](docs/images/components-dark.png)

The code is never behind a click. From 1600 px of window width it sits beside the example, below that under it;
tables, bars and grids always get the full width. The classes and `data-` attributes under the title are read from
the markup of the examples.

### A group in a real page

![The list page of the sample app with the table outlined](docs/images/components-in-page.png)

Each group has a panel with a real page of the sample app. It loads when opened, switches between desktop and phone,
and outlines the component you pick.

### The icon library

![The icon library: Home automation, 302 icons, the categories beside the gallery](docs/images/icon-library.png)

| One icon: its categories, markup and file | On a phone | The categories on a phone |
|---|---|---|
| ![The dialog of an icon](docs/images/icon-library-dialog.png) | ![The gallery on a phone](docs/images/phone-icon-library.png) | ![The categories as a dialog](docs/images/phone-icon-categories.png) |

Search by name or by what an icon is for (`heating` finds the radiator), narrow by category, click an icon for its
markup and its `.svg`. The files – sprite, catalogue, each icon, an archive – are under `/ui/iconlib/`; the icons are
Lucide's (ISC licence) plus 51 drawn for MatDesign where a smart home needs one that Lucide has not.

### Pickers, the builder and the grid

| A period with flexible dates | A colour with its contrast | The grid of twelve columns |
|---|---|---|
| ![The period picker](docs/images/picker-period.png) | ![The colour picker](docs/images/picker-colour.png) | ![The layout page with the grid](docs/images/layout-grid.png) |

| The builder checks an accent | Which apps look alike? | The toolbar opens a dialog |
|---|---|---|
| ![The builder](docs/images/builder.png) | ![The palette](docs/images/palette.png) | ![The filter dialog](docs/images/toolbar-dialog.png) |

### The same toolkit in an app

![The sample app in dark mode: a list with its toolbar](docs/images/sample-list-dark.png)

The sample app is built from nothing but MatUi, in the accent of one of the apps.

### On a phone

| The menu | The page index | A full-screen dialog |
|---|---|---|
| ![The drawer with the logo, the switcher, the user and the version](docs/images/phone-menu.png) | ![The index of a long page as a dialog](docs/images/phone-index.png) | ![The filter dialog on a phone](docs/images/phone-dialog.png) |

| A list | A picker is a full-screen sheet | The Studio |
|---|---|---|
| ![A list on a phone](docs/images/phone-list.png) | ![The period picker on a phone](docs/images/phone-picker.png) | ![The versions in the Studio on a phone](docs/images/studio-phone.png) |

### The Studio

![The overview of the Studio](docs/images/studio-overview.png)

| An app and its colour | A component with its examples | What changed between two revisions |
|---|---|---|
| ![Editing an app](docs/images/studio-app.png) | ![Editing a component with the live preview](docs/images/studio-component.png) | ![The history of an app with a diff](docs/images/studio-history.png) |

| Every check, run on the data | The releases and the last runs | One run of the pipeline |
|---|---|---|
| ![The checks](docs/images/studio-checks.png) | ![The releases](docs/images/studio-releases.png) | ![A run with its five stages](docs/images/studio-run.png) |

| Which app is behind | The styleguide every release carries |
|---|---|
| ![The versions of the apps](docs/images/studio-versions.png) | ![The styleguide of a release, opened from the release directory](docs/images/studio-styleguide.png) |

## What is on the site

![The overview of MatDesign](docs/images/overview.png)

| Section | What you find |
|---|---|
| **Identity** | The apps of the family and their accent, a contrast report per app, the palette check and the audit. |
| **Rules** | The rulebook, searchable, with a page per rule. |
| **Foundations** | Colour (light and dark side by side), type, spacing, motion, the icon sprite, the optional **icon library**, and every design token. |
| **Shell** | The frame around every page: switcher, items with badges and submenus, foot, header bar, footer – with a live sample and the HTML skeleton. |
| **Components** | Buttons, forms, pickers, toolbar, table, pagination, cards, tabs, dialogs, alerts and more – live, with code beside, classes, rules and a real page per group. |
| **Layout** | Breakpoints and containers, the grid of twelve columns, stack, row and auto grid. |
| **Utilities** | One class, one property: spacing, display by width, flex, text, colour, borders, sizing, position. |
| **Patterns** | The page types (list, gallery, edit, settings, dashboard, sign-in) in real frames, on a desktop and a phone. |
| **Builder** | Try an accent: what the toolkit derives from it, contrast in both modes, the real screens in that colour. |
| **Download** | MatUi as an archive or file by file, and the design system as JSON (`/api/rules`, `/api/tokens` …). |
| **Studio** | Behind a sign-in; the first visit creates the administrator in a setup assistant – see [docs/STUDIO.md](docs/STUDIO.md). |

## Quick start

Ready-made images are published to the GitHub Container Registry by the GitHub action:

| Tag | Built from | Use it for |
|---|---|---|
| `ghcr.io/real-ttx/matdesign:latest` | `main` | releases |
| `ghcr.io/real-ttx/matdesign:nightly` | `dev` | the newest features |

### 1. Just run it

Copy this into `docker-compose.yml` and start it – nothing else needed:

```yaml
services:
  matdesign:
    image: ghcr.io/real-ttx/matdesign:latest
    container_name: matdesign
    restart: unless-stopped
    ports:
      - "8060:8060"
    volumes:
      - matdesign-data:/data

volumes:
  matdesign-data:
```

```bash
docker compose up -d
```

Open **http://localhost:8060** (health check: `/health`). No variables, no `.env` file. The image runs as an unprivileged user,
needs no Docker socket and holds no secrets.

Without Compose:

```bash
docker run -d --name matdesign --restart unless-stopped -p 8060:8060 \
  -v matdesign-data:/data ghcr.io/real-ttx/matdesign:latest
```

### 2. The first visit: the setup assistant

The **Studio** is where apps, rules and components are maintained and a change becomes a release. Open
**http://localhost:8060/Studio** right after the start: the **setup assistant** asks for the first administrator – a name
and a password (at least 10 characters, typed twice) – signs you in and leads into the Studio. From then on the Studio asks
for that name and password (repeated failures lock the client out for a while).

![The setup assistant: a name and a password typed twice](docs/images/studio-setup.png)

Whoever opens the page first becomes the administrator, so do it right after the start. The password is kept as a salted hash
in the `matdesign-data` volume, which also keeps everything the Studio saves; an update is just
`docker compose pull && docker compose up -d`. If you lose the password, delete `admin.json` in the volume and restart
(`docker exec matdesign rm /data/admin.json && docker restart matdesign`): the assistant shows again.
[docs/STUDIO.md](docs/STUDIO.md) describes what the Studio does and what it deliberately does not.

### 3. Use the toolkit in an app

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

**Copy the files, do not link them.** An app carries MatUi itself and never loads it from another site (rule `CODE-03`):
it has to work offline and as a PWA. The **Download** page has the archive, a release of the Studio has the same files
under `/releases/<version>/`, and [docs/ADOPTING.md](docs/ADOPTING.md) is the checklist for moving an existing app over.

### 4. From source

```bash
./scripts/redeploy.ps1                                    # rebuild the image and redeploy the dev stack
docker compose -f docker-compose.dev.yml up -d --build    # the same without the script
```

The development stack runs on **http://localhost:8060**; a fresh volume starts with the setup assistant like the release does
(the browser checks create the administrator `admin` with the password `studio-dev` when they find none).
Without Docker (the Studio needs a data directory):

```bash
MATDESIGN_DATA=./data ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MatDesign.Web --urls http://localhost:8060
```

### Settings that matter

None of them is needed to run it – the image has the right defaults.

| Variable | Default | Meaning |
|---|---|---|
| `MATDESIGN_DATA` | `/data` (in the image) | Data directory of the Studio; it must be writable. Without it the Studio is off and the site is read-only |
| `MATDESIGN_ADMIN_PASSWORD` | – | Optional, for automation (CI, a throwaway stack): the password of the administrator `admin`, which skips the setup assistant. It wins over the account in the volume |
| `ASPNETCORE_URLS` | `http://+:8060` | Address inside the container |

### The `/data` volume

```
/data
├─ admin.json     the administrator: a name and a salted hash of the password (written by the setup assistant)
├─ studio/        what the Studio saves: one append-only log of revisions per item
│                 (apps, rules, components, releases, runs, versions in use)
├─ releases/      one directory per release: the toolkit, design.json, the styleguide, manifest.json
└─ keys/          DataProtection keys (the Studio session survives a restart)
```

A release never changes and the site offers no way to delete one – apps may depend on it, so the volume is the only
place that could.

## Status

| Milestone | Content | Status |
|---|---|---|
| Toolkit | MatUi (ten CSS parts, one script, the icon sprite), colour maths, rulebook, app registry | ✅ |
| Site | Rulebook, foundations, shell, identity, builder, download – tests, container, CI | ✅ |
| Components | Menu badges, submenus and switcher, forms, **pickers** (select, date and period, time, colour, **tags**), content components (with a **carousel**), a **schedule** (agenda, day, week, month, year; calendar weeks, paging by swipe and keys, lazy loading), **layout system**, utilities | ✅ |
| Flat look | No gradients, glow or blur; selected is a tint (`COLOR-08`); toolbar with search field | ✅ |
| Studio 1–2 | Data store, sign-in, **apps** | ✅ |
| Studio 3–4 | **Rules** and **components** with live examples | ✅ |
| Studio 5–6 | **History** (diff, restore) and **checks** | ✅ |
| Studio 7–8 | **Releases** through a pipeline, served for the apps; **versions** per app | ✅ |
| Setup | The **setup assistant** creates the first administrator; the release compose has no variables and no `.env` file | ✅ |
| Icon library | 1,925 icons in 54 categories, **Home automation** with its areas, search, single files, archive, in a release | ✅ |
| Documentation | **Code beside every example**, a real page per group, a styleguide in every release | ✅ |
| Releases | The real sample pages in a release (today: code and live examples only) | open |
| Components | CSS variables per component, accessibility notes, a search across components | open |

## Development

Building and testing happens through the container:

```bash
./scripts/redeploy.ps1
```

The tests need no container and no browser; they also start the whole site in memory and crawl it:

```bash
dotnet build MatDesign.slnx          # expected: 0 warnings
dotnet test MatDesign.slnx
```

The behaviour of the script is checked in a real browser (Playwright against the running site, 140+ checks):

```bash
cd tests/e2e && npm install && node run.js http://localhost:8060
```

Each run of the browser checks makes one release (a release never changes), so the dev volume grows;
`docker compose -f docker-compose.dev.yml down -v` followed by `up -d --build` starts it clean.

Other tools: `node tools/icons.js` rebuilds the icon sprite from `tools/icons.base.json` and `node tools/iconlib.js` the icon library from `tools/iconlib/`
(CI fails when either is stale),
`node tools/audit.js` rescans the other repositories for the audit snapshot.

**What the tests hold the site to**, among other things:

- the colour maths against what CSS `color-mix(in oklab, …)` gives, and WCAG AA for every app, in light and dark
- the dark set is written twice and the two copies are identical; no literal colour outside the tokens; no web
  font; every class and property is `mat-`-prefixed; no border on one side of a rounded item
- every page answers, has exactly one `h1`, the menu, the breadcrumb, the theme button, the user and the version, and no
  inline style; every list page has its toolbar above the table; no link leads nowhere
- every example in the catalogue uses classes the toolkit ships, icons the sprite has, labelled table cells, typed
  buttons and unique ids – the Studio holds an example it saves to the same code
- the Studio: the store of revisions, the sign-in and its lock-out, every validator, the checks, the pipeline and the
  hashes of a release, and every page of the Studio against the shell rules

## How it is built

```
src/MatUi              Razor class library: the toolkit (CSS in ten parts, matui.js, the icon sprite). No build step.
src/MatDesign.Core     Colour maths (OKLab, contrast), the app registry, the rulebook, the Studio store and validators. No web types.
src/MatDesign.Web      ASP.NET Core 10 Razor Pages: the site, drawn with MatUi itself. Studio/ and Pages/Studio/ are the Studio.
tests/MatDesign.Tests  xUnit: colours, the rulebook, the CSS and the script against the rules, the catalogues, the whole site in memory.
tests/e2e              The behaviour in a real browser against a running site (Playwright).
tools/                 icons.js (builds the sprite), iconlib.js (builds the icon library from tools/iconlib/), audit.js (scans the other repositories).
docs/                  ARCHITECTURE.md, ADOPTING.md, STUDIO.md
```

- **ASP.NET Core 10** (Razor Pages), plain CSS and plain JavaScript – no framework, no build step, no database
- The CSS is the source of truth for the tokens: the site reads `matui.tokens.css` and shows it
- Every example in the component catalogue is a raw string that is rendered live **and** shown as source, and a test
  parses every one of them
- The Studio keeps revisions as append-only JSON files on a volume; the site works without it

[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) records the decisions, [docs/ADOPTING.md](docs/ADOPTING.md) is the
checklist for an app, [docs/STUDIO.md](docs/STUDIO.md) describes the Studio.

The site is in **English**; the apps keep the language of their own interface. The audit is a static scan of the other
repositories (a snapshot in `src/MatDesign.Web/Data/audit.json`): it finds what is written in the files and cannot tell
how a page looks.

## Branches & versioning

| Branch | Purpose | Version |
|---|---|---|
| `main` | Release | `<major>.<minor>.<build>-<yyyyMMdd>` |
| `dev` | Development | `nightly-<build>-<yyyyMMdd>` |
| local | – | `local-<yyyyMMdd>` |

`Major`/`Minor` live in [`Directory.Build.props`](Directory.Build.props), the build number comes from the GitHub
action (`github.run_number`). Images are published to the GitHub Container Registry. The version of the toolkit is
the first line of `matui.css`.
