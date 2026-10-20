# Review of the rulebook, 2026-10-20

The owner asked to go through the rules again, see whether something was forgotten, and keep a changelog. This page is the written result of the first review. The changelog is `/Rules/Changes` (docs/VERSIONING.md,
"The rulebook has a changelog too"). A later review is a new dated section below this one, not a new file.

## Method

- Read: the repository guide (the markdown file in the root that guides work in the repository), docs/ARCHITECTURE.md, ADOPTING.md, VERSIONING.md, PRODUCT.md, STUDIO.md, all 130 rules (`RuleBook.cs`), the validator, the rule pages, the changelog code and the notes of the 78 components and 6 patterns.
- Looked at what the code does, not only at what the documents say: the CSS (10 parts), `matui.js` (events, ARIA, texts, focus), the tests (what each rule's `Check` names), the running site (headers, pages) and git (when each rule first stood in the book).
- A rule was added only with a reason from the repo (the code or the docs already do or require it, or an accessibility gap), a level, and a `Check` that is true. Where the toolkit does not do a thing yet, the finding is below and not in a rule.
- A rule of the source `Owner` was not touched. No plain factual error (a broken reference) was found in one. Everything else about them is under "Decisions for the owner".
- Rules that were added or changed have a line in the changelog of the rulebook and a test (`RuleReviewTests`). The ids FORM-23, SHELL-20 and LIST-18 are left free: other changes use them.

## Counts

| Area | Before | After | Added |
|---|---:|---:|---|
| IDENT Identity | 9 | 9 | |
| SHELL Shell | 19 | 19 | |
| LIST Lists and galleries | 17 | 17 | |
| FORM Forms | 22 | 23 | FORM-24 |
| BUTTON Buttons | 6 | 6 | |
| DIALOG Dialogs | 7 | 7 | |
| TABLE Tables | 4 | 5 | TABLE-05 |
| COLOR Colour and themes | 11 | 11 | |
| TYPE Typography | 4 | 4 | |
| SPACE Space and shape | 6 | 6 | |
| ICON Icons | 5 | 5 | |
| MOBILE Phones | 5 | 5 | |
| A11Y Accessibility | 5 | 10 | A11Y-06 to A11Y-10 |
| CODE Markup and code | 10 | 15 | CODE-11 to CODE-15 |
| **Total** | **130** | **142** | **12** |

| | Before | After |
|---|---:|---:|
| Must | 65 | 65 |
| Should | 57 | 69 |
| May | 8 | 8 |
| Source Owner | 41 | 41 |
| Source MatDesign | 38 | 50 |
| Checked by a test (`Check` starts with Test) | 58 | 87 |
| Checked by the palette report | 1 | 1 |
| Checked by review | 71 | 54 |

The 29 more rules that name a test are 10 of the new rules and 19 whose `Check` now says which test or browser check holds them (it said Review). Sixteen rules have a statement of more than 1000 characters (limit 1200), eleven of them more than 1100; FORM-15 and COLOR-11 have 1179.
The changelog has 171 lines: 130 baseline lines (dates from git: 103 rules on 2026-10-10, the others up to 2026-10-15), 12 for the added rules, 29 for the changed ones.

## Findings

Action: **fixed** (changed in this review, with a line in the changelog), **added** (a new rule), **owner** (a proposal, see the last section), **task** (a change to the toolkit that is not part of this work; a follow-up was suggested).

### Rules that said something else than the code does

| Rule | Finding | Action |
|---|---|---|
| A11Y-05 | The CSS calls the spinner "the one exception to A11Y-05" (under reduced motion it turns slowly: a spinner that does not move says nothing is happening). The rule said "reduced to nothing". | fixed |
| A11Y-02 | Named "the search dialog with `/`". Since LIST-03 allows a search field, `/` also puts the cursor in it. Panels (Escape gives the focus back) were not named. | fixed |
| A11Y-03 | Said nothing about images, the footer or the one h1. IDENT-08 and FORM-22 cite A11Y-03 for alt text and for the name of an image, which it did not contain. | fixed (extended) |
| A11Y-01 | The `Check` said "Test (toolkit CSS)". No test held it; some controls were tested one by one. | fixed: two tests (the skip link is the first thing on every page; no rule takes the outline away without a ring, a border or a tint, four exceptions with the test of their ring) |
| BUTTON-05 | "Nothing a finger has to hit is smaller than 36 px" next to MOBILE-04, which says 44 px and speaks of the hit area. | fixed |
| SHELL-12 | "A flash message at the top of the content: a toast ...". A toast is at the bottom right and goes after 6 seconds (so it cannot be the only place for an error); the alert is the one at the top. | fixed |
| FORM-10 | Cited by the catalogue (form-layout, form-grid, cards, accordion) and by the settings pattern for "split a long form into cards, sections or tabs". It was only about the sticky button row. | fixed (extended) |
| ADOPTING.md | The example of the code input lost its closing tags; the next bullet was swallowed by the code block. | fixed |

### Check texts

| Rule | Finding | Action |
|---|---|---|
| IDENT-07, SHELL-01, SHELL-05, SHELL-06, SHELL-08, LIST-03, FORM-08, FORM-15, DIALOG-05, COLOR-06, SPACE-02, ICON-03, CODE-02, MOBILE-04, SHELL-12 | The `Check` was the default Review, but a unit test or a browser check holds them (named now). IDENT-07 got its test in this review (every page of the site: `Page title · Site name`). | fixed |
| SHELL-03, DIALOG-06 | Review, but `tests/e2e/run.js` checks the order of the foot of the menu and Cancel first, danger last. | fixed |
| SPACE-03, FORM-13 | Review, but `ToolkitCssTests` pins no lift on hover and the placeholder of a floating label. | fixed |
| SHELL-07, FORM-04, LIST-05 | The `Check` said a markup or script test. Only a browser check exists. | fixed |
| BUTTON-01 | The `Check` said "Test (CSS contract)". Typed buttons and classes that exist are tested; "no bare `<button>`" is not. | fixed (narrower) |
| 52 more rules | Still Review (54 with A11Y-08 and A11Y-10, which are held by the tests of their controls). Most cannot be tested by code (LIST-01, BUTTON-02, BUTTON-03, TYPE-04 ...). The ones that could name an existing test and were not checked one by one: SHELL-02, SHELL-09, SHELL-10, SHELL-11, FORM-03, FORM-05, FORM-06, FORM-07, FORM-12, DIALOG-04, TYPE-02, SPACE-01. | owner (a pass of an hour) |

### Quality of the rules, for the owner to decide

| Rule | Finding | Action |
|---|---|---|
| DIALOG-03, TABLE-02, FORM-14, MOBILE-02, SHELL-16 | The documented breakpoints are 600, 900 and 1200 px (Layout page, MOBILE-02). The CSS and these rules use 480, 600, 640, 700, 720, 900, 1100 and 1200: a dialog is full screen below 640, a table is cards below 720, a form layout goes back above below 700, the menu is a drawer below 900, the index is a column from 1100. | owner |
| LIST-02 | "Without exception", but LIST-15 (rows), LIST-16 (a grouped table) and LIST-17 (a tree) say nothing about a toolbar, and the rows and the tree have their own filter field (`data-item-filter`, `data-tree-filter`). | owner |
| LIST-03 | Sort is a button that opens a dialog. The table component documents sortable column headers (`aria-sort`). Both stand, neither names the other. | owner |
| A11Y-04 | Level Should. A form error that is not announced fails WCAG 3.3.1; the toolkit does it everywhere. Level Must is the honest one. (Source Matthix, not Owner.) | owner |
| FORM-05 | Names the model's `ErrorMessage` only; the script's own messages are `data-msg-*` (ADOPTING 3a, FORM-24). | owner (low) |
| SPACE-02 | Does not name the 12 px radius (`--mat-radius-xl`: the panel of a picker, the card of the sign-in). | owner (low) |
| Sixteen rules | More than 1000 of the 1200 characters; eleven of them more than 1100 (FORM-15, COLOR-11, SPACE-06, LIST-15, LIST-14, FORM-21, FORM-19, LIST-17, SHELL-15, FORM-22, FORM-17). There is no room to add to them; detail belongs in the catalogue. | owner |
| Ids | In order, none twice, none missing in an area; the references in the rules all exist (a test holds that). No duplicates found: the repeats (a bar at the side, 44 px) cite one rule each. | none |

### What the catalogue and the code say that is not true yet

| Where | Finding | Action |
|---|---|---|
| Component `toasts` | "hovering keeps them". `matui.js` ends every toast with `setTimeout(remove, 6000)`; nothing pauses it. A message that goes while the pointer or the focus is on it fails WCAG 2.2.1. | task (pause on hover and focus) |
| Component `fields` | "On a touch screen inputs are 44 px high and 16 px type, so iOS does not zoom." The fields inherit 15 px (`--mat-fs-md`); only the code input keeps 16 px or more. iOS Safari zooms into a field under 16 px. | task (16 px with `pointer: coarse`, then a MOBILE rule) |
| `matui.js` | Sends `mat:theme`, `mat:accent`, `mat:navset` and `mat:dialog-open`; the header, ADOPTING.md and the changelog name none of them, so by docs/VERSIONING.md they are not part of the contract. | task (document them or say they are internal); a test pins the four so a fifth fails |
| Catalogue citations | `tabs` cites SHELL-09 (the page head), `spinner` cites BUTTON-03 (labels start with a verb), `u-spacing` cites MOBILE-04. They are about something else. | owner (the components pages are changed by other work) |
| The site | Pages send no `Content-Security-Policy`, no `X-Content-Type-Options`, no `Referrer-Policy`; only the brand files and the releases do. CODE-03 says an app works under a strict policy. | owner |

## What was added

Every added rule is of the source MatDesign, level Should, has a reason, an example, a counter-example and a `Check` that is true, and a test in `RuleReviewTests`. The reason in each case is something the toolkit already does.

| Rule | What it says | Why it was missing |
|---|---|---|
| A11Y-06 The page names its language | `<html lang>` on every page; the script writes its texts and the pickers and the schedule write dates in that language; English without it. | Every page of the site has `lang` (the smoke test says so); the script reads it for every text; no rule said so. |
| A11Y-07 State is told by the attribute that names it | `aria-expanded`, `aria-pressed`, `aria-selected`, `aria-checked`, `aria-current`, `aria-invalid`, `aria-busy`; a button at its limit that has the focus is `aria-disabled`, never `disabled`. | 44 uses of `aria-expanded`, 24 of `aria-pressed`, 20 of `aria-disabled` in the script; the rule for the stepper, the carousel and the schedule each said it for itself. |
| A11Y-08 A change that does not move the focus is told in a live region | Toasts, counts of a filter, the page of a carousel, the status of an upload; polite; never hidden. | 18 live regions in the script and the toolbar; the repository guide says "a hidden live region is not read"; only A11Y-04 spoke of announcing, and only for errors. |
| A11Y-09 The focus is not lost | A dialog gives it back, a panel gives it back on Escape, removing a file moves it to the next remove button, errors put it on the first field, a pager keeps it. | The script does each of these and the catalogue notes say so; no rule did. |
| A11Y-10 What is loading says so, and so does what failed | A spinner has a text; a task is a progressbar with a name; `aria-busy`; skeleton; a failure is told in words with a way to try again and is never an empty list. | The spinner and skeleton notes, `is-loading`, and the failed states of the appointment picker, the tree and the upload; LIST-14 and LIST-17 mention a loading state in passing. |
| FORM-24 A form is checked when it is sent | Check on send, message at the field and in the summary, first error gets the focus, an error goes when the field is right, the server draws the same markup. | `matui.js` does exactly this (`submit` handler, one `input` listener that clears an error); FORM-03, FORM-05 and A11Y-04 do not say when. |
| TABLE-05 Selecting rows | A check per item named by what it selects, select all of this page, a tint, a bar that counts and says "this page". | The the repository guide convention (`data-selectable`), ADOPTING.md and the notes of the table, cards and gallery. |
| CODE-11 The texts the script writes can be changed | English and German by `lang`, every key in both, `data-<control>-<name>` changes any text, no other text in the script. | ADOPTING 3a and a test per table of texts (the new generic test finds 11 tables with equal keys); CODE-05 only says the interface is in the app's language. |
| CODE-12 A drawn control keeps its native element | A real input stays the truth, invisible in place, never `display: none`; without the script it is the native control; read values from it. | ARCHITECTURE.md says it for the picker, slider, stepper, rating, code input and upload; each rule said "without the script it is ..." for itself. |
| CODE-13 The names of the script: attributes, events and the API | `data-*` named after what they do; events `mat:<noun>-<verb>`, on the element, with `detail`, cancelable where a page can stop the follow-up; `window.MatUi.<control>`; `MatUi.refresh`; what is written down is the contract. | CODE-04 names classes, properties and attributes only; `ScriptTests` hold the header and the API; 21 events all follow one shape. |
| CODE-14 A sign-in is locked per name and address, headers come from known proxies | Delay and lock per name and per address, one message for every failure, forwarded headers believed only from a known proxy and only the last entry, a setting that cannot be read stops the start. | STUDIO.md and the last commit on the line (the lock-out could be started over by writing a header); CODE-10 covers roles only. |
| CODE-15 A change is finished when it is written down and checked | Rule, changelog line, catalogue example, header of the script, adoption guide, changelog of the toolkit; a unit test, a browser check for what only a browser shows. | the repository guide "the rules are the product", `MenuActionTests` and the other "documented where" tests, the CI check on `CHANGELOG.md`. |

## Rules that were looked at and not added

| Topic | What is there | Why no rule |
|---|---|---|
| Visible focus, skip link | A11Y-01 | Exists. Now tested. |
| Reduced motion | A11Y-05, SPACE-04 | Exist. A11Y-05 corrected. |
| `prefers-contrast`, forced colours | `forced-colors` in three blocks of `matui.forms.css`; nothing for `prefers-contrast`; the `contrast` scheme (COLOR-09) is a choice of the user | A rule would claim more than the toolkit does. |
| Print | No `@media print` anywhere | Nothing to describe. |
| Right to left, logical properties | 38 `margin-inline` next to 39 `margin-left`, no `[dir]` or `:dir()` | The toolkit is not ready for it and both languages of the family are left to right. |
| Number, date, time formats | Posted in fixed formats (FORM-15), shown in the language of the page (A11Y-06); the invariant culture is how this site is made (the repository guide) | A rule of the site, not of the apps. Currency: nothing in the code. |
| Error pages, offline | SHELL-11, MOBILE-05 | Exist. |
| Empty states, destructive confirmation | LIST-11, FORM-11, FORM-09, DIALOG-06 | Exist. Undo: no feature. |
| `autocomplete`, file and image handling | FORM-12, FORM-21, IDENT-08, A11Y-03 | Exist. |
| Performance budget | The components page is about two megabytes (a comment in `run.js`) | The site would break its own rule. Owner. |
| Caching and file names | IDENT-08 (brand files by hash, `immutable`); `/ui/matui.css?v=<hash>`; releases never change | Only this site and its brand do it; no promise for apps. |
| `rel` on links | `target="_blank"` has `rel="noopener"` in both places it is used | Two places. |
| A mobile rule for 16 px inputs | See above | The toolkit does not do it yet. Written into the follow-up. |
| Keyboard shortcuts, drag and drop alternatives | A11Y-02 (`/`), LIST-15, FORM-21 | Exist. |

## Decisions for the owner

1. **Phone thresholds** (DIALOG-03 640, FORM-14 700, TABLE-02 720, MOBILE-02 900, SHELL-16 1100, documented 600, 900, 1200). Move the thresholds of the dialog, the form and the table to the documented ones, or document the eight as they are? DIALOG-03 is an Owner rule, so nothing was changed.
2. **The toolbar rule** (LIST-02 "without exception"): do rows, a grouped table and a tree need one, or is their own filter field enough? And may a table have sortable headers next to the Sort dialog (LIST-03)?
3. **Levels**: A11Y-04 from Should to Must? The twelve added rules are Should as agreed; CODE-14 (a sign-in) and A11Y-08 to A11Y-10 would be Must in a stricter book.
4. **Browser security headers for the site's pages** (CSP, nosniff, referrer): add them, with a test, and then a rule in CODE?
5. **Right to left, print, `prefers-contrast`**: wanted, or stated as not supported?
6. **Performance budget**: the components page is about two megabytes; set a budget for it first, then a rule.
7. **The 52 remaining Review rules**: a pass that names the tests that exist, and drops the claim where none does.
8. **The catalogue's mis-citations** (tabs, spinner, u-spacing): fix with the components pages.
9. **Follow-ups suggested**: pause a toast while pointer or focus is on it; 16 px inputs on touch screens, then a MOBILE rule; document or hide the four events. Each changes `src/MatUi` and so needs its line in `CHANGELOG.md`.
10. **The changelog starts at 2026-10-20.** Between the first day and now 23 rules were changed after they were added (SHELL-03 seven times, FORM-15 thirteen times: `node tools/rule-history.js --changes`); those changes are not in it. They can be added as lines with their dates if wanted; the notes would have to be written by hand.
11. **The hash covers statement, level and source.** A new title, reason, example or check needs no line; the review wrote lines for check changes anyway. Add the title to the hash?

## Repeat it

`node tools/rule-history.js --changes` (what moved since a rule was added), `node tools/rule-hash.js --all`, `dotnet test --filter RuleChanges`. The checks of this review are in `tests/MatDesign.Tests/RuleReviewTests.cs`.
