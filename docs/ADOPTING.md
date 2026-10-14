# Moving an app onto MatUi

For the owner of an app of the family. The aim is not to copy a look but to **delete code**: the menu, the dialogs, the toolbar, the buttons, the colours and the dark mode
come from the toolkit, and what is left in the app is what only this app does.

Look first at where the app stands: **Identity → Audit** on the site (a static scan of its repository, one column per rule) and **Identity → Apps → your app** (the accent, its
contrast, what was found in the app).

## 1. Put the toolkit into the app

Download the archive on the **Download** page and copy `matui.css`, `matui.js`, `matui.boot.js` and `matui-icons.svg` into the static files of the app. **Copy, do not link**
(`CODE-03`): the app has to work offline and as a PWA. Update by copying again; the version is the first line of `matui.css`.

Layout (the skeleton of a whole page is on the **Shell** page):

```html
<html lang="en" data-app="yourapp">
<head>
  <script src="/matui.boot.js"></script>
  <link rel="stylesheet" href="/matui.css" />
</head>
<body>
  <!-- the contents of matui-icons.svg, inline, once -->
  ...
  <script src="/matui.js"></script>
</body>
</html>
```

`data-app` and `data-theme` belong on `<html>`. In a .NET solution you can reference the `MatUi` project instead (it is a Razor class library; its files are under `/_content/MatUi/`).

## 2. One colour

The app sets one colour and nothing else:

```css
[data-app="yourapp"] { --mat-app-accent: #2f6feb; }
```

- White text on it must reach 4.5:1. If the colour is too light, also set `--mat-app-on-accent: #0b1020` (and `--mat-app-accent-text` for links, see `matarchive` in `matui.apps.css`).
- The colour has to be in the **registry** (`AppRegistry.cs`) and in `matui.apps.css` of this repository; a test fails when the two differ and when the contrast is not AA.
- Check it in the **Builder** (derived colours, contrast in light and dark, the nearest app) and on **Identity → Palette** (is it too close to another app, `IDENT-04`).
- If the app already has a colour users know, keep it where AA allows and say so in the registry (`FoundAccent`); the site then shows what changed and why.

## 2a. Colour schemes (optional, `COLOR-09`)

An app can let its users choose the surfaces besides the standard look: **`paper`** (warm, slightly yellowed), **`contrast`** (pure white or pure black, strong lines instead of shadows, the borders of controls at 3:1 or
more) and **`oled`** (a black page and menu, near-black surfaces; dark mode only, in light mode it is the standard look). It is one more attribute on `<html>`, next to `data-theme`, `data-app` and `data-accent`:

```html
<html lang="en" data-app="yourapp" data-theme="dark" data-scheme="oled">
```

- **Opt-in.** Without `data-scheme`, with `standard` or with any other value the app looks as it always did. A scheme changes surfaces (backgrounds, lines, text, the status texts, shadows, the dim layer, the scrollbar) and
  never the accent, so it works with every accent, every preset and both modes. Every combination of scheme, mode and accent is checked for AA: **Foundations → Colour** shows the numbers, the **Builder** takes a scheme next to your accent.
- **From the account.** Write `data-scheme` on every page from the user's setting on the server, `standard` too: an attribute that is there is the account's, and what the browser saved does not override it. Without an account
  the browser keeps the choice (`localStorage` `mat-scheme`) and `matui.boot.js` applies it before the first paint, so the page does not flash.
- **The control.** A segmented control in the settings. Each button is `data-scheme-set="standard|paper|contrast|oled"` (`aria-pressed` follows the choice); where the choice is kept per user, `data-scheme-save-url` on a button or on the
  group around them posts `scheme=<name>` (`standard` for none) with the anti-forgery token of the page, exactly like `data-theme-save-url`. `MatUi.scheme.get()` and `.set(name)` do the same from code, and `mat:scheme` is fired on
  `<html>` when it changes.

```html
<div class="mat-seg" role="group" aria-label="Colour scheme" data-scheme-save-url="/Account/Scheme">
  <button type="button" class="mat-seg__btn" data-scheme-set="standard" aria-pressed="true">Standard</button>
  <button type="button" class="mat-seg__btn" data-scheme-set="paper" aria-pressed="false">Paper</button>
  <button type="button" class="mat-seg__btn" data-scheme-set="contrast" aria-pressed="false">High contrast</button>
  <button type="button" class="mat-seg__btn" data-scheme-set="oled" aria-pressed="false">Black (OLED)</button>
</div>
```

- A scheme is a set of surfaces only. An app that has a second dark mode or its own greys deletes them (`COLOR-02`) and offers a scheme instead.

## 2b. Colours a user chooses (`COLOR-10`)

A tag, a project or a folder can have a colour that the user picked. That colour is **data**, shown as a mark next to the name and set with one attribute, `data-colour="#rrggbb"` (`#rgb` works, in any case), never with an
inline style:

```html
<span class="mat-tag" data-colour="#d6336c">Invoices</span>                                    <!-- a tag with a dot before its text -->
<span class="mat-dot" data-colour="#0d9488" aria-hidden="true"></span> Receipts                <!-- a dot of its own, 10 px -->
<svg class="mat-icon" data-colour="#7c3aed" aria-hidden="true" focusable="false"><use href="#mat-i-folder"></use></svg> Project Alpha  <!-- the icon in that colour -->
<button type="button" class="mat-picker__option" data-value="7" data-colour="#2563eb">…</button>  <!-- a dot before the name of an option, also in the closed field -->
```

- The script writes the colour into `--mat-swatch` when the page loads and in `MatUi.refresh(scope)`, so markup that is added later (a row from a request) needs one call. Anything that is not a hex colour is ignored: no dot,
  nothing inherited. `MatUi.picker.addOption(wrapper, { value, name, colour })` and the answer of a create endpoint (`{ value, name, colour }`) take a colour too.
- The colour is a mark, never the text: the text of a tag stays muted and reaches AA whatever the colour, a thin ring in the colour of the text keeps a white dot visible in light mode and a black one in dark mode, and the name
  is always beside it (`COLOR-04`). So any colour a user can choose is safe, and the toolkit never works out a text colour from it.

## 3. The shell (`SHELL-*`)

Replace the app's own frame with the shell:

| What | Where it is written |
|---|---|
| Menu on the left, logo and name above it | `SHELL-01` |
| Items: icon + label, the current one marked, groups with small headings, counts as badges | `SHELL-02` |
| A list the user can change (folders, lists): its action is a small icon at the right end of the group heading, `<li class="mat-nav__group mat-nav__group--action" role="presentation"><span class="mat-nav__group-title">…</span><a class="mat-nav__group-action" href="…" aria-label="Manage lists">icon</a></li>` (or a `<button data-menu-toggle>` that opens a menu; `NavGroup.Action` in the model of this site), instead of a Manage or New item below the list; a link to the current page carries `aria-current="page"` and is marked like a current item (`NavSet.Contains` counts its address as a page of the menu) | `SHELL-02` |
| At the bottom: the user (opens a context menu, *Sign out* last) next to the 3-state theme button, the version below | `SHELL-03`, `SHELL-04`, `SHELL-05`, `SHELL-06` |
| The menu scrolls on its own; the page never scrolls | `SHELL-07` |
| A breadcrumb above every page | `SHELL-08` |
| Page head: icon, title (`h1`), one line | `SHELL-09` |
| Badges at the right end of an item: a count (`99+` above 99) or a dot, in the tone of the meaning, with a text for screen readers | `SHELL-14` |
| Submenus: a parent is a button that opens its children, one level deep; the branch with the current page is open | `SHELL-13` |
| More than one menu (for example Administration): one panel of items per menu and the switcher, under the logo, directly above the user at the foot (`mat-nav__switch--foot`), both, or at the end of the menu directly above the foot (`mat-nav__switch--end`, after the panels in the markup); the popup of the two lower places opens upwards (`data-menu-place="top"`) | `SHELL-15`, `SHELL-03` |
| A menu that is an area with a page of its own (mail, administration, account): make its entry in the popup a link, `<a class="mat-menu__item" href="/start" role="menuitemradio" data-nav-set="id" data-nav-title="Title">` (`NavSet.Href` in the model of this site): choosing it loads that page, which draws its own menu; the pages of the other menus need no hidden panel of it | `SHELL-15` |
| A context that changes what the whole app shows (the tenant, the organisation): a closed parent in the popup of the switcher, `<button class="mat-menu__item mat-menu__parent" data-nav-group aria-expanded="false" aria-controls="…">`, with a form button for each choice below it (`<form class="mat-menu__form" method="post">` around `<button class="mat-menu__item mat-menu__item--sub" role="menuitemradio" aria-checked="…">`); order of the popup: the menus, the choices, the administration last (`ShellModel.Choices`, `NavSet.Last`) | `SHELL-15` |
| Many menus: group them below a parent in the switcher's popup (a button with `data-nav-group`; the menus carry `data-nav-parent`), one level deep | `SHELL-15` |
| A main action first in the menu, filled with the accent: `<a class="mat-nav__item mat-nav__item--primary">` with an icon and a label, one per app, no badge | `SHELL-19` |
| A long page has an index beside it ("On this page"), an icon in the header bar that opens it in a dialog on small screens | `SHELL-16` |
| Icons at the right end of the header bar (`mat-topbar__actions`), at most four; the main action (scan, add) filled with the accent, last: `mat-btn--primary mat-btn--icon` instead of `mat-btn--ghost`, and a second one beside it as `mat-btn--secondary mat-btn--icon` | `SHELL-17` |
| Sign-in, setup and error pages without a menu: a centred card (`mat-auth`) | `SHELL-11` |

Saving the theme: `data-theme-save-url` on the theme button posts the choice (`theme=system|light|dark`) so it can be kept per user; without it the choice stays in the browser.

## 3a. The language of the toolkit's own texts

The few texts the script writes itself (the label of the theme button, the pickers, the dialogs, the messages of a form) are English or German, chosen by `lang` on `<html>`. An app in another language, or one
that words them differently, changes any of them with an attribute on `<html>`; the markup of a page needs nothing for this. These are all of them:

| Attribute | What it changes |
|---|---|
| `lang="de"` | The built-in language of the pickers and of the theme button: `en` (the default) or `de`. |
| `data-theme-label="Design: {0}. Klicken für {1}."` | The label and tooltip of the theme button; `{0}` is the current theme, `{1}` the next one. The script writes it again whenever the theme changes, so the label in the markup of the button is only what is shown before the script has run. |
| `data-theme-name-system`, `data-theme-name-light`, `data-theme-name-dark` | The three names in that label (built in: System, Light, Dark and System, Hell, Dunkel). |
| `data-pk-<name>` | One text of the pickers, in lower case after `data-pk-`: `data-pk-apply="Speichern"`. The names are the keys of `PK_TEXT` in `matui.js`, which also holds the texts of the theme button and the AM and PM of a 12-hour time; a test keeps English and German complete. |
| `data-dlg-ok`, `data-dlg-cancel`, `data-dlg-close`, `data-dlg-confirm`, `data-dlg-notice`, `data-dlg-input` | The buttons and titles of `MatUi.confirm`, `alert`, `prompt` and `data-confirm`, and the X of a dialog or a toast. English when absent: set them on a page that is not in English. |
| `data-msg-required`, `data-msg-type`, `data-msg-pattern`, `data-msg-range` | The messages of a form (`FORM-05`); each can also be set on one field. |
| `data-copied` | The tip after *Copy*. |
| `data-locale`, `data-week-start` | The language of the dates in the pickers (default `lang`) and the first day of the week (0 Sunday to 6, default 1). |
| `data-up-<name>` | One text of the file upload (`FORM-21`), in lower case after `data-up-`: `data-up-refusedtype="{0}: Format nicht erlaubt."`. The names are the keys of `UP_TEXT` in `matui.js`; `{0}`, `{1}` and `{2}` are the file name, the allowed types and the sizes. |

A text that goes into the markup of a page (a label, a hint, the words of a `data-confirm`) is the app's own and stays in the language of the app (`CODE-05`).

## 4. Lists and forms (`LIST-*`, `FORM-*`)

- **CRUD is a list and a separate page** for create and edit (`LIST-01`). No inline editing, no modal for create or edit.
- **A toolbar above every list and every gallery** (`LIST-02`), with **Filter and Sort as buttons that open dialogs, and the search as a button or, for a list that is searched all the time, as a field in the toolbar with the dialog buttons at its right end** (`LIST-03`). Use the pattern of the **Toolbar**
  component: one GET form, every dialog sends the whole state, tools in effect are shown as chips (`LIST-04`).
- Pagination directly under the table (`LIST-06`), the list actions below it, left-aligned (`LIST-07`); *Delete* lives on the edit page (`LIST-08`).
- **Buttons are always under the content** of a create or edit page, positive to negative: *Save*, *Back*, a space, *Delete* (`FORM-01`). Put them in `mat-btn-row`; the order is enforced.
- Labels above, hints below, errors at the field; `novalidate` on the form and the messages from `data-msg-*` (`FORM-03`, `FORM-05`). A submit button with `formnovalidate` (*Delete*, *Save as draft*, a button that only
  asks the server for something) sends the form without the check, as in the browser. A dependent field appears only when it is needed (`data-show-if`, `FORM-04`).
- The right control for the value (`FORM-12`): the type of the value (`email`, `tel`, `number`, `date`) with `autocomplete` and `inputmode`; radios up to five choices, a select above; a switch applies at once, a check is saved.
  A form with errors shows a **summary** at the top with a link to every field (`data-validation-summary`, `FORM-03`). Layouts: columns, label beside the field on a wide screen, one line, sections (`FORM-14`).
- Every table cell has `data-label`: on a phone the rows become cards (`TABLE-02`).
- A value on a scale whose exact number does not matter is a **slider**, a from and to a **range slider** (`FORM-17`): native `input type="range"` (one, or two named `from` and `to`) in a wrapper
  `mat-slider[data-slider]` (`mat-slider--range` for two), so a form posts them and they work without the script. The script draws the track, the thumbs (a target of 44 px) and the bubble with the
  value; `data-format="{0} €"` is the template of every value shown, `data-min-gap` keeps two thumbs apart, `data-bubble="always"`, `data-ticks` and `data-output="id"` are optional:

  ```html
  <div class="mat-field">
    <label class="mat-label" for="price-from">Price</label>
    <div class="mat-slider mat-slider--range" data-slider data-min-gap="10" data-format="{0} €">
      <input class="mat-slider__input" type="range" id="price-from" name="from" min="0" max="200" step="5" value="20" />
      <input class="mat-slider__input" type="range" id="price-to" name="to" min="0" max="200" step="5" value="80" />
    </div>
  </div>
  ```
- A list that acts on several items at once is a `data-selectable` element: a check per item (`data-select-row`, with an `aria-label` that says which item), maybe one that selects all of them (`data-select-all`) and a
  `data-bulk-bar` with a `data-bulk-count` that shows while something is selected. **The item of a ticked check is marked `is-selected`**, a tint behind the whole item (`COLOR-08`): its table row, its `.mat-tile`, or the
  nearest element with `data-select-item`, so a list of cards, or of rows of your own, needs no script of its own to show what is selected.
- A value that a native control cannot show well is chosen in a **picker** (`FORM-15`): an option with a picture, a description and tags, a date or a period, a time or a time range, a colour.
  The markup is a wrapper around the real input, which posts one value in a fixed format (`2026-10-10`, `2026-10-10..2026-10-14`, `09:30`, `09:00..17:30`, `#b4235f`, the key of an option);
  the script draws the button and the panel, which is full screen with an X on a phone. The MatCMS `richselect` and date pickers map onto `data-picker="select"` (with `data-multiple` for
  `multiselect`) and `data-picker="daterange"`:

  ```html
  <div class="mat-field">
    <label class="mat-label" for="due">Due date</label>
    <div class="mat-picker" data-picker="date" data-clearable data-min="2026-10-05">
      <input class="mat-picker__input" id="due" name="due" value="2026-10-10" />
    </div>
  </div>
  ```
- Things that are looked at, opened or ticked off one by one (contacts, messages, settings, files, tasks) are a **list of rows** (`LIST-15`): `ul.mat-itemlist` of `li.mat-item`, a row is one target
  (`mat-item__main`: a link, a button or a label) with a lead, a body and an end; a switch or a button beside it goes in `mat-item__aside`, never inside the link. Add `data-itemlist` for sliding actions
  (`mat-item--sliding`, listen to `mat:item-action`), reordering (`data-reorder` and a `mat-item__handle`, listen to `mat:reorder`) and a filter field (`data-item-filter`). A table that is searched and sorted stays a table (`LIST-02`).

- A **code** that is read from somewhere else (an authenticator or SMS code, a PIN, a recovery code) is a **code input** (`FORM-19`): one box per character, drawn by the script around a real input that posts the code joined.
  `data-chars` is `digits` (the default), `letters`, `alphanumeric`, `hex` or a class such as `[A-Z2-7]`; `data-mask` shows dots; `data-group="4-4"` draws groups; a form is sent with the last character only if you write `data-submit`.
  An app that rejects a code calls `MatUi.otp.clear(el)` and `MatUi.otp.setInvalid(el, "Message")`:

  ```html
  <div class="mat-field">
    <label class="mat-label" for="code">Authentication code</label>
    <div class="mat-otp" data-otp data-length="6" data-chars="digits" data-submit>
      <input class="mat-otp__input" id="code" name="code" inputmode="numeric" autocomplete="one-time-code" required pattern="[0-9]{6}" />
    </div>
    <span class="mat-error" aria-live="polite"></span>
  A dropdown that holds a **list of checkboxes** is the same select with plain options (a button with `data-value` and a label) and `data-multiple`; `data-select-all` adds a first row Select all
  whose box is empty, ticked or in between and acts on the options that are shown, so `data-search` narrows it.
- A number that people change by a step or two (a quantity, a timeout) is a **number stepper** (`FORM-20`): a native number field in `<div class="mat-stepper" data-stepper>` gets a button for less and one
  for more from the script (`data-unit="GB"` for a suffix, `mat-stepper--stacked` for two small arrows, `mat-input--sm` / `--lg` for the size); without the script it stays the native field:

  ```html
  <div class="mat-field">
    <label class="mat-label" for="qty">Quantity</label>
    <div class="mat-stepper" data-stepper>
      <input class="mat-input" type="number" id="qty" name="qty" min="1" max="10" step="1" value="3" />
    </div>
  </div>
  ```

- A value that people give on a short scale (a review, a priority) is a **rating** (`FORM-22`): a `fieldset` with `mat-rating` and `data-rating`, a legend and a native radio with its label per value, so a form posts it and,
  without the script, it is a row of radio buttons. The script draws stars over them (`mat-rating--heart` or `--dot` for hearts and dots, `mat-rating--sm` / `--lg`); the radios say what can be chosen, so half stars are the values
  0.5, 1, 1.5 ... and a radio with the value 0 is "no rating". `data-clearable` lets a click on the chosen value clear it, `data-show-value` writes the value beside the shapes, `mat:rating-change` tells the page.
  An average that is only shown is `<span class="mat-rating mat-rating--readonly" data-rating data-value="4.5" data-count="128" data-show-value>4.5 out of 5</span>` (the text is what is seen without the script; the script adds
  `role="img"` and a label in words). Texts: `data-rt-<name>` on `<html>`; API `MatUi.rating.get(el)` / `.set(el, value)`:

  ```html
  <fieldset class="mat-field mat-fieldset mat-rating" data-rating data-clearable data-show-value>
    <legend class="mat-legend">Overall</legend>
    <input class="mat-rating__input" type="radio" id="q-0" name="q" value="0" checked /><label class="mat-rating__label" for="q-0">No rating</label>
    <input class="mat-rating__input" type="radio" id="q-1" name="q" value="1" /><label class="mat-rating__label" for="q-1">1 star</label>
    <!-- ... one radio and label for each value up to 5 -->
  </fieldset>
  ```

- A month that is **always shown** (`FORM-18`) is a calendar: `mat-calendar` with `data-calendar` around the real input `mat-calendar__input`, for one day, a period (`data-mode="range"`) or several days
  (`data-mode="multiple"`). It posts `2026-10-10`, `2026-10-10..2026-10-14` or `2026-10-10,2026-10-12`. `data-min="today"` (also `tomorrow`, `+3d`, `-1w`) means nobody can pick the past, `data-disabled`
  and `data-disabled-weekdays="0,6"` close days, `data-events` (JSON) marks days with dots and `data-agenda` lists the events of the chosen day. The date picker takes the same `data-min` and `data-max`.

  ```html
  <div class="mat-calendar" data-calendar data-min="today" data-disabled-weekdays="0,6">
    <input class="mat-calendar__input" id="visit" name="visit" />
  </div>
  ```

- An **appointment** (a doctor's practice, a rental pick-up) is booked with the appointment picker (`data-picker="slot"`, `FORM-18`): a month in which the days with a free time carry a dot and the others cannot
  be chosen, then the times of the chosen day, taken ones disabled with the word "Taken". It posts `2026-10-14T09:30` (with `data-duration="15"`: `2026-10-14T09:30..09:45`). The availability is data:
  `data-slots='{"2026-10-14": ["09:00", "09:15"]}'`, or the page answers `mat:slots-needed` per month (the event carries the month) with `MatUi.picker.setSlots(detail.picker, data, detail.month)`, so that a
  server decides. `data-min="today"`, `data-min-notice="2h"`, `data-unavailable="hide"` and `data-group` shape it; Next free appointment jumps to the first free time.

  ```html
  <div class="mat-picker" data-picker="slot" data-min="today" data-min-notice="2h" data-duration="15">
    <input class="mat-picker__input" id="appointment" name="appointment" />
  </div>
  <script>
    document.addEventListener('mat:slots-needed', async e => {
      const data = await (await fetch('/api/slots?month=' + e.detail.month)).json();   // { "2026-10-14": ["09:00", ...] }
      MatUi.picker.setSlots(e.detail.picker, data, e.detail.month);
    });
  </script>
  ```

- A select can **create what is missing** and **suggest** (`FORM-16`), both opt-in and both attributes of the wrapper; the markup of the options stays as it is.
  - `data-create-url="/QuickCreate?kind=tag"` gives the panel the search field and, for a text that is not the name of an option (any case), a first row *Create "text"*. Choosing it (also with the arrow keys and Enter, and Enter in
    the search where nothing matches) POSTs `application/x-www-form-urlencoded` `name=<text>`, with the anti-forgery token of the page (`__RequestVerificationToken`) when it has one, to that address. The answer is
    **2xx with JSON** `{ "value": "<key>", "name": "<label>", "description": "…" }` (`description` is optional): the option is added and chosen (a select with several choices adds it, keeps the panel open and starts the
    search again), the change event fires on the real input and a live region says what was created. **Anything else** (another status, no answer) keeps the panel open and shows `{ "error": "…" }` of the answer, or a default
    text, under the search field. Only where creating is cheap, reversible and needs nothing but a name.
  - `data-suggest-url="/Suggest?doc=5"` is fetched with GET when the panel opens first and nothing is chosen, and answers `[ { "value": "7" }, { "name": "New Corp" } ]` or `{ "items": [ … ] }`: a `value` is an option that is
    there, a `name` that is not an option is a suggestion that creates it (only with `data-create-url`; a name that equals an option is that option). They stand under *Suggestions* above *All* while nothing is chosen
    and nothing is searched, and the closed field says "3 suggestions" in the colour of a link. A page that gets the suggestions of many pickers in one request calls `MatUi.picker.suggest(wrapper, items)` itself.
  - From code: `MatUi.picker.set(wrapper, value)` chooses a value and fires change (for several choices the whole value: a string joined by `data-sep`, or an array), `MatUi.picker.addOption(wrapper, { value, name, description, tags })` adds
    an option (made with `textContent`, never markup). Texts: `data-create-label='Create tag "{0}"'` on the wrapper, and `data-pk-create`, `-creating`, `-created`, `-createfailed`, `-createhint`, `-suggestions`, `-all`,
    `-suggestioncount`, `-suggestionone` on the wrapper or on `<html>` (section 3a).

- Files that the user sends (documents, photos, attachments) are chosen in a **file upload** (`FORM-21`): `<div class="mat-upload" data-upload>` around the real `<input type="file" class="mat-upload__input">`, which is
  what the form posts and validates (`accept`, `multiple`, `required` are its own; without the script it is the native file field). The script draws a drop zone, the list of the chosen files with a remove button
  each (a thumbnail for an image) and names every refused file with its reason. `data-max-size="10MB"` and `data-max-files="5"` are checked as the files arrive, `data-paste` adds pasted images, `mat-upload--compact`
  is one row. **The server checks type, size and number again.** An app that sends the files itself (XHR, `fetch`) listens to `mat:upload-change` and draws progress with `MatUi.upload.setProgress(el, file, 0..100)` and
  `MatUi.upload.setError(el, file, "Message")`; the texts are `data-up-<name>` on `<html>` (section 3a):

  ```html
  <div class="mat-field">
    <label class="mat-label" for="docs">Documents</label>
    <div class="mat-upload" data-upload data-max-files="5" data-max-size="10MB">
      <input class="mat-upload__input" type="file" id="docs" name="docs" multiple accept=".pdf,image/*" />
    </div>
    <span class="mat-error" aria-live="polite"></span>
  </div>
  ```

## 4a. Layout and utilities

- **Containers and the grid:** a page inside the shell is `mat-page`; one without it (sign-in, a shared link) is `mat-container`. For a layout you have decided use the twelve columns
  (`mat-cols` with `mat-col-12 mat-col-md-6 mat-col-lg-3`); for cards that fill the row `mat-grid`; for the small cases `mat-stack` and `mat-row`. The breakpoints are the same in every app:
  `sm` 600, `md` 900, `lg` 1200 px, mobile first. See the **Layout** page.
- **Utilities** set one property from the scales of the toolkit: spacing on the 4 px scale (`mat-mt-4`, `mat-px-3`, `mat-gap-2`), display and visibility (`mat-d-none mat-d-md-block`), flex,
  text, colour and tints, borders, radius, shadow, sizing. Use them on elements of your own pages, not to repair a component, and prefer `mat-stack` to a margin on every child. See the
  **Utilities** page.

## 4b. More icons: the optional library (`ICON-05`)

The sprite of the toolkit has the icons the apps share. If an app needs more (a smart-home app needs a lot), it takes the **icon library** from the Foundations → *Icon library* page: 1,900 icons in
categories, an icon can be in several, with *General* and *Home automation* first. It is made of files of its own and the toolkit does not depend on it, so skip this section if you do not need it.

- Take what you need: the sprite `matui-iconlib.svg` (inline once per page, ids `mat-il-NAME`, next to the toolkit sprite), or single icons (`/ui/iconlib/svg/NAME.svg`, or the markup from the dialog of an icon), and `LICENSE.txt` with them.
- Use it like the others: `<svg class="mat-icon" aria-hidden="true" focusable="false"><use href="#mat-il-lamp"></use></svg>`. An icon without a visible label needs an `aria-label` (`ICON-03`).
- An icon that gets a meaning in your interface (save, delete, filter …) belongs in the sprite of the toolkit (`ICON-02`: one meaning, one icon everywhere), not only in the library.

## 4c. Change the shape of a control: component properties (`SPACE-06`)

If the app wants its buttons square, its cards roomier or its tabs thicker, it does **not** override `.mat-btn`: it sets one of the documented properties `--mat-<component>-<aspect>`
(68 of them: `--mat-btn-radius`, `--mat-card-pad`, `--mat-table-pad-y` ...; every one with its type and its default on **Foundations → Tokens → Component properties**, on the page of the component and at
`/api/properties`). They have **no value in the toolkit**: each use is `var(--mat-btn-radius, var(--mat-radius))`, so an app that sets none looks exactly as before. Where you set it decides who gets it:

```css
:root             { --mat-btn-radius: 0; }           /* every button of every page of the app */
[data-app="mymat"] { --mat-card-pad: var(--mat-space-4); } /* the apps with that data-app (also inside a preview frame) */
.my-toolbar       { --mat-btn-height: 36px; }        /* the controls inside this one element and nothing else */
```

A custom property inherits, so an element sets it for itself and everything inside it. Use a value of the type the table gives: a length (`6px`, `0`, `var(--mat-space-3)`), a number
(a font weight), a duration; a colour or a font only as a token. Three things to know: a **height is a floor** (`--mat-btn-height`, `--mat-input-height`, `--mat-tabs-height`, `--mat-nav-item-height`,
`--mat-menu-item-height`: their default is the control height, 40 px with a mouse and 44 px on a touch screen), so keep it at 44 px or more under `@media (pointer: coarse)` (`MOBILE-04`: set a smaller one
only inside `@media (pointer: fine)`); a property of the **default size** leaves the `sm` and `lg` sizes alone (the radius is the exception: one value for every size of a control); and a property
changes a shape, never a colour or a shadow (`COLOR-01`, `SPACE-03`). The accent, the surfaces and the dark mode stay the tokens' business.

**Setting them in the Studio** (stage 12): a company edits the properties on *Studio → Brand → Components*, next to the real control with a live preview, and an app on its own edit page (*Overrides of this app*); the Studio writes them
at the end of `matui.css` (the apps part), so an app that copies the files needs to do nothing. The order in which values win is **the toolkit's defaults, the brand, the app (`[data-app="id"]`), and, last, a colour scheme the user chose**
(`data-scheme` is a more specific selector). Ranges that the editor holds a value to: a radius 0 to 24 px, padding and gaps 0 to 48 px, a border 0 to 4 px, the width of a dialog or a toast 200 to 960 px, a weight 100 to 900, small text 11 px and up (table and breadcrumb 12 px).
**Touch targets:** a height of 44 px or more is written for everyone; a height under 44 px (never under 32 px) is written inside `@media (pointer: fine)` and so applies with a mouse only, while a touch screen keeps the 44 px of `--mat-control-h`.
Do the same in an app's own CSS (the example above). `(pointer: fine)` is the device's primary pointer: a laptop with a touch screen counts as a mouse. A brand can also change, inside a colour scheme, the colours that scheme sets, and an app its radii, spacing, type and motion tokens on `[data-app]`.

## 5. Dialogs (`DIALOG-*`)

- **Every confirmation, alert and input is a dialog** of the toolkit: `data-confirm="…"` on a button, link or form; `MatUi.confirm`, `MatUi.alert`, `MatUi.prompt` from a script. Remove
  every `window.confirm`, `alert` and `prompt` (`DIALOG-01`).
- **On a phone every dialog is full screen with the X at the top right** (`DIALOG-03`): the toolkit does this for `<dialog class="mat-dialog">`. If the app has its own modal, replace it.
- Destructive confirmations: *Cancel* first, a space, the red button last (`DIALOG-06`).
- **A toast is seen above an open dialog** (`SHELL-12`): `MatUi.toast(message, { tone })` keeps the toasts inside the topmost dialog the toolkit has opened and puts them back on the page when it closes. A modal dialog
  is in the top layer of the browser, so nothing outside of it is seen above it, whatever its `z-index`: an app does not need a toast layer of its own for its dialogs (and one outside the dialog would be hidden).

## 6. Delete your own design code

- **Colours:** replace literal colours and the app's own variables with the `--mat-*` tokens (`COLOR-01`). The registry lists each app's old token scheme (`TokenScheme`) to ease the search.
  Do not keep a second dark mode: the toolkit's dark set is the one (`COLOR-02`).
- **Inline styles and inline scripts:** none (`CODE-02`). A width that is data goes into a class or is set by the script (`data-value` on `mat-meter`).
- **Decoration:** gradients, glow around buttons, blur behind bars, cards that lift, tinted icon tiles next to titles and upper-case spaced labels go too (`COLOR-06`, `SPACE-02`,
  `SPACE-03`, `SHELL-09`). The toolkit is flat on purpose; an app that keeps its own decoration next to it looks like two designs.
- **Fonts:** the system stack, no web font (`TYPE-01`). **Icons:** the sprite, no icon font, no emoji (`ICON-01`).
- **Classes:** the toolkit's `mat-*` for what it ships; the app's own classes keep the app's prefix (`CODE-02`, `CODE-04`).

## 6a. Keep up with the releases

When the Studio is on, MatDesign publishes **releases**: the toolkit files of one moment under a version, with their hashes. An app that wants to follow them asks `/releases/latest.json`, compares its `version`
with the one it carries, and when the release is newer copies `/releases/<version>/matui.css`, `matui.js`, `matui.boot.js` and `matui-icons.svg` into its static files (or the archive `matui-<version>.zip`),
checking each file against the SHA-256 of the manifest. Every release also carries `styleguide.html`, the rules and every component with its examples as one static page (`/releases/<version>/styleguide.html`), and `design.json` with the same as data. The app still carries its own copy (`CODE-03`): the release is where the copy comes from, not something it loads at run time. The Studio's **Versions** page is where it is written down which release each app has,
and which apps are behind.

**The toolkit has a version of its own** (semantic versioning): it is in the first line of `matui.css` and in `MatUi.version`, and `/Changelog` (also `/ui/CHANGELOG.md` and `/api/changelog?since=0.1.0`) says what changed in each version, what breaks and what an app has to do. `node tools/matui-check.js <folder of the app>` finds the app's copy, says which version it is, whether the files are all one version and whether one was changed by hand, and what an update takes with it. How a version is made and how an app updates, step by step: [VERSIONING.md](VERSIONING.md).

## 7. Check it

1. Run the app, look at **every page in light and dark, and at 390 px** (`MOBILE-01`). Take screenshots and look at them.
2. Open the dialogs on the phone width: full screen, X at the top right.
3. Press `/` on a list: the search dialog opens. Tab through a page: the focus ring is visible, the order makes sense (`A11Y-01`, `A11Y-02`).
4. Run the audit again (`node tools/audit.js`) and commit the new `Data/audit.json`: the row of the app should be green where the code allows it.
5. Add the app to the registry if it is new, with its glyph (an icon of the sprite) and its port.

## What the toolkit will not do for you

- Pick the right words: buttons start with a verb, errors say what to do (`BUTTON-03`).
- Decide what belongs in a dialog and what in a page: a dialog that needs more than a screen of fields is a page.
- Keep the app's own behaviour. MatUi is the frame and the controls; the functions of the app stay the app's.
