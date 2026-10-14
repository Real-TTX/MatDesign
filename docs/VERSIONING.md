# Versions of the toolkit

MatUi, the toolkit that the apps copy, has a version of its own. It tells an app **whether to update, what the update takes with it and whether it has to change something
of its own**. This page says what the number promises, where it is written, how a version is made and how an app updates.
The changes of every version are in [CHANGELOG.md](../CHANGELOG.md), on the site at **/Changelog**, as a file at `/ui/CHANGELOG.md` and as data at `/api/changelog`.

## Three numbers, one that matters to an app

| Number | What it is | Looks like | Who needs it |
|---|---|---|---|
| **MatUi version** | the toolkit: `matui.css`, `matui.js`, `matui.boot.js`, `matui-icons.svg` | `0.1.0` | **an app**: this is the one to compare |
| Build of the site | the container image that served or made the files | `0.1.42-20261010`, `nightly-42-20261010`, `local-20261010` | whoever runs MatDesign |
| Release of the Studio | a frozen copy of the toolkit with the design data (apps, rules, components) | `0.1.0` | an app that wants files that never change (`/releases/<version>/`) |

A Studio release is not lower than the MatUi version of the build that made it, and its `matui.js` and `matui.css` name the version of the release.
The first line of `matui.css` names the version and then the build: `/* MatUi 0.1.0 (a075e2ef54) - build local-20261013 - bundled from ... */`.

## What the number promises

Semantic versioning: `MAJOR.MINOR.PATCH`.

- **Patch** (`0.1.0` to `0.1.1`): a fix. Nothing to do but copy the files.
- **Minor** (`0.1.x` to `0.2.0`): something new, or a change that nobody has to react to. Nothing to do but copy the files.
- **Major** (`1.x` to `2.0.0`): a change that needs work in the app. The changelog says what (**Breaking**) and how (**Migration**).
- **Before 1.0** a **minor** may break something too. It says so in the same way: a version with a **Breaking** section always has a **Migration** section, and the tools refuse to make one without.

### What an app may rely on (the contract)

A change to any of these, other than adding to them, is **breaking**:

- the **classes** `mat-*` (blocks, elements, modifiers) and the state classes `is-*` that the catalogue documents, and the markup structure of an example in the catalogue;
- the **attributes** `data-*` and their values, and the **events** `mat:*` with their `detail`;
- the **custom properties** `--mat-*` (the tokens) and `data-app`, `data-theme`;
- the script API `window.MatUi` (what the header of `matui.js` and the catalogue name);
- the **names of the icons** in the sprite;
- the **behaviour** that a rule or the catalogue states (keys, what closes a panel, the posted formats of the pickers).

Not part of the contract, and free to change in any version: the `doc-*` classes of the site, the names of functions inside `matui.js`, the markup that the script builds
inside a control (the chips of the tag input, the bar of the carousel) unless the catalogue documents it, and the exact look: colours of the tokens, spacing and
radii may be refined in a minor version (a change of the look that the rulebook did not ask for is still written down under **Changed**).

### Deprecating

A class, attribute or token that is going away is first marked under **Deprecated** in the changelog, keeps working for at least one minor version, and is removed
under **Removed** and **Breaking** in the next major (before 1.0: the next minor).

## Where the number is written

One source, `MatUiVersion` in `Directory.Build.props`, and four places that carry it. `node tools/bump.js` writes all of them, and a unit test holds them to each other:

| Where | How |
|---|---|
| `Directory.Build.props` | `<MatUiVersion>0.1.0</MatUiVersion>`; the build reads it into the assembly (`AppVersion.MatUi`) |
| `CHANGELOG.md` | the newest released entry is `## [0.1.0] - date`; above it `## [Unreleased]` |
| `matui.js` | the header (`Version 0.1.0 of the toolkit MatUi`) and `window.MatUi.version` |
| `matui.boot.js` | `/* MatUi boot 0.1.0: ... */` |
| served and copied | the first line of `matui.css`; `/ui/manifest.json`, `matui.manifest.json` in the archive; `/api/version` (`matui`); the Download and Changelog pages |

A white-label release (docs/STUDIO.md, "White label") carries the same number in the same places under the company's names: `window.AcmeUi.version`, the first line `/* AcmeUi 0.1.0 (hash) ... */` of `acmeui.css`, `acmeui.manifest.json` with
`"name": "AcmeUi"`. The version does not change because of the rename, and neither does the contract: the changelog of the archive is the toolkit's, with its names renamed. The hash in the first line is the hash of the renamed files.

The version in the source is **the last release**. What is not released yet is under **Unreleased**; the nightly and local builds of the site carry it.

## Making a version (the maintainers)

1. **In every change that an app can see** (anything under `src/MatUi`: CSS, script, sprite, a documented markup or attribute), write a line under `## [Unreleased]` in `CHANGELOG.md`, in the same commit.
   Sections: **Added**, **Changed**, **Deprecated**, **Removed**, **Fixed**, **Security**, and the two of ours: **Breaking** (what an app has to change) and **Migration** (the steps, numbered by order).
   The build of a pull request fails when `src/MatUi` changed and `CHANGELOG.md` did not.
2. When it is time: `node tools/bump.js minor` (or `patch`, `major`, or a number such as `0.4.0`; `--dry` shows what it would do). It turns Unreleased into the new version with today's date,
   leaves an empty Unreleased above it and writes the number into the sources. It refuses an empty Unreleased, a Breaking without Migration, a patch that carries a break
   and a number that is not higher.
3. `dotnet test` (it holds the numbers to each other and the changelog to its format), commit `MatUi 0.2.0`, tag `matui-v0.2.0`, push.
4. In the Studio, **Releases → New release** suggests this version; the release freezes the files under `/releases/0.2.0/` and tells the apps that use MatUi that there is a newer one.

## Updating an app

An app **copies** the toolkit (rule `CODE-03`) and never loads it from another site, so an update is a copy, done on purpose.

1. **Look.** `node tools/matui-check.js <folder of the app>` finds the copy, says which version it is and whether all files are the same version, whether a file was changed by hand
   (against `matui.manifest.json` that came with the archive) and what an update takes with it. Exit code `0` current, `1` an update is available, `2` the update needs changes in the app or the copy is mixed or changed.
   It reads this repository's changelog, or another with `--changelog http://host/ui/CHANGELOG.md`.
2. **Read.** The page **/Changelog** (with `?since=0.1.0` for what an app on that version takes with an update) or `/api/changelog?since=0.1.0`. Read every version from your own to the newest, the oldest first.
3. **Do the steps** of every **Migration** you meet, in the app's own code and markup.
4. **Copy all the files** of the new version, never a few of them: the files of one version belong together. Take them from a Studio release (`/releases/<version>/matui-<version>.zip`: files that never change)
   or from `/ui/matui.zip` (the toolkit as it is now). A change that you made by hand in a copied file is lost: the check names such files first; move what is needed into the app's own CSS
   (the colour of the app is one line, `[data-app="yourapp"] { --mat-app-accent: #2f6feb; }`).
5. **Write it down.** Studio → Versions → the app: the version it carries now, so that the overview can say which apps are behind.
6. **Run the app's tests and look** at its pages, at 390 px too.

An app that references the project `MatUi` (a Razor class library) updates by taking the commit or tag `matui-v<version>` of this repository; the files carry the same version.

## Why not a package

The toolkit is a handful of plain files with no build step, and the apps are in several languages. A copy of the files, a number that is in the first line of each, a manifest with their hashes and a
changelog is all an app needs to know where it stands; a package manager would be one more thing in every repository.
