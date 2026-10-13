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

## 3. The shell (`SHELL-*`)

Replace the app's own frame with the shell:

| What | Where it is written |
|---|---|
| Menu on the left, logo and name above it | `SHELL-01` |
| Items: icon + label, the current one marked, groups with small headings, counts as badges | `SHELL-02` |
| At the bottom: the user (opens a context menu, *Sign out* last) next to the 3-state theme button, the version below | `SHELL-03`, `SHELL-04`, `SHELL-05`, `SHELL-06` |
| The menu scrolls on its own; the page never scrolls | `SHELL-07` |
| A breadcrumb above every page | `SHELL-08` |
| Page head: icon, title (`h1`), one line | `SHELL-09` |
| Badges at the right end of an item: a count (`99+` above 99) or a dot, in the tone of the meaning, with a text for screen readers | `SHELL-14` |
| Submenus: a parent is a button that opens its children, one level deep; the branch with the current page is open | `SHELL-13` |
| More than one menu (for example Administration): one panel of items per menu and the switcher under the logo | `SHELL-15` |
| A long page has an index beside it ("On this page"), an icon in the header bar that opens it in a dialog on small screens | `SHELL-16` |
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

A text that goes into the markup of a page (a label, a hint, the words of a `data-confirm`) is the app's own and stays in the language of the app (`CODE-05`).

## 4. Lists and forms (`LIST-*`, `FORM-*`)

- **CRUD is a list and a separate page** for create and edit (`LIST-01`). No inline editing, no modal for create or edit.
- **A toolbar above every list and every gallery** (`LIST-02`), with **Filter and Sort as buttons that open dialogs, and the search as a button or, for a list that is searched all the time, as a field in the toolbar with the dialog buttons at its right end** (`LIST-03`). Use the pattern of the **Toolbar**
  component: one GET form, every dialog sends the whole state, tools in effect are shown as chips (`LIST-04`).
- Pagination directly under the table (`LIST-06`), the list actions below it, left-aligned (`LIST-07`); *Delete* lives on the edit page (`LIST-08`).
- **Buttons are always under the content** of a create or edit page, positive to negative: *Save*, *Back*, a space, *Delete* (`FORM-01`). Put them in `mat-btn-row`; the order is enforced.
- Labels above, hints below, errors at the field; `novalidate` on the form and the messages from `data-msg-*` (`FORM-03`, `FORM-05`). A dependent field appears only when it is needed
  (`data-show-if`, `FORM-04`).
- The right control for the value (`FORM-12`): the type of the value (`email`, `tel`, `number`, `date`) with `autocomplete` and `inputmode`; radios up to five choices, a select above; a switch applies at once, a check is saved.
  A form with errors shows a **summary** at the top with a link to every field (`data-validation-summary`, `FORM-03`). Layouts: columns, label beside the field on a wide screen, one line, sections (`FORM-14`).
- Every table cell has `data-label`: on a phone the rows become cards (`TABLE-02`).
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

## 5. Dialogs (`DIALOG-*`)

- **Every confirmation, alert and input is a dialog** of the toolkit: `data-confirm="…"` on a button, link or form; `MatUi.confirm`, `MatUi.alert`, `MatUi.prompt` from a script. Remove
  every `window.confirm`, `alert` and `prompt` (`DIALOG-01`).
- **On a phone every dialog is full screen with the X at the top right** (`DIALOG-03`): the toolkit does this for `<dialog class="mat-dialog">`. If the app has its own modal, replace it.
- Destructive confirmations: *Cancel* first, a space, the red button last (`DIALOG-06`).

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
