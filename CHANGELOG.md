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

- The toolkit has a version of its own (`MatUi.version`, the first line of `matui.css` and the header of `matui.js`, `/ui/manifest.json`, `/api/version`), this changelog (also at `/ui/CHANGELOG.md`, `/api/changelog` and on the Changelog page) and a guide for updating an app (docs/VERSIONING.md), with `tools/bump.js` that makes a version and `tools/matui-check.js` that compares an app's copy with the toolkit. The archive an app copies holds the changelog and `matui.manifest.json` with the SHA-256 of every file.
- Menu switcher (`SHELL-15`): it can sit under the logo, at the foot of the menu directly above the user, or at both places (`mat-nav__switch--foot`, `data-menu-place="top"`), and menus can be grouped below a parent in its popup (`data-nav-group`, `data-nav-parent`, `mat-menu__parent`, `mat-menu__sub`).
- Tag input: `data-picker="tags"`, a field of chips with suggestions (`FORM-15`).
- Dropdown with a list of checkboxes: `data-picker="select" data-multiple` with `data-select-all` (`FORM-15`).
- Appointment picker: `data-picker="slot"` shows the days that have a free time and then the times of the chosen day; what is taken is marked or hidden (`data-unavailable`), `data-min`, `data-max` and `data-min-notice` keep out the past, Next free appointment, and availability that is loaded per month (`mat:slots-needed`, `MatUi.picker.setSlots`). It posts `2026-10-14T09:30` (`FORM-18`).
- Calendar: `mat-calendar` with `data-calendar`, a calendar that is always shown, for one day, a period or several days, with minimum and maximum, disabled dates and weekdays, events with an agenda and week numbers (`FORM-18`).
- The date pickers have a jump to a month and a year, and `data-min` and `data-max` also take `today`, `tomorrow`, `+2d`, `+3m` and the like.
- Schedule: `mat-schedule` with `data-schedule`, the events one below another in the views Agenda, Day, Week, Month and Year, with calendar weeks, paging by buttons, keys and swipe, and events that are loaded for the visible period (`LIST-14`).
- Lists of rows: `mat-itemlist` and `mat-item` for contacts, messages, settings, files, tasks and the like, with sliding actions, reordering, a filter and select all (`LIST-15`).
- Carousel: `mat-carousel` with `data-carousel`, `data-loop` and `data-autoplay` (`LIST-13`).
- Number stepper: `mat-stepper` with `data-stepper` (`FORM-16`).
- Slider and range slider with two thumbs: `mat-slider` with `data-slider` (`FORM-17`).
- Code input: `mat-otp` with `data-otp`, for one-time codes and PINs (`FORM-19`).

### Changed

- `MatUi.version` is the version of the toolkit (a string such as `0.1.0`); it used to be the fixed string `1`.
- The first line of `matui.css` is `/* MatUi <version> (<hash>) - build <build> - ... */`; the build that it used to name first is now second.
- The panels of the pickers only fade in: they no longer start lower and smaller and grow into place (`mat-picker-in` replaces `mat-menu-in` for them).
- The date pickers and the calendar draw their months with the same code; nothing that an app writes has changed.

### Fixed

- The index of a long page ("On this page") can be scrolled by the user again: it followed the page on every scroll event and pulled itself back.
- The `code` inside a `.mat-code` block has no padding of its own: the padding of an inline code moved the first line of a block to the right.

## [0.1.0]

The toolkit as the apps copy it before it had a version: the shell with the menu (badges, submenus, switcher), the header bar and the footer; buttons, forms and the pickers (select with pictures, date and period, time and time range, colour); lists with toolbar, table and pager; cards, tabs, dialogs, alerts, toasts and the other content components; the layout system and the utilities; the icon sprite and the optional icon library; the tokens for light and dark and the accent of every app.

### Added

- The baseline of the changelog. Every later version says what changed from this one.
