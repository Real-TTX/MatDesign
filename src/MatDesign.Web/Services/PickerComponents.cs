namespace MatDesign.Web.Services;

/// <summary>
/// The pickers (FORM-15): a select with pictures, descriptions and tags, a date and a period, a time and a time range, a colour. They are fields that
/// are filled in a panel; on a phone the panel is full screen. The real input inside holds the value in a fixed format.
/// </summary>
internal static class PickerComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    private static string Tile(string icon) => "<span class=\"mat-picker__img\" aria-hidden=\"true\">" + I(icon) + "</span>";

    /// <summary>An option of a select: a tile with an icon, a name, a description and tags (a tag that starts with ! is the accent pill).</summary>
    private static string Option(string value, string icon, string name, string description, params string[] tags)
    {
        var tagHtml = tags.Length == 0
            ? string.Empty
            : "<span class=\"mat-picker__tags\">" + string.Concat(tags.Select(t => t.StartsWith('!') ? "<span class=\"mat-pill mat-pill--accent\">" + t[1..] + "</span>" : "<span class=\"mat-tag\">" + t + "</span>")) + "</span>";
        return "<button type=\"button\" class=\"mat-picker__option\" data-value=\"" + value + "\">" + Tile(icon) +
               "<span class=\"mat-picker__text\"><span class=\"mat-picker__name\">" + name + "</span><span class=\"mat-picker__desc\">" + description + "</span>" + tagHtml + "</span></button>";
    }

    /// <summary>An option that is only words: a name and, if there is one, a line under it. It is what the script makes of an option that is created.</summary>
    private static string Plain(string value, string name, string? description = null)
        => "<button type=\"button\" class=\"mat-picker__option\" data-value=\"" + value + "\"><span class=\"mat-picker__text\"><span class=\"mat-picker__name\">" + name + "</span>" +
           (description is null ? string.Empty : "<span class=\"mat-picker__desc\">" + description + "</span>") + "</span></button>";

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("rich-select", "Select with pictures and tags", "Forms", "tag",
            "A choice where the options need more than a word: a picture, a title, a description and tags. One choice or several, with a search for a long list.",
            ["FORM-15", "DIALOG-03", "A11Y-02", "FORM-03", "FORM-16"],
            [
                new("A choice with a picture, a description and tags", $"""
                    <div class="mat-field">
                      <label class="mat-label" for="pk-plan">Plan</label>
                      <div class="mat-picker" data-picker="select" data-placeholder="Choose a plan">
                        <input class="mat-picker__input" id="pk-plan" name="plan" value="team" />
                        <div class="mat-picker__options" hidden>
                    {Option("free", "user", "Free", "One person, one app, no backups.", "1 user")}
                    {Option("team", "users", "Team", "Shared apps, daily backups and a domain of your own.", "!Popular", "10 users", "Backups")}
                    {Option("business", "briefcase", "Business", "Single sign-on, audit log and priority support.", "Unlimited users", "Audit log")}
                        </div>
                      </div>
                      <span class="mat-hint">The picture, title and tags of the choice stay visible in the field.</span>
                    </div>
                    """, "Open it with a click, Enter, Space or the arrow keys; the arrow keys move between the options, Enter or a click chooses and Escape closes. Tabs and pills are the toolkit's own (`mat-pill`, `mat-tag`)."),
                new("Several choices, with a search and a button that clears", $"""
                    <div class="mat-field">
                      <label class="mat-label" for="pk-apps">Apps in this stack</label>
                      <div class="mat-picker" data-picker="select" data-multiple data-search data-clearable data-placeholder="Choose apps">
                        <input class="mat-picker__input" id="pk-apps" name="apps" value="mail, files" />
                        <div class="mat-picker__options" hidden>
                    {Option("mail", "mail", "Mail", "Mailboxes, aliases and a webmail.", "Needs a domain")}
                    {Option("files", "folder", "Files", "Files and shares for the family.", "Backups")}
                    {Option("calendar", "calendar", "Calendar", "Appointments and tasks that sync with a phone.")}
                    {Option("chat", "message-square", "Chat", "Rooms and direct messages.", "Needs a domain")}
                    {Option("photos", "image", "Photos", "Albums, with faces and places kept on your server.", "Backups", "Large")}
                    {Option("notes", "edit", "Notes", "Short notes that you find again.")}
                        </div>
                      </div>
                      <span class="mat-hint">One value: the chosen keys, joined by a comma (<code>data-sep</code> changes the separator).</span>
                    </div>
                    """, "The list stays open while you choose; more than two choices show their count. The search finds words in the title, the description and the tags. Done closes the panel (the check boxes are drawn, so the option stays one button)."),
                new("In a form: required, with the message at the field", $"""
                    <form class="mat-form" method="get" action="/Components#rich-select" novalidate>
                      <div class="mat-field">
                        <label class="mat-label" for="pk-req">Server <span class="mat-req" aria-hidden="true">*</span></label>
                        <div class="mat-picker" data-picker="select" data-placeholder="Choose a server">
                          <input class="mat-picker__input" id="pk-req" name="server" required data-msg-required="Please choose a server." />
                          <div class="mat-picker__options" hidden>
                    {Option("home", "home", "Home server", "In the cupboard, 100 Mbit up.", "Free")}
                    {Option("vps", "cloud", "Rented server", "Always on, 1 Gbit, 4 euros a month.", "!Recommended")}
                          </div>
                        </div>
                        <span class="mat-error" aria-live="polite"></span>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">{I("check")}<span>Continue</span></button></div>
                    </form>
                    """, "Press Continue with nothing chosen: the message appears under the field, the field is marked and the focus goes to its button."),
                new("Create what is missing", $"""
                    <div class="mat-field">
                      <label class="mat-label" for="pk-create">Project</label>
                      <div class="mat-picker" data-picker="select" data-create-url="/api/demo/create" data-placeholder="Choose or create a project">
                        <input class="mat-picker__input" id="pk-create" name="project" value="" />
                        <div class="mat-picker__options" hidden>
                    {Plain("website", "Website", "The pages of the shop.")}
                    {Plain("app", "Mobile app")}
                    {Plain("backoffice", "Back office", "Accounting and orders.")}
                        </div>
                      </div>
                      <span class="mat-hint">Type a name that is not in the list: the first row creates it. <code>error</code> is refused on purpose, to show the message.</span>
                    </div>
                    """, "With `data-create-url` the panel always has the search field. While the text is not the name of an option (any case) the first row is Create and the name; it posts `name=…` and takes `{ value, name, description }` as the new option, which is chosen. Enter creates where nothing matches. A refusal keeps the panel open and says why under the search field."),
                new("Suggestions, and a name that creates", $"""
                    <div class="mat-field">
                      <label class="mat-label" for="pk-suggest">Tags</label>
                      <div class="mat-picker" data-picker="select" data-multiple data-create-url="/api/demo/create" data-suggest-url="/api/demo/suggest?set=tags" data-placeholder="Choose tags">
                        <input class="mat-picker__input" id="pk-suggest" name="tags" value="" />
                        <div class="mat-picker__options" hidden>
                    {Plain("finance", "Finance")}
                    {Plain("contracts", "Contracts")}
                    {Plain("family", "Family")}
                    {Plain("network", "Network")}
                    {Plain("work", "Work")}
                        </div>
                      </div>
                      <span class="mat-hint">Nothing is chosen, so the field counts the suggestions. They are asked for when the panel opens first.</span>
                    </div>
                    """, "`data-suggest-url` answers `{ \"items\": [ { \"value\": \"finance\" }, { \"name\": \"Invoices 2026\" } ] }`: a value is an option that is there, a name that is not an option creates it (with `data-create-url`). They stand under Suggestions, above All, until something is chosen or searched."),
                new("Suggestions only", $"""
                    <div class="mat-field">
                      <label class="mat-label" for="pk-folder">Folder</label>
                      <div class="mat-picker" data-picker="select" data-suggest-url="/api/demo/suggest?set=folders" data-placeholder="Choose a folder">
                        <input class="mat-picker__input" id="pk-folder" name="folder" value="" />
                        <div class="mat-picker__options" hidden>
                    {Plain("documents", "Documents", "Letters, contracts, invoices.")}
                    {Plain("photos", "Photos", "Scans of pictures.")}
                    {Plain("receipts", "Receipts")}
                    {Plain("manuals", "Manuals")}
                        </div>
                      </div>
                      <span class="mat-hint">Without <code>data-create-url</code> only options that are there can be suggested.</span>
                    </div>
                    """),
                new("Both, in a form: required, with the message at the field", $"""
                    <form class="mat-form" method="get" action="/Components#pk-corr" novalidate>
                      <div class="mat-field">
                        <label class="mat-label" for="pk-corr">Correspondent <span class="mat-req" aria-hidden="true">*</span></label>
                        <div class="mat-picker" data-picker="select" data-create-url="/api/demo/create" data-create-label="Create correspondent &quot;{"{0}"}&quot;" data-suggest-url="/api/demo/suggest?set=tags" data-placeholder="Choose a correspondent">
                          <input class="mat-picker__input" id="pk-corr" name="correspondent" required data-msg-required="Please choose or create a correspondent." />
                          <div class="mat-picker__options" hidden>
                    {Plain("finance", "Finance", "The bank and the tax office.")}
                    {Plain("contracts", "Contracts")}
                    {Plain("family", "Family")}
                          </div>
                        </div>
                        <span class="mat-error" aria-live="polite"></span>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">{I("check")}<span>Continue</span></button></div>
                    </form>
                    """, "`data-create-label` words the row for this field; `data-pk-create`, `-creating`, `-created`, `-createfailed`, `-createhint`, `-suggestions` and `-all` change the other texts, on the field or on `html`. The created option is a real option: the required message goes away and the change event fires."),
            ],
            [
                "An option is a button with `data-value`: an optional picture (`mat-picker__img`: an `img` with an empty `alt`, or an icon in a tile), a `mat-picker__name`, a `mat-picker__desc` and `mat-picker__tags`. The options sit in a `mat-picker__options` list that the script moves into the panel.",
                "The input holds the key of the option (`data-value`); with `data-multiple` it holds the keys joined by `data-sep` (default a comma and a space), so a field can change from one kind to the other without touching what it saved.",
                "`data-search` adds a search field to the panel (on a phone it is not focused at once, so the keyboard does not cover the options); `data-keywords` on an option adds words that the search finds but the list does not show.",
                "The label of the field names the button, and the button says what is chosen. On a phone the panel is full screen with an X at the top right (DIALOG-03).",
                "Create (FORM-16): `data-create-url` on the wrapper. The first row is Create and the typed text, while that is not the name of an option; choosing it posts the name and takes `{ value, name, description }` back. Only where creating is cheap and needs nothing but a name.",
                "A refusal (not 2xx, or no answer) keeps the panel open and writes the `error` of the answer, or a default text, under the search field, read aloud. While it runs the row is busy (`aria-busy`) and cannot be pressed twice; a success is announced in a live region.",
                "Suggestions (FORM-16): `data-suggest-url` is fetched with GET when the panel opens first with nothing chosen. They stand under Suggestions above All, never while searching or once a value is chosen; the closed field says \"3 suggestions\" in the colour of a link.",
                "A suggested value is that option; a suggested name that is not an option creates it (it needs `data-create-url`). From code: `MatUi.picker.set(wrapper, value)`, `.addOption(wrapper, { value, name, description, tags })` (made with `textContent`) and `.suggest(wrapper, items)`.",
            ]),

        new("date-picker", "Date and period", "Forms", "calendar",
            "A date or a period from a calendar: the language of the page for what is shown, one fixed format (2026-10-10) for what is posted. A period shows two months on a wide screen.",
            ["FORM-15", "DIALOG-03", "A11Y-02", "FORM-12"],
            [
                new("A date", """
                    <div class="mat-form__grid">
                      <div class="mat-field">
                        <label class="mat-label" for="pk-date">Due date</label>
                        <div class="mat-picker" data-picker="date" data-clearable data-min="2026-10-05" data-max="2026-12-31" data-placeholder="Choose a date">
                          <input class="mat-picker__input" id="pk-date" name="due" value="2026-10-10" />
                        </div>
                        <span class="mat-hint">From 5 October to 31 December 2026.</span>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="pk-birth">Date of birth (optional)</label>
                        <div class="mat-picker" data-picker="date" data-clearable data-placeholder="Choose a date">
                          <input class="mat-picker__input" id="pk-birth" name="birth" value="" />
                        </div>
                      </div>
                    </div>
                    """, "A click on a day chooses it and closes the panel. The arrow keys move by day and week, Home and End to the start and end of the week, Page up and down by month (with Shift by year); Enter chooses."),
                new("A period", """
                    <div class="mat-field">
                      <label class="mat-label" for="pk-period">Period</label>
                      <div class="mat-picker" data-picker="daterange" data-clearable data-placeholder="Choose a period">
                        <input class="mat-picker__input" id="pk-period" name="period" value="2026-10-12..2026-10-16" />
                      </div>
                      <span class="mat-hint">Posted as <code>2026-10-12..2026-10-16</code>.</span>
                    </div>
                    """, "The first click is the start, the second the end (a day before the start begins a new period); while the start is chosen the days up to the pointer are marked. The period is complete with its second day."),
                new("A period with flexible dates", """
                    <div class="mat-field">
                      <label class="mat-label" for="pk-flex">Stay</label>
                      <div class="mat-picker" data-picker="daterange" data-flex="1,2,3,7" data-placeholder="Choose the dates">
                        <input class="mat-picker__input" id="pk-flex" name="stay" value="2026-11-02..2026-11-06~2" />
                      </div>
                      <span class="mat-hint">Posted as <code>2026-11-02..2026-11-06~2</code>: plus or minus two days.</span>
                    </div>
                    """, "With `data-flex` (the numbers of days) the panel offers exact dates or plus or minus some days, and a period is applied with the button, because a choice of flexibility can follow the second day."),
            ],
            [
                "The field posts `2026-10-10` for a date and `2026-10-10..2026-10-14` for a period (with `~2` for the flexible days), whatever the language of the page. What the button shows follows `lang` of the page (or `data-locale` on `html`): 10 Oct 2026, 10.10.2026.",
                "`data-min` and `data-max` (a date each) disable the days outside them; `data-week-start` (0 Sunday to 6, default 1 for Monday) is read from the wrapper or from `html`.",
                "Each day has its full date as its name for a screen reader, the days are a grid that the arrow keys walk, and the months have a live title that is read when they change. On a phone the panel is full screen and shows one month.",
                "A date in a table filter or a form that only needs the phone's own picker can stay `input type=\"date\"`; the picker is for what the native control cannot do (a period, limits, the same look everywhere).",
            ]),

        new("time-picker", "Time and time range", "Forms", "clock",
            "A time of day or a time range: columns of hours and minutes to choose from, a field to type in, and the 12 or 24 hours of the language. The field posts 09:30, always 24 hours.",
            ["FORM-15", "DIALOG-03", "A11Y-02", "FORM-12"],
            [
                new("A time", """
                    <div class="mat-form__grid">
                      <div class="mat-field">
                        <label class="mat-label" for="pk-time">Start</label>
                        <div class="mat-picker" data-picker="time" data-step="15" data-clearable data-placeholder="Choose a time">
                          <input class="mat-picker__input" id="pk-time" name="start" value="09:30" />
                        </div>
                        <span class="mat-hint">In steps of 15 minutes.</span>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="pk-time12">Reminder</label>
                        <div class="mat-picker" data-picker="time" data-hour12="true" data-placeholder="Choose a time">
                          <input class="mat-picker__input" id="pk-time12" name="reminder" value="14:45" />
                        </div>
                        <span class="mat-hint">With 12 hours: AM and PM column; still posted as <code>14:45</code>.</span>
                      </div>
                    </div>
                    """, "Type a time (930, 9:30, 09.30, 9:30 pm) and press Enter, or choose an hour and a minute and apply. The arrow keys move in a column and the choice follows them; Now takes the time of day."),
                new("A time range", """
                    <div class="mat-field">
                      <label class="mat-label" for="pk-hours">Opening hours</label>
                      <div class="mat-picker" data-picker="timerange" data-step="30" data-clearable data-placeholder="Choose the hours">
                        <input class="mat-picker__input" id="pk-hours" name="hours" value="09:00..17:30" />
                      </div>
                      <span class="mat-hint">Posted as <code>09:00..17:30</code>.</span>
                    </div>
                    """, "The panel switches between From and To and says how long the range is; an end before the start is the next day (a night shift) and the panel says so."),
            ],
            [
                "The format of what is shown follows the language (`data-hour12=\"true|false\"` decides it for one field); what is posted is always `HH:MM` or `HH:MM..HH:MM`.",
                "`data-step` is the distance of the minutes (default 5). A time that is between the steps stays exactly as it is and is shown in its column.",
                "On a wide screen the field to type in has the focus when the panel opens; on a phone the chosen hour has it, so the keyboard does not cover the columns. Enter in the field takes the time and applies it.",
            ]),

        new("colour-picker", "Colour", "Forms", "palette",
            "A colour from a set of swatches or freely: an area for saturation and brightness, a hue slider and a hex value, with the text colour that reads best on it and how well.",
            ["FORM-15", "COLOR-04", "DIALOG-03", "A11Y-02"],
            [
                new("Swatches and the free choice", """
                    <div class="mat-field">
                      <label class="mat-label" for="pk-colour">Accent</label>
                      <div class="mat-picker" data-picker="colour" data-clearable data-placeholder="Choose a colour">
                        <input class="mat-picker__input" id="pk-colour" name="accent" value="#b4235f" />
                      </div>
                      <span class="mat-hint">Posted as <code>#b4235f</code>.</span>
                    </div>
                    """, "A swatch is chosen with one click. The area, the hue and the hex value are tried first and applied with the button; the line under them tells whether white or dark text reads better on the colour (AA is 4.5 to 1)."),
                new("Your own swatches", """
                    <div class="mat-field">
                      <label class="mat-label" for="pk-brand">Brand colour</label>
                      <div class="mat-picker" data-picker="colour" data-swatches="#5b5fe6:MatDock,#0d9488:MatMail,#c2410c:MatFile,#7c3aed:MatPaper,#0284c7:Matmon" data-placeholder="Choose a colour">
                        <input class="mat-picker__input" id="pk-brand" name="brand" value="" />
                      </div>
                      <span class="mat-hint"><code>data-swatches</code> lists colours, each with a name after a colon.</span>
                    </div>
                    """),
            ],
            [
                "The field posts `#rrggbb` in lower case, without transparency. Typing in the hex field accepts three or six digits, with or without `#`.",
                "The area is a slider for the keyboard: left and right change the saturation, up and down the brightness (with Shift in tens); the hue is a slider too. The swatches are one group for the arrow keys.",
                "The name of a swatch is the name of the button; without a name it is the hex value. The colour is never the only information: the field shows the hex value next to the chip (COLOR-04).",
            ]),
    ];
}
