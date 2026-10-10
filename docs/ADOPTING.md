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
| A long page has an index beside it ("On this page"), a "Jump to" dialog on small screens | `SHELL-16` |
| Sign-in, setup and error pages without a menu: a centred card (`mat-auth`) | `SHELL-11` |

Saving the theme: `data-theme-save-url` on the theme button posts the choice (`theme=system|light|dark`) so it can be kept per user; without it the choice stays in the browser.

## 4. Lists and forms (`LIST-*`, `FORM-*`)

- **CRUD is a list and a separate page** for create and edit (`LIST-01`). No inline editing, no modal for create or edit.
- **A toolbar above every list and every gallery** (`LIST-02`), with **Search, Filter and Sort as three buttons that open dialogs** (`LIST-03`). Use the pattern of the **Toolbar**
  component: one GET form, every dialog sends the whole state, tools in effect are shown as chips (`LIST-04`).
- Pagination directly under the table (`LIST-06`), the list actions below it, left-aligned (`LIST-07`); *Delete* lives on the edit page (`LIST-08`).
- **Buttons are always under the content** of a create or edit page, positive to negative: *Save*, *Back*, a space, *Delete* (`FORM-01`). Put them in `mat-btn-row`; the order is enforced.
- Labels above, hints below, errors at the field; `novalidate` on the form and the messages from `data-msg-*` (`FORM-03`, `FORM-05`). A dependent field appears only when it is needed
  (`data-show-if`, `FORM-04`).
- The right control for the value (`FORM-12`): the type of the value (`email`, `tel`, `number`, `date`) with `autocomplete` and `inputmode`; radios up to five choices, a select above; a switch applies at once, a check is saved.
  A form with errors shows a **summary** at the top with a link to every field (`data-validation-summary`, `FORM-03`). Layouts: columns, label beside the field on a wide screen, one line, sections (`FORM-14`).
- Every table cell has `data-label`: on a phone the rows become cards (`TABLE-02`).

## 5. Dialogs (`DIALOG-*`)

- **Every confirmation, alert and input is a dialog** of the toolkit: `data-confirm="…"` on a button, link or form; `MatUi.confirm`, `MatUi.alert`, `MatUi.prompt` from a script. Remove
  every `window.confirm`, `alert` and `prompt` (`DIALOG-01`).
- **On a phone every dialog is full screen with the X at the top right** (`DIALOG-03`): the toolkit does this for `<dialog class="mat-dialog">`. If the app has its own modal, replace it.
- Destructive confirmations: *Cancel* first, a space, the red button last (`DIALOG-06`).

## 6. Delete your own design code

- **Colours:** replace literal colours and the app's own variables with the `--mat-*` tokens (`COLOR-01`). The registry lists each app's old token scheme (`TokenScheme`) to ease the search.
  Do not keep a second dark mode: the toolkit's dark set is the one (`COLOR-02`).
- **Inline styles and inline scripts:** none (`CODE-02`). A width that is data goes into a class or is set by the script (`data-value` on `mat-meter`).
- **Fonts:** the system stack, no web font (`TYPE-01`). **Icons:** the sprite, no icon font, no emoji (`ICON-01`).
- **Classes:** the toolkit's `mat-*` for what it ships; the app's own classes keep the app's prefix (`CODE-02`, `CODE-04`).

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
