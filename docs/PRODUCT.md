# MatDesign as a product

Decided 2026-10-14 by the owner: MatDesign is no longer only the design system of the Mat apps. It becomes **software in which any company builds its own
branding, its own CI/CD for it and its own toolkit**. Mat is the first company that uses it, and the reference and template for the others.

## The three pillars

| Pillar | What a company does | State |
|---|---|---|
| **Branding** | Names itself, chooses accent, shape, type, density and logo, and gets the checks (AA contrast, touch targets) that keep a brand from breaking its own UI | **stages 10 and 12 done** (a brand item, a token editor with limits and AA checks, the properties of single controls, overrides per app and per colour scheme); logo, favicon and fonts: **stage 13 done** (files in the volume, checked by content, served, released) |
| **Toolkit** | Takes the controls (CSS, one script, icon sprite) with its brand applied, its rules and its documented components | **the brand reaches them** (stages 10 to 12): tokens, component properties, per app and per scheme |
| **CI/CD** | Versions, changelog, checks and a release pipeline that publishes the files and tells the apps | **done** (stages 7 and 8 of the Studio) |

## Decisions

- **One instance per company.** Each company runs its own container with its own volume and its own administrator (as today: one service, one volume, no secret in
  the compose file). The data model is built so that everything of a company lies under one place, so that a service for many companies stays possible later. It is not
  built now.
- **The same repository is the product.** Nothing moves out. Mat becomes the first tenant: its apps, accents and rules are the sample data and the template, no longer
  the only thing the program knows.
- **The demo data are the current data.** The Mat apps, accents, rules and component documentation as they are now become the demo template that a new company can
  start from, look at and change. Nothing of it is thrown away or rewritten for the product; a new company's own data replaces it, or is built on top of it.
- **The Studio edits data, never free CSS.** A brand is a set of values for tokens that the toolkit documents, each with a type and limits, checked on save. The files of
  the toolkit stay reviewed and tested in the repository (docs/STUDIO.md, "What it does not do").
- **The mechanics are shared, the look is data.** Controls, scripts and tests are the same for everybody; what differs is the tokens, the assets and the names.
- **Nothing is loaded from another site** (`CODE-03`): fonts and logos of a brand are files in its volume and in its releases.

## Stages

Each stage ends with the build clean, the tests and browser checks green, the container rebuilt and a commit, like the Studio stages.

| # | Stage | Contents |
|---|---|---|
| 10 | **Brand and tokens** (done) | A `brand` item in the Studio (company name, short name, tagline) and a **token editor**: every token of `matui.tokens.css` can be overridden globally, light and dark, with a type per token (colour, length, number, font stack, duration), limits and live checks (AA for every pairing the toolkit holds, touch targets of 44 px, body text not below 14 px, line height 1.2 to 2, radii 0 to 24 px). The values are written as an overlay at the end of the apps part of the bundle (after the tokens, so it wins; light on `:root`, dark written twice and identical), ten parts as before, into releases, the archive and the changelog. History, restore and checks work like for apps and rules. A live preview with the sample app. |
| 11 | **Component properties** | The toolkit gets documented `--mat-<component>-*` properties (radius, padding, border width, height, ...) with a fallback to the global token, so that nothing changes for an app that does not set them. Part of the toolkit, no Studio change: it gives the editor something to turn. **Done:** 68 properties in 16 component groups, declared by `@prop` lines in the CSS and read by `ComponentProperties` (docs/ARCHITECTURE.md, "Component properties"; `SPACE-06`). |
| 12 | **Token editor per component and per app** (done) | The editor lists the properties of a component next to its examples with a preview beside each control (`/Studio/Brand/Components`); overrides are global (the brand), per app (`[data-app]`, on the app's page: shape, spacing, type, motion and every property) and per colour scheme (what the scheme itself sets, on the brand page). Written after the brand in the order toolkit, brand, app, scheme; a height under 44 px only for a fine pointer, so touch targets never shrink (`MOBILE-04`); same checks, history, restore and release as the brand. Details: docs/STUDIO.md, "Components, apps and schemes". |
| 13 | **Assets** (done) | Logo, word mark, favicon and app icon (uploaded, judged by their content: type, size, an SVG against an allowlist, alt text required) and the fonts of a brand (WOFF2 or WOFF, at most 12 faces in 3 families) as files in the volume named by their SHA-256, never overwritten or deleted, so that history and restore work. Served under `/ui/brand/` with immutable cache headers, written into every release under `brand/` with their hashes and into the archive; `@font-face` generated with `font-display: swap`; the shell uses the logo and the favicon. The rule is `IDENT-08`. |
| 14 | **Demo data and templates** | The setup assistant asks for the company and starts from a template: **Demo** (the current Mat data, unchanged), or a blank brand; the site's own name, logo and menu come from the brand and no longer say MatDesign. |
| 15 | **White label build** | A release can rename the toolkit for the company (`mat-` to its prefix, `MatUi` to its name) in the Build stage, with the tests of the toolkit run on the result. |
| 16 | **People and roles** | More than one user per instance with roles (administrator, editor, viewer) and the activity list that names them. |

The order is a dependency: stage 11 gives the editor of stage 12 its handles, stage 13 completes a brand, stage 14 removes the last thing that says Mat.

## What is in the code today and has to become data

`AppRegistry` (the apps), `RuleBook` (the rules) and `ComponentCatalog` (the documentation) are built-in data with the Studio's items on top (`DesignData`). That
mechanism already is the model for tenants: the built-in layer is the demo data, and stage 14 lets a company choose it or start blank. Until then the data stays as it is.

The overlay for a brand follows the one of the apps (`ToolkitFiles.AppsOverlay`): what the Studio saved is written after the built-in CSS, so it wins, and the bundle, its
hash and the archive follow `DesignData.Version`.
