# Architecture

The decisions behind MatDesign and the reasons for them. When one of them changes, change this file in the same commit.

## 1. One place for the design, as a website and a toolkit

The Mat apps are separate projects with their own repositories, stacks and teams of one. Left to themselves they drifted: different menus, different dialogs, a
`window.confirm` here, a literal white there. MatDesign is the one place that says how an app looks and behaves, and ships the code that does it.

- **A website** because a rule is easier to follow when you can look at it: the rulebook, live examples, real frames of the sample app, the numbers (contrast, distance
  between accents).
- **A toolkit** (`MatUi`) because a rule that has to be re-implemented in every app is a rule that gets broken. The toolkit makes the right thing the easy thing: the button
  row puts *Delete* last whatever order you write the buttons in.
- **Its own container** (port 8060) so it can be opened next to any app, and so the design can be read without installing the apps.

## 2. The CSS is the source of truth

`matui.tokens.css` is read by the site at start (`CssTokens`): the tokens page, the colour page, the accent report and the tests all come from it. There is no second list
to keep in sync. A token's description is the comment behind its declaration, its group the last `/* ---- Name ---- */` heading before it.

The colour maths of the accent derivation (`hover = 86 % accent + 14 % black` …) exists twice: in the CSS as `color-mix(in oklab, …)`, and in `Colors.cs`, because the
site and the tests need the numbers without a browser. A test pins `Colors.Mix` to what CSS gives for a known pair, and the report is calibrated so that **every app in the
registry reaches AA in light and dark**. If the CSS changes, the report must change with it; the contrast test is the alarm.

**One accent per app, everything else derived.** An app sets `--mat-app-accent` (and, only if white text on it is unreadable, `--mat-app-on-accent`, and for the link colour
`--mat-app-accent-text`). Hover, the tints, the focus ring and the dark variant follow. In dark mode the accent is lightened (74 % accent, 26 % white) and the
text on it is dark. This keeps 22 colours consistent without 22 stylesheets.

**Schemes change surfaces, never the accent** (`COLOR-09`). `data-scheme="paper|contrast|oled"` on `<html>` is an opt-in set of surfaces, lines, text colours, shadows, dim layer and scrollbar for the same controls: *paper*
(warm and slightly yellowed), *contrast* (pure white or black, lines instead of shadows, control borders at 3:1) and *oled* (black; dark mode only, in light mode it is the standard look). The values are the **end of
`matui.tokens.css`**, not a part of their own: they are token values like the dark set, so the parser, the descriptions and the test that keeps the dark values written twice are the ones the tokens already have (a scheme's
selectors start with `:root[data-scheme=…]`, so `CssTokens` never takes them for a base token or for the dark set; `CssTokens.Schemes` reads them). A scheme sets the same tokens in light and in dark, so its light values never
show through in dark, and it sets no accent token, so every combination of scheme, mode, accent and app works without a rule of its own. One thing does follow the surfaces: the colour of a link. It is derived from the accent
for a white or near-black page; on warm paper it comes out a little too light, so `--mat-accent-text` takes `--mat-accent-text-keep` (100 % in the standard look, 75 % on paper in light) of itself and the rest from
`--mat-text`. That one share is what a scheme sets, for every accent and every app at once, and the standard look is byte-for-byte what it was (a test pins it, and the standard values were compared in a browser).
`SchemeSurfaces` reads a scheme in one mode from the CSS and `AccentReport.Checks(surfaces)` and `SchemeSurfaces.Checks()` hold every pairing to AA for every app and every accent preset in every mode a scheme has, so the numbers on
the Colour page are the CSS's. Where the standard look is a little under for one accent (a link of Matarchive on the page background, the link of Matdo on a chip in dark) the pairing is shown as advice for the standard look and
required for every scheme: the standard look does not change.

## 3. The registry is data, the rulebook is code

`AppRegistry` lists the apps with the accent the toolkit uses, the accent found in the app (so the site can say what changed and why), the logo colour, the class prefix and
the repository. `matui.apps.css` has to equal it (a test), so the family's colours are written once in the registry and once as CSS, and cannot differ.

`RuleBook` holds the rules in C#, not in Markdown: a rule has an ID, an area, a level (Must / Should / May), a source (the owner's own words, or the app whose documentation
already had it) and, where a test enforces it, a `Check`. In code they can be searched and linked, counted, cited by other rules and verified (a rule that cites an unknown
ID fails a test). `/api/rules` gives the same list as JSON for tools and agents.

## 4. The site eats its own dog food

The site's layout is the shell of the toolkit (`Pages/Shared/_Layout.cshtml`), driven by a `ShellModel`. The **Preview** page draws a *sample app* of the family with the same
layout, the same partials (toolbar, pager) and the same controls, in the accent of any app, in light or dark. The frames on the pattern, shell, builder and app pages are
this page. So the examples are not mock-ups: if the shell is wrong, the site is wrong.

The toolbar and the pager are Razor partials driven by models (`ToolbarModel`, `PagerModel`): one implementation for every list on the site and in the samples. The toolbar's three
dialogs belong to **one GET form** through the `form` attribute, so each dialog sends the complete state and a result is a link that can be shared.

### The menu is data

The items of the menu are not markup an app writes but a model the partial `_Menu` draws: a `NavSet` (one menu: id, title, icon) holds `NavGroup`s (a small heading) of `NavItem`s
(title, address, icon, an optional `NavBadge`, optional children). A parent has children and no address: it is a button that opens them, one level deep (`NavSet.IsValid` and a test
check this). A `NavGroup` can carry a `NavGroupAction` (a gear or a plus at the right end of its heading, a link or the id of a menu the app draws; `NavSet.IsValid` checks that it has a heading and exactly one of the two). A `NavBadge` is a count (above 99 it reads `99+`) or a dot, in a tone that follows the status colours; a closed parent shows the sum of its children's badges in the most
urgent tone. An app with more than one `NavSet` gets the switcher; the set that has the current page is the one drawn visible, the others are in the page with `hidden`, so
choosing another one in the switcher swaps them without a request. A `NavSet` with an `Href` is the other kind: an area with a page of its own, whose entry in the popup is a link, so choosing it
loads that start page; the page that loads draws its own menu, and the pages of the other menus carry no hidden panel of it (`_Menu` leaves it out). `ShellModel.Choices` are the other kind of entry in the popup: a context that changes what the whole app shows (the tenant) is a `SwitcherChoices` group, a closed parent with a small form (a `SwitcherChoice`, posted with an anti-forgery token) for each choice; the popup lists the menus, then the choices, then the menus that are `NavSet.Last` (the administration). The popup of the switcher is drawn at the end of the page like the user menu, because the menu has a backdrop filter and
that would make it the frame of a fixed popup; it names the element that holds the panels (`data-nav-scope`) so that an example in the documentation can show a menu without touching the menu
of the page. The choice of open and closed branches is remembered in the browser per app and never overrides the branch of the current page.

The switcher has two choices that belong to the app, both in the model and not in the markup an app writes: **where it sits** (`ShellModel.SwitcherPlace`: `Top` under the logo, the default;
`Bottom` as the first thing in the foot, directly above the user, where its popup opens upwards; `Both`, the buttons stay in step because the script updates every `.mat-switcher` in the scope;
`End` after the panels and above the foot, for a menu that starts with a main action (`SHELL-19`), the popup opening upwards too)
and **whether menus are grouped** (`NavSet.Parent`, a `NavSetParent` with a title and an icon). `ShellModel.SwitcherEntries` gathers the menus of one parent below it: in the popup the parent is a
button (`data-nav-group`, `aria-expanded`, `aria-controls`) that opens a `role="group"` with the menus (`data-nav-parent`), one level deep like the submenus of the items; it is not a menu itself.
The group with the current menu is open when the page loads, the switcher then names the parent above the name of the menu, and a closed parent carries the dot of the menus inside it that
need attention. The popup is placed again whenever a group is opened or closed, so that it never covers the switcher it came from. The sample app shows both with
`/Preview?switcher=bottom|both|end&menus=tree`.

### Forms are a part of their own

The form controls live in `matui.forms.css`, not in `matui.controls.css`: fields and inputs, selects, checks and radios, switches, range, input groups, floating labels, the drop zone, layouts and the
validation summary. The catalogue of the form components is its own file (`FormComponents`) and is merged into `ComponentCatalog.All`, which sorts by group and by an explicit reading order where it matters (the
forms read like a manual). The script adds only what CSS cannot do: the filled part and the output of a range, a counter, the eye of a password, the names in a drop zone, an indeterminate check and the
summary of the errors of a form; validation itself stays the browser's (`required`, `type`, `pattern`) with the messages of `data-msg-*`.

### The code input

A code that is read from somewhere else (an authenticator or SMS code, a PIN, a recovery code, a key) is one box per character (`mat-otp`, `FORM-19`). Like a picker it is a wrapper around a **real input** that is
validated, posted and reset; the script hides it and draws the boxes, and without the script it is a text field. The decisions: the boxes are **one tab stop** with the arrow keys inside (roving tabindex, like a toolbar), because
a code is one value and Tab should go to the next field, and a screen reader gets one group named by the label with each box named by its place; the boxes have **no `maxlength`**, so that an SMS code that arrives whole on the first box
is not cut but spread over the boxes, and no name, so that only the real input posts; an incomplete code is made **invalid** on the real input (`setCustomValidity`), so `required`, `pattern` and the messages of the form work without a
second validation, and the boxes follow `disabled`, `readonly` and `aria-invalid` of the real input; **nothing is sent by itself** unless the author writes `data-submit` (then `requestSubmit()`, so the validation and `data-confirm` still
run). The type stays 16 px or more (a phone zooms into smaller type) and the boxes shrink in width instead of overflowing, so a code of twelve fits a phone.
The **number stepper** (`FORM-20`) is a native number field in a `mat-stepper[data-stepper]` wrapper. The script adds a button for less and one for more and marks the wrapper `is-ready`; only then
the CSS hides the spinners of the browser, so without the script it is the native field. The arithmetic is done in whole numbers (the decimals of `step`, `min`, `max` and the value decide the scale), with the
step base of the browser (`min`, else the `value` attribute, else 0), so a value that the script produces is one that the browser's own validation accepts. A button that cannot go further is
`aria-disabled` and never `disabled` (the focus must not be lost); a press is handled on `pointerdown` (so that holding repeats, faster and faster) and a click without a press (`detail` 0, the keyboard or an
assistive technology) steps once. The buttons are not in the Tab order, the field has the keys (arrows, Page up and down, Shift for ten steps). The stacked variant exists only for `(pointer: fine)`: two arrows
that share the height of one control are too small for a finger (`MOBILE-04`), so a touch screen draws the default. The texts are in `STEP_TEXT` (English and German, a test checks).
The **rating** (`FORM-22`) keeps the native radios as the truth, like the slider keeps its range inputs: the group is a `fieldset.mat-rating[data-rating]` of radios and labels, so a form posts it and the keys, the one Tab
stop and the screen reader are the browser's; without the script it is a row of radio buttons. The script marks it `is-ready` (the radios become invisible and stay in place) and draws two layers of the same row of equal cells
(outlines, and the filled shapes clipped to `--mat-rating-fill`, a share of the whole row), so any value, also 3.7, is drawn exactly, and half of a cell is the middle of its shape. What can be chosen is what the radios are
(half stars are twice the radios), so there is no `data-step`. A pointer computes the first radio value at or above its place; a click checks that radio and the browser's `input` and `change` follow, then `mat:rating-change`.
A read-only rating has no radios: it is one image (`role="img"`, a label in words). The cell is as high as a control, so a touch screen gets 44 px from `--mat-control-h`. The texts are in `RT_TEXT` (English and German, a test checks).

### The file upload

A file that the user sends is chosen in a drop zone (`mat-upload[data-upload]`, `FORM-21`) that wraps a **real file input**, the one thing that is validated and posted; without the script it is the native file field. The decisions:
the script keeps the `files` of the real input **equal to the list** it shows (a `DataTransfer`), so the form posts exactly what the person sees and removing a file takes it out of the post, and a control whose browser
has no `DataTransfer` is left as the native field. The zone has **one Tab stop**, a real `button` whose `::after` covers the whole zone (a click anywhere is a click on the button, Enter and Space need no code), and the
real input is out of the Tab order and the accessibility tree; whoever focuses it (a message of the form) lands on the button. A file is checked as it arrives, from the dialog, a drop, a paste or the API, in one order
(folder, `accept`, `data-max-size`, already listed, room) and **a refused file never stops the others**: each is named with its reason in a live region under the zone, in words with an icon (`COLOR-04`). The limits are
written in the zone before anybody chooses. Without `multiple` a new file replaces the old one. The change of the dialog is put in order before other listeners hear it (capture phase) and a choice that adds nothing is not
heard at all; a drop, a paste and a removal tell the form with `input` and `change` on the real input, like the dialog does. The control **does not upload**: an app that does reads `mat:upload-change` and draws progress
and failures with `MatUi.upload.setProgress` and `setError`, because uploading (where to, with which token, resumable or not) is the app's business and drawing it is the toolkit's. The drag state counts `dragenter` and
`dragleave` (they are paired by the children of the control) and only reacts to a drag that carries files. The texts are in `UP_TEXT` (English and German, a test checks).

### Pickers

A picker is a field whose value is chosen in a panel: a select with pictures, descriptions and tags, a date or a period, a time or a time range, a colour, an appointment (`FORM-15`, `FORM-18`). The tag input is the one
picker that is a field of its own and has no panel to open (`KINDS.tags` is `inline`: no button, it draws chips and a text field from the value; its suggestions are the options of a select, as a
dropdown under the field on every screen size, never a full-screen sheet, because the keyboard stays in the field). They are a part of their own
(`matui.pickers.css`) and one section of `matui.js`; the select and the calendar were taken over from MatCMS, the time and the colour were made in the same style. The markup is a wrapper
`mat-picker[data-picker]` around the real input (`mat-picker__input`), which is what is validated, posted and reset. It holds the value in a **fixed format** (`2026-10-10`,
`2026-10-10..2026-10-14~2`, `09:30`, `09:00..17:30`, `#b4235f`, `2026-10-14T09:30`, the keys of the options), whatever the language of the page; the script adds the button that shows it in the language of the
page (`Intl`) and the panel. The panel is `position: fixed` next to the button (below, above, or covering it when neither side has room) and full screen with an X on a phone, like every dialog.
It lives inside the wrapper, so a picker in a dialog is not cut off by it, and Escape closes the picker first and the dialog second. The real input is invisible but lies over the button, so a
browser message points at the right place, and whoever focuses it (a label, the validation) lands on the button. The texts are English and German (`data-pk-*` on `html` changes them; a test checks that
both languages have every text). The same table holds the label of the theme button (`data-theme-label` and `data-theme-name-*` replace it), so no text that the script writes is English only ([ADOPTING.md](ADOPTING.md), "The language of the toolkit's own texts").

A select can **create what is missing and suggest** (`FORM-16`), both opt-in attributes of the wrapper (`data-create-url`, `data-suggest-url`), so a list that a user keeps does not have to be left to add one more name. The
panel stays the one of the select: the first row of the list is the row that creates (an option of its own kind, `mat-picker__create`, that the arrow keys reach like any other), a suggested entry is a copy of the option
(or, for a name that is not an option yet, a row that creates it) in a group under a heading, and none of them is ever counted as an option of the list (`isReal`). The server is asked with a plain `fetch` (a POST of the name with the
anti-forgery token of the page, a GET for suggestions) and answers JSON; what comes back is turned into an option with `textContent`, never markup, and a refusal keeps the panel open and says why at the row. The markup of an
existing select is untouched and nothing about it changes unless one of the attributes is there. The pages that fetch for many pickers at once, and the app that sets a picker from a button, use `MatUi.picker.suggest`, `.set` and `.addOption`.

### Colours that users choose

A colour a user picked for a tag, a project or a folder is **data**, not a part of the design (`COLOR-10`), and it has one way in: `data-colour="#rrggbb"` (`#rgb` too) on a tag, a dot (`mat-dot`, 10 px), an icon or an option of a picker. The script
writes it into `--mat-swatch`, the property that the colour picker and the swatches already use, and marks the element `is-coloured`; the CSS draws a dot or colours the icon from it, so no page needs an inline style or a literal colour. A
colour that is not a hex colour is ignored and blocked from being inherited (the mark looks neutral). The point of the design is what the colour is *not*: it is never the colour of the text. A tag keeps the muted text colour that reaches AA, so
white, black or a colour that nothing reads well on is as safe as any other, and a thin ring in the colour of the text keeps a white dot visible in light mode and a black one in dark mode. The colour is never the only signal either: the name stands
next to it (`COLOR-04`). `MarkupChecks` flags a `data-colour` that the script would ignore, because that typo would show nothing.

### Slider and range slider

A slider (`FORM-17`) is a native `input type="range"` in a wrapper `mat-slider[data-slider]`; a range slider (`mat-slider--range`) holds two of them, `from` and `to`. The native inputs stay the truth: they post
the values, are validated, have the keyboard and the role of the browser and are what a screen reader reads, so the script only dresses them. Without it the inputs are drawn as native controls in the
look of the toolkit (the two of a range slider one under the other); with it (`is-ready`) they lie invisible over the slider (opacity, never `display: none`, so they keep the focus) and the script draws
one track with the filled part, a thumb per input, a bubble with the value, the marks and the labels, and handles the pointer itself: a press on the rail moves the nearest thumb (both at one value: the
side of the press, or the first movement when the thumbs themselves are pressed), the thumbs of a range slider cannot cross (`data-min-gap`), the one moved last is on top. The thumb is a target of 44 px
(36 px for `--sm`) and the rail has `touch-action: pan-y`, so the page can still be scrolled. The keys are handled by the script as well (arrows, Page Up and Page Down by a tenth of the scale, Home and
End, every key sending `input` and `change`), and a capture listener on `input` puts a value that a screen reader or a script made right (the gap, the grid of the steps) before anybody reads it. Every value
shown (the bubble, the labels, the output) and `aria-valuetext` come from one template, `data-format="{0} GB"`; the two inputs of a range slider are named from the label, "Price from" and "Price to"
(`SL_TEXT`, English and German). Two bubbles that would cover each other become one. A form reset is picked up after the event, `MatUi.refresh` and `initForms` pick up new sliders, and
`MatUi.slider.update(wrapper)` reads a changed scale again.
A **dropdown with a list of checkboxes** is the select picker with `data-multiple`: the box in front of every option is drawn by CSS, so an option stays one button. `data-select-all` adds a first row
that is a `role="checkbox"` button (a `role="option"` has no state in between) with `aria-checked` true, false or mixed; it looks at, and acts on, the options that are shown and can be chosen, so the search
narrows it and a disabled option is left alone. The arrow keys, Home and End walk the rows (that row first), Space is the click of the button. The field names one or two options, counts three or more and says
All selected when every option of a list of more than two is ticked.
The month grid of the date picker is shared, not copied (`FORM-18`): one `calMonth`, one `calPaint`, one set of keys, driven by a **state** (mode `single`, `range` or `multiple`; min and max; `off(date)` for
days that cannot be chosen; marks, a level of days, months or years). The calendar that is always shown (`mat-calendar[data-calendar]`, with a real input `mat-calendar__input`) draws the same grid into the
page; the picker only adds a button and a panel. A limit is a date, `today` or a distance (`dateExpr`: `+3d`). Texts that only the calendar needs live in their own table (`CAL_TEXT`), found through `pkText`.

The appointment picker (`KINDS.slot`, one value `2026-10-14T09:30`, with the length `..09:45`) is a panel picker whose panel holds that grid, with a dot on the days that have a free time, next to the
list of the times of the chosen day (below it on a phone). It adds only what is its own: the availability (`data-slots`, or asked for per month through the event `mat:slots-needed` and answered with
`MatUi.picker.setSlots`, with a loading and a failed state), the minimum notice, the shortcut to the next free appointment and the summary line. A time that has passed is never offered, and a time or a day
that is not free says so in words, not only in a colour (`COLOR-04`). The browser checks answer the event themselves, so that no check depends on the day it runs; the documentation site answers it with
generated days near today (`doc.js`), so that its examples keep working.
### The schedule

The schedule (`LIST-14`, `mat-schedule`) shows events in time: an agenda under sticky day headings, a day, a week, a month and a year, paged by the unit of the view (buttons, keys, a swipe). It is data in, markup out: the author writes an
empty root and a JSON list of events (`data-events`), the script draws the rest, so that the five views, the paging and the texts are one code path. Three decisions are worth knowing. **It follows its own width**, not the window's
(`is-wide` from 640 px, by a `ResizeObserver`): the week is a time grid or a row per day, the month shows events or dots, and the same schedule works in a narrow column of a page. **A length that is data stays out of the markup**: the place of a block
in the hours, a bar over columns and the number of columns are `--mat-sch-*` properties that the script sets with `style.setProperty`, so a strict content security policy holds. **Time is an input**: the calendar weeks are ISO 8601 (a formula that a
test ports and checks against .NET for forty years), today comes from the browser unless `data-now` pins it, and the examples pin it, so that neither a test nor the documentation changes with the day. An app that loads events is told what is
visible (`mat:schedule-range`) and answers with `MatUi.schedule.setEvents`; `data-lazy` shows a loading state in between.
### Lists of rows

The list that a phone app is full of (contacts, messages, settings, files, tasks) is a part of its own (`LIST-15`, in `matui.lists.css` and one section of `matui.js`; the catalogue is `ListComponents`). It is a
`ul.mat-itemlist` of `li.mat-item`, and a row is **one target** with slots: `mat-item__main` (a link, a button, a label or a div) holds the lead, the body and the end, and what else has to be pressed stands beside it
in `mat-item__aside`, because an interactive element inside a link is invalid and a bad target. The list decides the look (lines from the text or none, the grouped card, sticky dividers), the row the content. The names avoid
`mat-list*`, which is the list block of a page (toolbar, table, pager). A flat look needs three decisions that are easy to get wrong: the focus ring is drawn **inside** the row (a row that clips, as the grouped card and a sliding
row do, would cut an outer ring); a hover or press adds a little of the text colour over whatever is behind, so that a selected row keeps its tint; and the card **clips** (`overflow: clip`) instead of hiding, because `hidden` makes a
scroll container of it and a sticky divider would stick to that.

The script does only what CSS cannot, and only for a list with `data-itemlist`. **Sliding actions**: the options stand outside the box of the row (`right: 100%`, `left: 100%`) and the row slides by `--mat-item-x`, so they are never
under a tint that is transparent; they are `inert` and `visibility: hidden` while closed, so the Tab key does not enter them, and a swipe is never the only way: the script adds a More button and the arrow keys open the sides.
**Reordering** moves the other rows with transforms while the pointer holds one, and changes the DOM when it is dropped (moving the node that holds the pointer capture would lose the capture); the keyboard path (Space, arrows,
Escape) moves the node at once and puts the focus back on its handle, and a live region says every step. Both paths end in `mat:reorder` with the keys of the rows in their new order. The checks of rows are the ones of tables
(`data-selectable`), and a filter hides rows by their text, ignoring case and accents. All texts are English and German (`IL_TEXT`).

### Search

**The control** (`FORM-23`, `mat-search[data-autocomplete]`, in `matui.pickers.css` and one section of `matui.js`) is a wrapper around a **real `input type="search"`**, like the pickers: in a `<form role="search">` Enter sends it and without the
script it is a plain search field, which is also what makes it usable in a page that has no `/api/search`. The script moves the input into a bar next to a magnifier and a clear button and adds the popup, a WAI-ARIA 1.2 combobox with a
listbox: the input keeps the focus for good and names the active option with `aria-activedescendant`, the options are links (so a click, Ctrl and click, a touch and a browser without the script all do the one thing a link does) that are not in
the Tab order, and **choosing is one path**: Enter dispatches a click, with the modifier keys, on the active option and the one click handler asks the app (`mat:search-select`, cancelable), remembers the choice and lets the browser follow the link.
The typed text is never replaced while the arrow keys walk. Three decisions are worth knowing. **What a server or a page says is data, not markup**: titles, lines, headings and kinds are `textContent`, the highlight is made of text nodes and a
`mark`, an icon name has to be `[a-z0-9-]+`, and an address is followed only when it is http or https, so a result called `<img onerror>` is shown as text (a test and a browser check hold that). **Answers can be late**: every question counts up a number and an
`AbortController`, an answer is shown only if it is the latest, and whatever closes the popup invalidates the one on its way; a failure is a row ("Search is not available", the way to the page of the results stays) and a slow answer a loading row after
200 ms, so typing is never blocked. **A key that the page already has is not taken**: `data-key="/,mod+k"` names the keys that focus the field, a plain key that another element has as `data-shortcut` (the list pages' own search) stays
that element's and the hint in the field then names the other, and the first visible search of a page that claims a key gets it. On a phone the focused field becomes a **full-screen panel** (`is-full`, `position: fixed; inset: 0`, the field and an X on top, the
suggestions below, the page locked like under a picker): the same input, so the soft keyboard does not flicker, and in the header bar (`mat-topbar__search`, `SHELL-20`) a phone shows an icon button instead of the field. The bar's order is
enforced by CSS (`order` 1 for the search, 2 for the icons; the burger and the breadcrumb keep 0); the width is the documented `--mat-search-width`, the shape `--mat-search-radius` and `--mat-search-height` (a floor: 44 px on a touch screen).
Recent choices are five entries in `localStorage` per app and key, read again like data from a server. The texts are `SRCH_TEXT` (English and German).

**The site's search** (`/api/search`, `/Search`, the field in the header bar of every page of the site and of the Studio, not of a sample app) is `SearchIndex` (`MatDesign.Web/Services`): documents of rules, components, tokens (the custom
properties and the documented component properties), icons (the sprite, and the library, whose tags and categories also make the sprite's icons findable), the pages of the menu, the apps and the patterns, built in memory from what `DesignData`
merges (so a rule saved in the Studio is found) and rebuilt when `DesignData.Version` moves; a query reads a snapshot, so it never waits for a rebuild. **Not searched**: the Studio's own pages and content, the changelog and the docs.
A document has fields with weights (title 10, id 9, keywords 7, code 6, group 3, references 2, text 1.5): the *code* field holds what people copy (the classes and attributes of a component, a token's values), a rule is shown as `FORM-21 File upload` and
searched by `File upload` with its id as a field of its own, and a token is searched without its `--mat-` prefix (`accent` finds `--mat-accent`). A word of the query is looked up in the vocabulary of all documents: the same word, its singular or plural (0.95),
a word it starts (the closer in length the better), a word that holds it (0.35), a word one letter off for a word of five letters or more and two for nine (only where nothing is the word or starts with it: a typo; a swap of two letters is one), and the
words of the **synonyms** (`SearchSynonyms`, English and German, by hand: a group of words that mean the same, found exactly, as a start, as a compound that begins with one, or with a typo). **Every word must be found** (AND), each with the score of its best
place; little words (the, a, how, der, die, mode …) need not be found and count when they are. Bonuses: the exact id of a rule (`form-15`, `FORM 15`, `form15`, `FORM-5`) wins before everything; a title that is the query (or its plural) +200, that starts with it +80,
that holds it at a word +35; a thing that is *named by a synonym* of the word (the page called Switches for "toggle") +25; the whole phrase in the text +8 (only for a query without little words); the bonus of an icon or a token is worth a tenth or a third. Kinds
weigh (pages 1.25, components 1.2, apps 1.05, rules and patterns 1, tokens 0.85, icons 0.7). Text is **folded** by a table, not by `string.Normalize`: the image runs with the invariant globalization, where a normalisation form does nothing for an umlaut (found by running
the site: `haekchen` found nothing in the container while the unit tests, which run with ICU, passed). A query is under 5 ms for the whole index (a test holds it), the answer for a text is cached until the data changes, `/api/search` has an ETag of the version of
the data, and `/Search` is a list page like the others: the search as a field in the toolbar, the kinds as the filter, groups with counts and a link to all of a kind, a pager under a long list.

### Layout and utilities

`matui.layout.css` is the last part before the apps: containers, a grid of twelve columns (`mat-cols`, `mat-col-N`, with `sm`, `md` and `lg` variants from 600, 900 and 1200 px, mobile first) and
utility classes for spacing, display, flex, text, colour, borders and sizing. They come after every component, so a utility wins over a component's value of the same specificity, and none of them uses
`!important`; every value is a token. A child of the grid spans twelve columns by its **end line** (`grid-column-end`): the shorthand would make the start line a span as well, and then no
`mat-col-N` could win (a test pins this).

The pages Layout and Utilities are made of the same catalogue as the components (the groups `Layout` and `Utilities` of `ComponentCatalog`), so everything that is tested for a component example is
tested for them. A `CatalogSection` says which groups a page shows, and one partial (`_DocSection`) draws the three pages. An example uses only classes of the toolkit, because it is copied into other
apps: the demo cells of the layout pages are made of the very utilities that are shown.

### A flat look, on purpose

The first version looked like what a code assistant produces by default: a brand gradient on the logo and behind the hero, a glow around the primary button, glass bars with blur,
a tinted blue-grey page, an icon in a tinted tile next to every title, 18 px corners with soft shadows, cards that lift on hover, upper-case spaced labels. None of that carries
information, and together it makes every app look like every other app that was generated. So the toolkit is plain on purpose:

- **Flat surfaces.** The logo, the avatar and the primary button are the accent colour, nothing more. No gradient backgrounds, no glow, no blur behind a bar or a dialog (`COLOR-06`).
  A gradient is allowed only where it is data: the ramps of the colour picker, the track of a range.
- **Lines, not shadows.** A card is a surface with a border. Only what floats (a menu, a popover, a dialog) has a shadow (`SPACE-03`). Nothing moves on hover; a link card darkens its border.
- **Small corners.** 8 px for cards and dialogs, 6 px for buttons, inputs and menu items, 3 to 4 px for small marks; only badges, status pills and switches are round (`SPACE-02`).
- **Neutral greys.** The page and the surfaces are warm greys in light and neutral dark greys in dark, not a tint of the accent. The accent is for what is actionable or current:
  a button, a link, the tint behind the current menu item and its icon (`SHELL-02`). What is selected is never marked by a bar at one side of a rounded item (`COLOR-08`).
- **Words instead of decoration.** Group labels and table headings are sentence case, not upper case with letter spacing; a page starts with its title, without an icon tile (`SHELL-09`);
  an explanation is a paragraph, not an alert box; a coloured edge on a toast is the icon's colour instead.

A test (`ToolkitCssTests`) pins the parts that are easy to slide back into: no `radial-gradient`, no `backdrop-filter`, no brand gradient token, no upper-case table headings.
All of this is values in `matui.tokens.css` and a few rules per file, so a different decision is a small change, but it is a decision about how every app looks.

### Component properties: handles for the shape of a control

The tokens set the look of everything at once (the radius of a button *and* of an input and a menu item is `--mat-radius`). A brand, and later the Studio's editor, also needs to change one control: square
buttons with round inputs, a card with more room. The toolkit offers that through a small, deliberate set of **`--mat-<component>-<aspect>` properties** (`SPACE-06`; 71, three to six per component: radius,
padding, a height, a border width, a gap, a type size or weight; button, field, card, dialog, context menu, tag, pill, badge, tabs, table, alert, toast, switch, check, menu item, header bar, search field). The decisions:

- **The fallback is the default, the property has no value.** Every use is `border-radius: var(--mat-btn-radius, var(--mat-radius))`: no default in `:root`, so an app that sets nothing computes exactly what it did
  before (a browser check compares the computed styles of every example with and without). The fallback is a global token; only where the toolkit has none (a border of 1 px, the 42 px of a switch) it is a literal
  of the type. A property that is used with two defaults (`--mat-btn-radius` is `--mat-radius-sm` for a small button) says the first in its declaration; the small button follows the same property, so one value squares every size.
- **Set where it should apply.** Custom properties inherit, so `:root` sets one for the app, `[data-app]` for one app and an element for what is inside it: the same three places as the tokens, no class to override.
- **A height is a floor.** `--mat-btn-height`, `--mat-input-height`, `--mat-tabs-height`, `--mat-nav-item-height`, `--mat-menu-item-height` and `--mat-search-height` fall back to `--mat-control-h`, which the tokens make 44 px under
  `(pointer: coarse)` (`MOBILE-04`); a brand that wants less sets it for `(pointer: fine)` only. The editor of stage 12 does exactly that: a value under 44 px is written inside `@media (pointer: fine)` (so a touch screen never sees it and keeps the 44 px of the control height), a value under 32 px is refused, and a browser
  check with a touch-emulating context holds that the button, field, menu item, tab and menu item of the shell stay 44 px high. The limits of every property are in `ComponentTokens` (docs/STUDIO.md, "Components, apps and schemes").
- **The target is the hit area, not the mark.** What is 44 px on a touch screen is what takes the tap (`MOBILE-04`), and it never depends on a property that draws the mark: `--mat-check-size` and `--mat-switch-*` change the
  check and the track, the label around them is 44 px high anyway. Everything that makes a target larger is written inside `@media (pointer: coarse)` (so a mouse sees nothing change: a browser check compares the sizes
  with a mouse) and in one of three ways: the label or the element grows (padding, `min-height`; a rule of `ToolkitCssTests`-style `TouchTargetTests` holds that no coarse rule sets a target under 44 px), a small button has a
  centred `::before` of 44 px (nothing around it moves; not the `::after`, which the tooltip of `data-tip` uses), or the script forwards a tap (the cell around the check of a table row, the row of a field in a list). A row of shapes that has no room (a rating of ten on a phone) goes
  on a second row (`is-wrapped`, `--mat-rating-cols`, set by the script; the filled part is a polygon of two rows). `tests/e2e/touch-audit.js` measures every control of every page with `elementFromPoint` (one measure,
  `touch-hit.js`, shared with the "Touch targets" block of `run.js`) and lists its exceptions with their reasons: compact variants (36 px), a link inside a sentence, the days of a month that has to fit a narrow box,
  a code of many boxes (narrower, never lower) and the sample of the brand editor.
- **No colour, no shadow.** A shape is not a look: colours stay the tokens' business (`COLOR-01`) and the flat look does not get a switch to turn it off (`SPACE-03`, `COLOR-08`). The type `colour` exists in the format for
  a later property and is not used.
- **Declared in the CSS, one line above the rule that uses it**: `/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */` (component, name, type, fallback, description).
  `ComponentProperties` (Core, next to `CssTokens`) reads the lines; the Foundations > Tokens page, the page of each component and `/api/properties` are drawn from them; and a unit test fails when a property of a
  component group is used with a fallback and has no line, a line has no use, a fallback is not a token that exists (or a literal of the type), or a part gives one a value. So the list can never go stale, and the
  editor has something to turn (type, default and description come with it).

### What is deliberately not in the toolkit

Compared with a general kit such as Bootstrap there is **no navbar** (the shell replaces it, `SHELL-01`), **no jumbotron or hero** (an app is not a landing page), **no off-canvas as a
component of its own** (a dialog from the side, `mat-dialog--end`, does it), and **no theming with Sass variables** (the tokens are custom properties and an app sets one colour). What an app needs
beyond that is a component of its own with the app's prefix (`CODE-04`).

### Long pages and one page per section

The components are not a list of pages to click through but one page, grouped, with an index beside it (`mat-with-toc`, `mat-toc`): a sticky column from 1100 px, an icon in the header bar that opens it in a dialog below that
(full screen on a phone); the layout and the utilities are two more pages of the same kind. `data-scrollspy` marks the link of the section at the top; it computes per index from the position of the sections, so the index beside the content and the one in the dialog
agree and two indexes on one page do not disturb each other. Because every example of every component is on the same page, **all ids share one page**: a test checks that no id appears twice and
that every `#link` leads to a section that exists.

## 5. The documentation cannot drift from the toolkit

- **Components** are data (`ComponentCatalog`): each example is one raw string, rendered live and shown as source, **beside the example on a wide window and below it otherwise, always visible and coloured**
  (`CodeHighlighter` wraps tags, attributes, strings and comments in spans on the server; the text stays the code, so Copy copies it; `ComponentExample.Wide` puts the code below for tables, bars and grids).
  Under the title of a component the classes and `data-` attributes of its examples are listed (`ComponentApi`, read from the markup). Each group also has a panel "See … in a page"
  (`ContextPages`, `_ContextPage`): a frame of a view of the sample app (`/Preview`), loaded when the panel is opened, with marks that outline a component in it. A test checks every one: the classes exist in the toolkit's CSS,
  the icons exist in the sprite, every `td` has `data-label`, every button has a type, every control has an id, ids are unique and references resolve, tags are balanced, and
  there is no inline style.
- **Pages** are scanned by a test for classes that no stylesheet defines, inline styles, inline scripts, outside resources and icons that are not in the sprite.
- **The whole site** is started in memory and crawled: every page answers, has one `h1`, the shell, and no inline style; every list page has its toolbar above the table; no
  link or frame leads nowhere.
- **The script** is read by a test: no browser dialog, no module syntax, every `data-` attribute its header documents is handled.

## 6. Copy, do not link

An app never loads MatUi from this site (`CODE-03`): it must work offline, as a PWA and under a strict content security policy, and it must not break when this site is down. The
Download page serves the files (and an archive with a README) for copying; `/ui/*` has `Access-Control-Allow-Origin: *` so a tool can read them. The first line of `matui.css` carries
the version and the hash.

## 7. No build step, no web fonts, no icon font

Plain CSS in ten parts that are bundled in the order of the cascade (`tokens`, `base`, `shell`, `controls`, `forms`, `pickers`, `lists`, `dialogs`, `layout`, `apps`) and one dependency-free script that wires
everything with delegated listeners and `data-` attributes, so server-rendered and dynamically added markup behave the same. System fonts only (the font files of a company's own brand are the one exception: `IDENT-08`, section 12). Icons are one inline SVG sprite
(`tools/icons.js` builds it; CI fails when it is stale). SVG and not an icon font (`ICON-01`): the icons are strokes, a font would need them turned into outlines, and an icon in a page is a `<use>` of
a symbol that takes its colour from the text.

**The icon library** is the optional second set (`ICON-05`): `src/MatUi/wwwroot/iconlib/` holds a sprite of its own (`matui-iconlib.svg`, ids `mat-il-NAME`, so both sprites can be on one page), a
catalogue (`iconlib.json`: categories, and every icon with its categories, tags and origin) and the licence. Nothing in `matui.css` or `matui.js` knows it (a test checks), so an app can do without it, and an
app that wants it takes the whole sprite or single icons (the site serves each as a file of its own and all of them as an archive under `/ui/iconlib/`; a release carries the sprite, the catalogue and the
licence in `iconlib/`). The icons are Lucide's (ISC; no brand logos) plus the ones drawn for MatDesign in the same style where a smart home needs one that Lucide has not (radiator, curtains, motion sensor …).
**An icon can be in any number of categories**: Lucide gives each icon its categories, `tools/iconlib/categories.json` adds *General* and *Home automation* with its ten areas as curated lists (and
*Home automation* is the union of its areas), and `custom.json` names the categories of the drawn ones. `tools/iconlib/import-lucide.js` takes a checkout of Lucide into `lucide.json` (run when Lucide is updated,
the result is committed), and `node tools/iconlib.js` builds the files from the three sources without a network and checks them (valid names, only shapes and `currentColor`, known categories, none empty); CI
fails when what is committed is stale. The page *Icon library* (Foundations) carries only the symbols of the icons it shows, not the whole sprite.

A later part must be more specific than a base class it overrides, because the bundle puts `controls` after `shell` (the drawer's close button had to be `.mat-nav__brand > .mat-nav__close`
to stay hidden on a desktop).

## 8. The audit is a snapshot, and says so

`tools/audit.js` reads the layouts, pages, stylesheets and scripts of the other repositories and records what is *written* there: a sidebar, a breadcrumb, a theme switch, browser
dialogs, literal colours, inline styles. It cannot see how a page looks, so a tick means the code is there, and `unknown` means it could not be told. The result is committed
(`Data/audit.json`) so the site works without the other repositories; running the script again refreshes it. The audit is advice for the owner of an app, not a gate.

## 9. English, invariant

The site is in English; the apps keep the language of their own interface (`CODE-05`). The site formats numbers and dates with the invariant culture, so a developer's machine and a container
show the same text (a German PC printed `46,9 KB`, the container `46.9 KB`).

## 10. Security and operation

The site is stateless unless the Studio is on (docs/STUDIO.md): no database, no secrets in the image, no Docker socket. Without the Studio there is no cookie either. The Studio is the one thing that
writes: into `/data` (a volume), and only where a data directory is set (the image sets it). Its first user, always an administrator, is created by the setup assistant on the first visit; the people are kept as salted PBKDF2 hashes
(`users.json`, written atomically, a file that cannot be read keeps the Studio off); whoever opens the assistant first becomes the administrator, which is the price of a compose file without a password. Its session is a cookie that is
HttpOnly, SameSite Lax and ends after eight hours, when the person's password changes or when the account is disabled or removed; it carries the id of the person and no role, which is read again on every request. A sign-in that fails costs half a second and five of them for one name from one address lock that pair out
for a minute, and signing out is a POST with an anti-forgery token. Who may change what is decided on the server from the role (`CODE-10`, "People and roles" below). The image runs as the unprivileged user of the base image and needs nothing else; the file system can be made read-only (a tmpfs for `/tmp`, the volume for `/data`) and the capabilities
dropped (`read_only`, `cap_drop: [ALL]`, `no-new-privileges`) by whoever wants that, but the compose file leaves it out to stay simple. `Preview` and `Builder` take an app id, a theme, a colour scheme and an accent from the query string and accept only known ids, `light`/`dark`,
a scheme of the registry and a `#rrggbb` value; everything else is ignored. A preview frame never saves its theme or scheme choice for the whole site (`data-preview`).

## 11. Versions and delivery

`Directory.Build.props` is the one place for the version: release `<major>.<minor>.<build>-<date>`, nightly `nightly-<build>-<date>`, local `local-<date>`. CI (`.github/workflows/build.yml`)
checks the icon sprite, builds, runs the tests and pushes a multi-arch image to GitHub Packages (`main` → `latest`, `dev` → `nightly`). The Docker build runs on the builder's own platform, so the
arm64 image needs no emulation for the compile.

## 12. The Studio

The design data used to be written in C# (the registry, the rulebook, the catalogue of components) and a change was a commit. The **Studio** ([STUDIO.md](STUDIO.md)) lets a person maintain it in the browser and
turns the data into releases, without giving up what the site is: the decisions are these.

- **State only where a data directory is set.** The site itself stays stateless. The Studio is on when a data directory is writable (the image sets `/data`, a volume), so the release compose file has no
  variable at all. The first user, an administrator, is created by the setup assistant on the first visit (a name and a password, kept as a salted hash in the volume; the others are made by administrators): the price is that whoever opens the assistant first
  becomes the administrator, and the answer to that is to do it right after the start. `MATDESIGN_ADMIN_PASSWORD` remains as an optional shortcut for automation.
- **People have one role each, and the server decides** (stage 16, `CODE-10`). `StudioRole` and `Capability` (in `MatDesign.Core`, pure, no web types) are the whole of it: three roles (administrator, editor, viewer) as a fixed list of capabilities, and `StudioRole.Can(role, capability)` as the one
  decision. `UserStore` keeps the people in `users.json` (atomic writes, a lock, a file that cannot be read is an error and never an empty list, the guards for the last administrator and for oneself, a name that was used is never given again) and hashes with the code that was always `AdminAccount`'s, so an old hash verifies and an `admin.json` becomes the first user.
  In the web project `[RequireCapability]` is declared on every page model (what opening it needs) and on every changing handler (what the change needs); `StudioPageModel` reads the person again on every request (the cookie carries the id and the stamp, never a role), checks the declarations before the handler runs and answers with the page `_Forbidden`
  at status 403. A minimal endpoint that writes uses `RequireCapability` on its route. A reflection test reads the assembly and fails for a page, a handler or an endpoint that declares nothing; a crawl as each role holds the matrix. What a person may not do is left out of the page or turned off with the reason, which is a courtesy and never the protection.
  The changes to the people are entries of the activity list (`StudioPeople`), carrying who did it and whom, and never a secret.
- **The code stays the base, the Studio puts items on top.** An item with the id of a built-in app, rule or component replaces it; deleting the item brings the built-in one back. `DesignData` is the one place every
  page reads from, and `ToolkitFiles` rebuilds what depends on it (the colours of the apps, the bundle, its hash) when its version moves.
- **An append-only log per item.** A save writes a file with who, when, why and the whole item, atomically; the newest is the current state. History, diff and restore need nothing else, and a restore is a new revision
  that has to pass the same validator as a save, so an old state that is wrong today is refused with the reason.
- **One code for the contract.** The checks that hold the built-in examples in the unit tests (`MarkupChecks`) are the checks that hold a typed example in the Studio; markup that could harm a page is never drawn.
  The validators, the checks page and the release pipeline all call the same code, so a release cannot pass what the page fails.
- **Releases are immutable.** A release is a directory with its files (the toolkit, the design data with every example, and a static styleguide that draws the components with the release's own files), their hashes and a manifest, moved into place whole. The site offers no way to change or delete one: apps may depend on it. Apps learn of the
  newest from `/releases/latest.json` and copy the files; the Studio records by hand which release each app has.
- **A brand is tokens, with types and limits, checked for AA** (`COLOR-11`). The one item `brand` holds values for the tokens that `matui.tokens.css` documents; each has a kind (colour, length, number, duration, font stack, easing) and, where usability depends on it, a limit
  (touch targets, body text, line height, radii). It is written as an overlay after the tokens (in the apps part, so the ten parts stay ten): light on `:root`, the dark set twice and identical, nothing at all for a brand that sets nothing. The contrast checks that the toolkit
  holds for its own surfaces run on the brand's (`SchemeSurfaces.Pairings`, `AccentReport`, every scheme, every app), in the browser while you type for the pairings that do not depend on the accent, and on the server on save and in the pipeline. A scheme's selectors are more specific than the brand's, so a scheme keeps its surfaces.
- **A template is the built-in layer, the tenant chooses it** (stage 14, `IDENT-09`). One instance is the site of one company: the item `tenant` (company, short name, site name, tagline, template, and the accent of a blank site), first written by the setup
  assistant to `tenant.json` and then changed in the Studio with a history of its own. `ITemplate` (in `MatDesign.Core`, no web types) says what a template offers before the Studio saved anything: the apps, the audit, whether the sample pages may name the apps
  of the Mat family, the brand that is in force, the app that is the site itself. **`demo`** is what the site always was (`AppRegistry.All`, the audit snapshot, the Mat brand, the `matdesign` app as the site); **`blank`** has no apps, no audit and a neutral brand, and its site
  is an app made from the tenant (`this-site`, a name that no app of the Studio can have, with the accent of the tenant, written to the apps part of `matui.css`). The rules and the documentation of the components are the toolkit's and are in both. `DesignData` takes
  its built-in layer from the tenant's template (Studio items still on top, read again on `Reload`), and every page asks it (`Apps`, `SiteApp`, `Pick(id)` for a sample app, `Tenant.Apps` for "the Mat apps") instead of naming the registry or the literal `MatDesign`. An instance that
  has no tenant behaves as the demo, byte for byte: a test holds the pages against the ones the site served before there were tenants. Switching the template deletes nothing: the items stay, switching back brings the other layer back.
- **A white label is a function of the source, applied to a copy** (`CODE-09`, stage 15). The one item `whitelabel` (a prefix and a product name) makes the Build stage rename the files it has assembled; the repository keeps `mat-` and `MatUi`, and a
  release without a setting is byte for byte what it was. `WhiteLabel` (Core, pure) is one regular expression over the text with the token boundaries as look-behinds, so a name is never renamed twice and never inside another word; the other names of the family,
  addresses and `matui-check.js` are left alone. The copy is then held to checks written in C# (no node in the image) that compare it with the source: nothing of the source left, the same classes and properties, every icon, the same shape of the script,
  the length of every file, `MarkupChecks` over every renamed example. A finding fails the stage. The traps in `matui.js`, found by reading it and each held by a test: the global `window.MatUi` and the comments; `data-mat-sprite`, the only `data-mat-` attribute, read by its whole name;
  storage keys `mat-theme`, `mat-accent`, `mat-scheme` and the dynamic `'mat-view-' + key` (the prefix stays whole at the start of a string); classes built from parts (`'mat-stepper__btn--' + kind`, `'--mat-sch-' + n`); the expando properties `_matPicker`, `_matUpload` ...
  (renamed as `_acmePicker`); `'#mat-i-' + name` for the sprite; events `mat:...`; no regular expression, no `slice(4)` and no name cut into two strings depends on the length of the prefix (a test reads the script for them); the comment
  `MatUiVersion` (the name of a property of the repository) stays; `design.json` is renamed as JSON because the serializer writes a quote as `"`; a file name can look like a class when the product is called as the prefix (`acme-icons.svg`), which the checks know.
  The look is the same: the browser checks run the renamed toolkit behind the renamer of the site and compare screenshots pixel by pixel with the original.
- **It edits data, not the toolkit.** The CSS and the script are files of the repository, reviewed and tested there; the Studio never writes them. It is also not a CI system for the apps: its release is the design system.
- **A brand's files are immutable, content-addressed and judged by content** (`IDENT-08`). The one item `assets` (logo, word mark, favicon, app icon, font faces) only refers to files in `<data>/assets/<sha256>.<ext>`, which are written once and never overwritten or deleted, so a restore can always find what a revision refers to
  (and is refused when a file is gone). `BrandAssetValidator` (in `MatDesign.Core`, no web types) is the one gate, for an upload, a restore, the check and the pipeline: the type comes from the first bytes or from an XML parse, never from a name; an SVG is read with a DTD forbidden and against an allowlist of elements and attributes, because an
  uploaded SVG is the one place where somebody else's markup reaches the site. The site serves a file only while it still has its hash, under a name that carries the first ten digits of it (so `immutable` is true), with `nosniff` and, for an SVG, a policy that forbids everything. The `@font-face` rules are an overlay after the brand's
  tokens (`ToolkitFiles.AssetsOverlay`) with a URL **relative to the stylesheet**, so the same text is right on the site (`/ui/`), in a release and in an app's own folder; a release carries the files under `brand/`. Nothing in the toolkit's source knows a brand: `.mat-brandmark` (the image of a logo, two of them with a dark variant) is the only
  class added, and a page without assets is byte for byte what it was.

## What it does not do

- It does not change the other apps. Adoption is a decision and a piece of work in each repository ([ADOPTING.md](ADOPTING.md)); the audit shows where each one stands.
- It does not run browser tests in the test project: the behaviour of the script is checked by reading it, and end to end by a real browser against the container (`tests/e2e`).
- It does not pick colours for the apps. The palette check proposes, a person decides.
