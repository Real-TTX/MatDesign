# Changelog of MatUi

The toolkit that the Mat apps copy: the CSS (`matui.css`), the script (`matui.js`, `matui.boot.js`) and the icon sprite (`matui-icons.svg`).
This file says what changed in which version, so that an app that carries a copy knows what it takes with an update and what it has to do.
How the numbers work, and how an app updates: [docs/VERSIONING.md](docs/VERSIONING.md).

The format is [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) with two sections of our own:

- **Breaking**: what an app has to change in its own code or markup when it takes the version (a class that was renamed or removed, a changed `data-` attribute, a changed behaviour that apps rely on).
- **Migration**: the steps for it. A version with a Breaking section needs a Migration section.

A change that an app can see is written down under **Unreleased** in the pull request that makes it. `node tools/bump.js <patch|minor|major>` turns Unreleased into the next version.

## [Unreleased]

### Added

- The toolkit has a version of its own (`MatUi.version`, the first line of `matui.css` and `matui.js`, `/ui/manifest.json`, `/api/version`), this changelog (also at `/ui/CHANGELOG.md`, `/api/changelog` and on the Changelog page) and a guide for updating an app (docs/VERSIONING.md), with `tools/matui-check.js` that compares an app's copy with the toolkit.
- Menu switcher (`SHELL-15`): it can sit under the logo, at the foot of the menu directly above the user, or at both places (`mat-nav__switch--foot`, `data-menu-place="top"`), and menus can be grouped below a parent in its popup (`data-nav-group`, `data-nav-parent`, `mat-menu__parent`, `mat-menu__sub`).
- Tag input: `data-picker="tags"`, a field of chips with suggestions (`FORM-15`).
- Carousel: `mat-carousel` with `data-carousel`, `data-loop` and `data-autoplay` (`LIST-13`).
- Groups in a table: `tbody.mat-group` with a heading row that opens and closes the group (`data-groups`, `data-group`, `data-group-toggle`, `data-group-fixed`, `data-groups-set`), the rows hanging on a line and the add row (`mat-group__add`) as the last row of the group; the choice is remembered (`LIST-16`).
- Number stepper: `mat-stepper` with `data-stepper` (`FORM-16`).
- Slider and range slider with two thumbs: `mat-slider` with `data-slider` (`FORM-17`).
- Code input: `mat-otp` with `data-otp`, for one-time codes and PINs (`FORM-19`).
- Dropdown with a list of checkboxes: `data-picker="select" data-multiple` with `data-select-all` (`FORM-15`).

### Changed

- `MatUi.version` is the version of the toolkit (a string such as `0.1.0`); it used to be the fixed string `1`.
- The first line of `matui.css` is `/* MatUi <version> (<hash>) - build <build> - ... */`; the build that it used to name first is now second.
- The panels of the pickers only fade in: they no longer start lower and smaller and grow into place (`mat-picker-in` replaces `mat-menu-in` for them).

### Fixed

- The index of a long page ("On this page") can be scrolled by the user again: it followed the page on every scroll event and pulled itself back.

## [0.1.0]

The toolkit as the apps copy it before it had a version: the shell with the menu (badges, submenus, switcher), the header bar and the footer; buttons, forms and the pickers (select with pictures, date and period, time and time range, colour); lists with toolbar, table and pager; cards, tabs, dialogs, alerts, toasts and the other content components; the layout system and the utilities; the icon sprite and the optional icon library; the tokens for light and dark and the accent of every app.

### Added

- The baseline of the changelog. Every later version says what changed from this one.
