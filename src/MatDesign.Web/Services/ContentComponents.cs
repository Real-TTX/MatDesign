namespace MatDesign.Web.Services;

/// <summary>
/// More examples for a component that already exists (its id), appended to what it has. A different name or summary replaces the old one.
/// </summary>
internal sealed record ComponentExtension(string Id, IReadOnlyList<ComponentExample> Examples, IReadOnlyList<string> Behaviour, string? Name = null, string? Summary = null, IReadOnlyList<string>? Rules = null);

/// <summary>
/// The rest of the controls a framework is expected to have: button groups and dropdowns, accordion, list group, avatars and indicators, stats and
/// timeline, text and pictures, spinners, popovers; and more examples for the progress bar, the table, cards, tabs, dialogs, skeletons and alerts.
/// </summary>
internal static class ContentComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("button-group", "Button groups", "Actions", "toggle",
            "Buttons that belong together share one border: a choice of views, a few exclusive toggles, a split button. Several groups make a toolbar.",
            ["BUTTON-01", "BUTTON-05", "A11Y-02"],
            [
                new("A choice of views", $"""
                    <div class="mat-btn-group" role="group" aria-label="View">
                      <button type="button" class="mat-btn mat-btn--secondary is-active" aria-pressed="true">{I("list")}<span>List</span></button>
                      <button type="button" class="mat-btn mat-btn--secondary" aria-pressed="false">{I("grid")}<span>Gallery</span></button>
                      <button type="button" class="mat-btn mat-btn--secondary" aria-pressed="false">{I("table")}<span>Table</span></button>
                    </div>
                    """, "The current button is is-active and aria-pressed. The script of the page moves it; for the list and gallery switch of a list use data-view-switch."),
                new("A toolbar of groups", $"""
                    <div class="mat-btn-toolbar" role="toolbar" aria-label="Text">
                      <div class="mat-btn-group" role="group" aria-label="Alignment">
                        <button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Align left">{I("menu")}</button>
                        <button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Align centre">{I("sort")}</button>
                      </div>
                      <div class="mat-btn-group" role="group" aria-label="Actions">
                        <button type="button" class="mat-btn mat-btn--secondary" disabled>Undo</button>
                        <button type="button" class="mat-btn mat-btn--secondary">Redo</button>
                      </div>
                      <button type="button" class="mat-btn mat-btn--primary">{I("save")}<span>Save</span></button>
                    </div>
                    """),
                new("Toggles in a group", """
                    <div class="mat-btn-group" role="radiogroup" aria-label="Period">
                      <input type="radio" class="mat-btn-check" id="bg-day" name="bg-period" value="day" /><label class="mat-btn mat-btn--secondary" for="bg-day">Day</label>
                      <input type="radio" class="mat-btn-check" id="bg-week" name="bg-period" value="week" checked /><label class="mat-btn mat-btn--secondary" for="bg-week">Week</label>
                      <input type="radio" class="mat-btn-check" id="bg-month" name="bg-period" value="month" /><label class="mat-btn mat-btn--secondary" for="bg-month">Month</label>
                    </div>
                    """, "Radios drawn as buttons: the group works with the arrow keys and sends its value with the form."),
                new("Vertical", """
                    <div class="mat-row">
                      <div class="mat-btn-group mat-btn-group--vertical" role="group" aria-label="Order">
                        <button type="button" class="mat-btn mat-btn--secondary">Move up</button>
                        <button type="button" class="mat-btn mat-btn--secondary">Move down</button>
                        <button type="button" class="mat-btn mat-btn--secondary">Remove</button>
                      </div>
                    </div>
                    """),
            ],
            [
                "A group has a role and a name (`role=\"group\"`, `aria-label`), so a screen reader says what the buttons are for.",
                "Only one button of a group is primary, and a group is never mixed with the button row of a form: that one is under the content.",
            ]),

        new("dropdown", "Dropdowns and split buttons", "Actions", "chevron-down",
            "A button that opens a menu next to it: the actions of one thing, the ways to save, the actions of a row.",
            ["SHELL-05", "DIALOG-07", "A11Y-02", "ICON-03"],
            [
                new("A button with a menu", $"""
                    <div class="mat-row"><button type="button" class="mat-btn mat-btn--secondary" data-menu-toggle="dd-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false"><span>Actions</span>{I("chevron-down", "mat-btn__caret")}</button></div>
                    <div class="mat-menu" id="dd-menu" role="menu" aria-label="Actions" hidden>
                      <div class="mat-menu__head"><div><strong>Quarterly report</strong><small>Finance · 412 KB</small></div></div>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("eye")}<span>Open</span></a>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("download")}<span>Download</span></a>
                      <div class="mat-menu__sep" role="separator"></div>
                      <a class="mat-menu__item mat-menu__item--danger" role="menuitem" href="#">{I("trash")}<span>Delete</span></a>
                    </div>
                    """, "Arrow keys move in the menu, Escape closes it and returns to the button; a click outside closes it. The destructive item is last, behind a separator."),
                new("A split button", $"""
                    <div class="mat-btn-group" role="group" aria-label="Save">
                      <button type="button" class="mat-btn mat-btn--primary">{I("save")}<span>Save</span></button>
                      <button type="button" class="mat-btn mat-btn--primary mat-btn--icon" data-menu-toggle="dd-split" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false" aria-label="More ways to save">{I("chevron-down")}</button>
                    </div>
                    <div class="mat-menu" id="dd-split" role="menu" aria-label="More ways to save" hidden>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("copy")}<span>Save as a copy</span></a>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("archive")}<span>Save and archive</span></a>
                    </div>
                    """, "The main part does the usual thing at once; the caret offers the others."),
                new("The actions of a row", $"""
                    <div class="mat-row mat-row--between">
                      <span>Insurance policy</span>
                      <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" data-menu-toggle="dd-row" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false" aria-label="Actions for Insurance policy" data-tip="Actions">{I("more-horizontal")}</button>
                    </div>
                    <div class="mat-menu" id="dd-row" role="menu" aria-label="Actions for Insurance policy" hidden>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("edit")}<span>Edit</span></a>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("copy")}<span>Duplicate</span></a>
                    </div>
                    """, "Few actions of a row are icon buttons in the row; the rest goes into a menu. Delete lives on the edit page (LIST-08)."),
            ],
            [
                "The button has `aria-haspopup=\"menu\"` and `aria-expanded`, and an icon-only trigger has an `aria-label` that names the thing it acts on.",
                "`data-menu-place=\"bottom\"` opens the menu below the button. Without it the menu opens above the button (as the user menu at the foot of the shell must) and only goes below when there is no room above.",
                "On a phone the menu stays a small menu next to the button; it is a menu, not a form, so it is not a full-screen dialog (DIALOG-07).",
            ]),

        new("accordion", "Accordion and collapse", "Content", "layers",
            "Sections that open and close: questions and answers, advanced settings. Native details elements: they work without the script.",
            ["A11Y-02", "FORM-10", "MOBILE-04"],
            [
                new("One open at a time", $"""
                    <div class="mat-accordion">
                      <details class="mat-accordion__item" name="acc-faq" open>
                        <summary>How do I install an app?{I("chevron-down", "mat-accordion__chev")}</summary>
                        <div class="mat-accordion__body"><p>Open the store, choose the app and press Install. Passwords are generated for you.</p></div>
                      </details>
                      <details class="mat-accordion__item" name="acc-faq">
                        <summary>Where are my backups?{I("chevron-down", "mat-accordion__chev")}</summary>
                        <div class="mat-accordion__body"><p>In the Backups section: every app has its own schedule and retention.</p></div>
                      </details>
                      <details class="mat-accordion__item" name="acc-faq">
                        <summary>Can I change the colour of an app?{I("chevron-down", "mat-accordion__chev")}</summary>
                        <div class="mat-accordion__body"><p>Yes, in your account settings: choose one of the accents or your own.</p></div>
                      </details>
                    </div>
                    """, "Items with the same name attribute close each other. The summary is the button: Enter and Space open it."),
                new("Several open at once", $"""
                    <div class="mat-accordion">
                      <details class="mat-accordion__item"><summary>Network{I("chevron-down", "mat-accordion__chev")}</summary><div class="mat-accordion__body"><p>Ports, domains and the proxy.</p></div></details>
                      <details class="mat-accordion__item" open><summary>Storage{I("chevron-down", "mat-accordion__chev")}</summary><div class="mat-accordion__body"><p>Volumes and backups.</p></div></details>
                    </div>
                    """, "Without a name every item opens and closes on its own."),
                new("A single collapse", $"""
                    <details class="mat-collapse">
                      <summary>Advanced settings{I("chevron-down", "mat-accordion__chev")}</summary>
                      <div class="mat-accordion__body">
                        <div class="mat-field"><label class="mat-label" for="acc-tz">Time zone</label><input class="mat-input" id="acc-tz" name="tz" value="Europe/Berlin" /></div>
                      </div>
                    </details>
                    """, "For fields that most people never need (FORM-04 says to show them only when they are needed; this is the other way to keep a form short)."),
            ],
            [
                "The chevron is decoration: the summary text says what opens. Content inside a closed item is not in the tab order.",
                "Do not hide what must be seen (an error, a required field) in a closed item.",
            ]),

        new("list-group", "List group", "Content", "list",
            "Rows of one list as links, buttons or plain text, with an icon, a second line, a badge or a check; the current row marked.",
            ["TABLE-03", "COLOR-04", "A11Y-02", "MOBILE-04"],
            [
                new("Links with badges", $"""
                    <nav aria-label="Folders">
                      <ul class="mat-listgroup">
                        <li><a class="mat-listgroup__item is-active" href="#" aria-current="true">{I("mail")}<span class="mat-listgroup__text">Inbox</span><span class="mat-badge">12<span class="mat-sr-only"> unread</span></span></a></li>
                        <li><a class="mat-listgroup__item" href="#">{I("save")}<span class="mat-listgroup__text">Drafts</span><span class="mat-badge mat-badge--soft">2<span class="mat-sr-only"> drafts</span></span></a></li>
                        <li><a class="mat-listgroup__item" href="#">{I("archive")}<span class="mat-listgroup__text">Archive</span></a></li>
                        <li><a class="mat-listgroup__item is-disabled" href="#" aria-disabled="true" tabindex="-1">{I("lock")}<span class="mat-listgroup__text">Shared (no access)</span></a></li>
                      </ul>
                    </nav>
                    """, "The current row is marked with is-active and aria-current. A disabled row says so with aria-disabled and is taken out of the tab order."),
                new("Two lines and an action", $"""
                    <ul class="mat-listgroup">
                      <li class="mat-listgroup__item"><span class="mat-avatar" aria-hidden="true">AM</span><span class="mat-listgroup__text"><strong>Alex Morgan</strong><small>Administrator · last seen today</small></span><button type="button" class="mat-btn mat-btn--ghost mat-btn--sm">Edit</button></li>
                      <li class="mat-listgroup__item"><span class="mat-avatar" aria-hidden="true">JD</span><span class="mat-listgroup__text"><strong>Jamie Doe</strong><small>User · invited</small></span><span class="mat-pill mat-pill--warn">Pending</span></li>
                    </ul>
                    """),
                new("Numbered, and with checks", """
                    <div class="mat-grid">
                      <ol class="mat-listgroup mat-listgroup--numbered">
                        <li class="mat-listgroup__item">Install the app</li>
                        <li class="mat-listgroup__item">Open the setup wizard</li>
                        <li class="mat-listgroup__item">Choose a domain</li>
                      </ol>
                      <ul class="mat-listgroup">
                        <li class="mat-listgroup__item"><label class="mat-check"><input type="checkbox" id="lg-1" name="lg" checked /><span class="mat-check__text">Mail</span></label></li>
                        <li class="mat-listgroup__item"><label class="mat-check"><input type="checkbox" id="lg-2" name="lg" /><span class="mat-check__text">Files</span></label></li>
                        <li class="mat-listgroup__item"><label class="mat-check"><input type="checkbox" id="lg-3" name="lg" /><span class="mat-check__text">Calendar</span></label></li>
                      </ul>
                    </div>
                    """),
            ],
            [
                "A list group is for a few rows with one thing to do each. Many rows with several columns are a table, and a table has the toolbar above it (LIST-02).",
                "`mat-listgroup--flush` removes the border and the side padding to put a list group inside a card.",
            ]),

        new("avatars", "Avatars and indicators", "Content", "user-circle",
            "A person or a thing as a round picture or initials, with a state; a count or a dot on a button or an avatar.",
            ["COLOR-04", "COLOR-03", "SHELL-14", "ICON-03"],
            [
                new("Avatars and their state", """
                    <div class="mat-row">
                      <span class="mat-avatar mat-avatar--sm" role="img" aria-label="Alex Morgan">AM</span>
                      <span class="mat-avatar" role="img" aria-label="Alex Morgan">AM</span>
                      <span class="mat-avatar mat-avatar--lg" role="img" aria-label="Alex Morgan">AM</span>
                      <span class="mat-avatar mat-avatar--xl" role="img" aria-label="Alex Morgan">AM</span>
                      <span class="mat-avatar" role="img" aria-label="Jamie Doe, online">JD<span class="mat-avatar__status mat-avatar__status--ok"></span></span>
                      <span class="mat-avatar" role="img" aria-label="Sam Lee, away">SL<span class="mat-avatar__status mat-avatar__status--warn"></span></span>
                      <span class="mat-avatar" role="img" aria-label="Kim Park, offline">KP<span class="mat-avatar__status"></span></span>
                    </div>
                    """, "The state is in the name (\"online\"), not only in the colour of the dot (COLOR-04). A picture goes inside the avatar as an img."),
                new("A group", """
                    <span class="mat-avatars" role="group" aria-label="Members: Alex Morgan, Jamie Doe, Sam Lee and 3 more">
                      <span class="mat-avatar" aria-hidden="true">AM</span>
                      <span class="mat-avatar" aria-hidden="true">JD</span>
                      <span class="mat-avatar" aria-hidden="true">SL</span>
                      <span class="mat-avatar" aria-hidden="true">+3</span>
                    </span>
                    """),
                new("A count or a dot on something", $"""
                    <div class="mat-row">
                      <span class="mat-indicator"><button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Notifications, 3 new">{I("bell")}</button><span class="mat-badge mat-badge--error" aria-hidden="true">3</span></span>
                      <span class="mat-indicator"><button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Mail, something new">{I("mail")}</button><span class="mat-badge mat-badge--dot" aria-hidden="true"></span></span>
                      <span class="mat-badge">9</span>
                      <span class="mat-badge mat-badge--ok">ok</span>
                      <span class="mat-badge mat-badge--warn">!</span>
                      <span class="mat-badge mat-badge--error">2</span>
                      <span class="mat-badge mat-badge--info">i</span>
                    </div>
                    """, "The button's label says the count in words (the badge itself is hidden from screen readers). The tones are the status colours: amber is something to look at, red something that failed."),
            ],
            [
                "An avatar with initials has `role=\"img\"` and the name as `aria-label`; an avatar next to the name in text is decoration (`aria-hidden`).",
                "In the menu the same badges are `mat-nav__badge` (see Menu items, badges and submenus).",
            ]),

        new("stat", "Stats and timeline", "Content", "bar-chart",
            "One number that matters with its label and change; what happened, newest first.",
            ["COLOR-04", "COLOR-03", "TYPE-03", "A11Y-03"],
            [
                new("Stats in cards", $"""
                    <div class="mat-grid mat-grid--4">
                      <div class="mat-card"><div class="mat-card__body mat-stat"><span class="mat-stat__label">Items</span><span class="mat-stat__value">128</span><span class="mat-stat__delta mat-stat__delta--up">{I("arrow-up")}<span>6 more than last week</span></span></div></div>
                      <div class="mat-card"><div class="mat-card__body mat-stat"><span class="mat-stat__label">To review</span><span class="mat-stat__value">7</span><span class="mat-stat__delta">3 older than a week</span></div></div>
                      <div class="mat-card"><div class="mat-card__body mat-stat"><span class="mat-stat__label">Failed</span><span class="mat-stat__value">2</span><span class="mat-stat__delta mat-stat__delta--down">{I("arrow-down")}<span>1 less than yesterday</span></span></div></div>
                      <div class="mat-card"><div class="mat-card__body mat-stat"><span class="mat-stat__label">Storage</span><span class="mat-stat__value">63 %</span><span class="mat-stat__delta">of 2 TB</span></div></div>
                    </div>
                    """, "The change is written (an arrow and words): the colour only supports it."),
                new("A timeline", """
                    <ol class="mat-timeline">
                      <li class="mat-timeline__ok"><span class="mat-timeline__time">Today, 09:41</span><strong>Backup finished</strong> in 14 seconds.</li>
                      <li class="mat-timeline__warn"><span class="mat-timeline__time">Today, 08:02</span><strong>Disk almost full</strong>: 92 % used.</li>
                      <li class="mat-timeline__error"><span class="mat-timeline__time">Yesterday, 23:15</span><strong>Mail server did not answer</strong> after three tries.</li>
                      <li><span class="mat-timeline__time">Yesterday, 18:30</span>Alex Morgan updated the settings.</li>
                    </ol>
                    """, "An ordered list: the screen reader counts the entries. The time is mono, the colour of the dot is the status."),
            ],
            [
                "A number is in a tabular style (`font-variant-numeric`), so numbers in a row line up.",
                "A dashboard has few stats: the ones somebody acts on.",
            ]),

        new("typography", "Text and pictures", "Content", "type",
            "Running text with its lists, quotes and marks, a lead paragraph; pictures with a caption, in a fixed ratio, as a thumbnail or round.",
            ["TYPE-02", "TYPE-03", "TYPE-04", "A11Y-03"],
            [
                new("Running text", """
                    <div class="mat-prose">
                      <p class="mat-lead">A lead paragraph says in a sentence what the page is about.</p>
                      <p>Running text is 15 px, at most about 70 characters wide, with <a href="#">a link</a>, <strong>strong</strong> and <em>emphasised</em> words, <code>code</code>, <mark>a marked passage</mark> and <kbd>/</kbd> for a key. <small>Small print is 13 px.</small></p>
                      <ul><li>A list has a marker and room between its items.</li><li>Several lines of one item wrap under the text, not under the marker.</li></ul>
                      <blockquote class="mat-blockquote"><p>A quote has the accent at its side.</p><footer>Matthias, a note in the rulebook</footer></blockquote>
                      <hr />
                      <dl class="mat-kv"><dt>Version</dt><dd class="mat-mono">1.4.212-20261010</dd><dt>Port</dt><dd class="mat-mono">8060</dd></dl>
                    </div>
                    """, "mat-prose spaces what is inside it; headings are the usual h2 to h4 and follow the order of the page."),
                new("Pictures", """
                    <div class="mat-row">
                      <figure class="mat-figure"><img class="mat-img mat-img--rounded" src="/favicon.svg" alt="The MatDesign logo" width="96" height="96" /><figcaption>With a caption</figcaption></figure>
                      <figure class="mat-figure"><img class="mat-img mat-img--thumb" src="/favicon.svg" alt="The MatDesign logo as a thumbnail" width="96" height="96" /><figcaption>Thumbnail</figcaption></figure>
                      <figure class="mat-figure"><img class="mat-img mat-img--circle" src="/favicon.svg" alt="The MatDesign logo, round" width="96" height="96" /><figcaption>Round</figcaption></figure>
                    </div>
                    """, "An image has its size in width and height (the page does not jump while it loads) and an alt text, or an empty alt when it only decorates."),
                new("A fixed ratio", """
                    <div class="mat-grid mat-grid--3">
                      <figure class="mat-figure"><div class="mat-ratio"><img src="/favicon.svg" alt="The MatDesign logo in a 16 to 9 frame" /></div><figcaption>16:9 (the default)</figcaption></figure>
                      <figure class="mat-figure"><div class="mat-ratio mat-ratio--4x3"><img src="/favicon.svg" alt="The MatDesign logo in a 4 to 3 frame" /></div><figcaption>4:3</figcaption></figure>
                      <figure class="mat-figure"><div class="mat-ratio mat-ratio--1x1"><img src="/favicon.svg" alt="The MatDesign logo in a square frame" /></div><figcaption>1:1</figcaption></figure>
                    </div>
                    """, "mat-ratio keeps the ratio whatever the width and crops the picture to fill it (object-fit: cover), so the layout does not jump while it loads."),
            ],
            [
                "An image is sized by its container (`max-width: 100%`); no image makes a phone scroll sideways.",
                "A picture that carries information has an `alt` that says it; a decorative one has `alt=\"\"`.",
            ]),

        new("spinner", "Spinners and loading", "Feedback", "refresh",
            "Something is loading and nothing else can be shown yet. A spinner always comes with a text.",
            ["A11Y-03", "A11Y-05", "BUTTON-03"],
            [
                new("Spinners", """
                    <div class="mat-row">
                      <span class="mat-spinner mat-spinner--sm" role="status"><span class="mat-sr-only">Loading</span></span>
                      <span class="mat-spinner" role="status"><span class="mat-sr-only">Loading</span></span>
                      <span class="mat-spinner mat-spinner--lg" role="status"><span class="mat-sr-only">Loading</span></span>
                    </div>
                    """, "role=\"status\" and a hidden text: a screen reader says that something is loading."),
                new("A loading area and a loading button", $"""
                    <div class="mat-stack">
                      <div class="mat-card"><div class="mat-loading" role="status"><span class="mat-spinner"></span><span>Loading the backups …</span></div></div>
                      <div class="mat-row"><button type="button" class="mat-btn mat-btn--primary is-loading" aria-busy="true"><span>Save</span></button><button type="button" class="mat-btn mat-btn--secondary" disabled>Back</button></div>
                    </div>
                    """, "A button that waits for the server is is-loading and aria-busy: it shows a spinner instead of its label and cannot be pressed again."),
            ],
            [
                "With reduced motion the spinner turns slowly instead of stopping: a spinner that does not move says nothing is happening.",
                "For content that has a shape (a list, a card) use a skeleton: it does not make the page jump when the content arrives.",
            ]),

        new("popover", "Popover", "Feedback", "help-circle",
            "More than a tooltip: a short explanation or a few actions in a box next to its button. It opens on a click, never on hover.",
            ["A11Y-02", "MOBILE-04", "DIALOG-07"],
            [
                new("An explanation", $"""
                    <div class="mat-row"><button type="button" class="mat-btn mat-btn--secondary" data-menu-toggle="pop-help" data-menu-place="bottom" aria-haspopup="dialog" aria-expanded="false">{I("help-circle")}<span>What is retention?</span></button></div>
                    <div class="mat-menu mat-popover" id="pop-help" role="dialog" aria-label="About retention" hidden>
                      <div class="mat-popover__title">Retention</div>
                      <p>Backups that are older than this are deleted after the next backup has finished. The newest backup is never deleted.</p>
                      <a href="#">Read more</a>
                    </div>
                    """, "It is the same box as a menu (it is placed next to its button and stays on the screen); Escape and a click outside close it."),
            ],
            [
                "Everything in a popover can also be reached on a phone: it opens on a tap. Nothing may live only in a tooltip (they do not exist on touch).",
                "A popover that needs a decision or a form is a dialog.",
            ]),
    ];

    public static IReadOnlyList<ComponentExtension> Extensions { get; } =
    [
        new("meter", [
                new("Sizes and a value", """
                    <div class="mat-stack">
                      <div class="mat-row mat-row--between"><span>Small</span><span class="mat-mono">25 %</span></div>
                      <div class="mat-meter mat-meter--sm" role="progressbar" aria-valuenow="25" aria-valuemin="0" aria-valuemax="100" aria-label="Small"><div class="mat-meter__fill" data-value="25"></div></div>
                      <div class="mat-row mat-row--between"><span>Large</span><span class="mat-mono">75 %</span></div>
                      <div class="mat-meter mat-meter--lg" role="progressbar" aria-valuenow="75" aria-valuemin="0" aria-valuemax="100" aria-label="Large"><div class="mat-meter__fill" data-value="75"></div></div>
                    </div>
                    """, "The value is written next to the bar, so it can be read whatever the colour."),
                new("Tones", """
                    <div class="mat-stack">
                      <div class="mat-meter" role="progressbar" aria-valuenow="40" aria-valuemin="0" aria-valuemax="100" aria-label="Storage, fine"><div class="mat-meter__fill" data-value="40" data-tone="ok"></div></div>
                      <div class="mat-meter" role="progressbar" aria-valuenow="78" aria-valuemin="0" aria-valuemax="100" aria-label="Storage, getting full"><div class="mat-meter__fill" data-value="78" data-tone="warn"></div></div>
                      <div class="mat-meter" role="progressbar" aria-valuenow="96" aria-valuemin="0" aria-valuemax="100" aria-label="Storage, almost full"><div class="mat-meter__fill" data-value="96" data-tone="error"></div></div>
                    </div>
                    """, "A tone only when the value means something (a full disk); otherwise the accent."),
                new("Striped, animated and of unknown length", """
                    <div class="mat-stack">
                      <div class="mat-meter mat-meter--lg mat-meter--striped" role="progressbar" aria-valuenow="60" aria-valuemin="0" aria-valuemax="100" aria-label="Striped"><div class="mat-meter__fill" data-value="60"></div></div>
                      <div class="mat-meter mat-meter--lg mat-meter--striped mat-meter--animated" role="progressbar" aria-valuenow="45" aria-valuemin="0" aria-valuemax="100" aria-label="Uploading"><div class="mat-meter__fill" data-value="45"></div></div>
                      <div class="mat-meter mat-meter--indeterminate" role="progressbar" aria-label="Waiting for the server"><div class="mat-meter__fill"></div></div>
                    </div>
                    """, "An indeterminate bar has no aria-valuenow: nobody knows how far it is."),
                new("Several parts", """
                    <div class="mat-stack">
                      <div class="mat-meter mat-meter--lg mat-meter--stack" role="img" aria-label="Storage: 40 % documents, 25 % photos, 15 % backups, 20 % free">
                        <div class="mat-meter__fill" data-value="40"></div>
                        <div class="mat-meter__fill" data-value="25" data-tone="ok"></div>
                        <div class="mat-meter__fill" data-value="15" data-tone="warn"></div>
                      </div>
                      <div class="mat-row"><span class="mat-pill mat-pill--accent">Documents 40 %</span><span class="mat-pill mat-pill--ok">Photos 25 %</span><span class="mat-pill mat-pill--warn">Backups 15 %</span><span class="mat-pill mat-pill--plain">Free 20 %</span></div>
                    </div>
                    """, "The parts are told in words under the bar: the colours alone would not do (COLOR-04)."),
            ],
            [
                "The width of a bar is set from `data-value` by the script, so the markup carries no style attribute.",
                "A bar that shows a task has `role=\"progressbar\"` and a name; a bar that shows a share of something may be an `img` with the numbers in its label.",
            ],
            Name: "Progress and meter",
            Summary: "A bar for a share of something or for the progress of a task: sizes, tones, stripes, unknown length and several parts. The width comes from data-value."),

        new("table", [
                new("Variants", """
                    <div class="mat-stack">
                      <div class="mat-table-wrap">
                        <table class="mat-table mat-table--compact mat-table--bordered">
                          <caption class="mat-sr-only">A compact table with lines between the cells</caption>
                          <thead><tr><th scope="col">Service</th><th scope="col">State</th><th scope="col" class="mat-num">Latency</th></tr></thead>
                          <tbody>
                            <tr><td data-label="Service">Importer</td><td data-label="State"><span class="mat-pill mat-pill--ok">Online</span></td><td data-label="Latency" class="mat-num mat-mono">12 ms</td></tr>
                            <tr><td data-label="Service">Search index</td><td data-label="State"><span class="mat-pill mat-pill--warn">Degraded</span></td><td data-label="Latency" class="mat-num mat-mono">480 ms</td></tr>
                            <tr><td data-label="Service">Mail</td><td data-label="State"><span class="mat-pill mat-pill--error">Offline</span></td><td data-label="Latency" class="mat-num mat-mono">-</td></tr>
                          </tbody>
                        </table>
                      </div>
                    </div>
                    """, "mat-table--compact (less padding), mat-table--bordered (lines between the cells), mat-table--zebra (every other row tinted); a row is marked with is-selected."),
                new("Sortable columns and a header that stays", $"""
                    <div class="mat-table-wrap mat-table-wrap--sticky">
                      <table class="mat-table">
                        <caption class="mat-sr-only">Backups, sortable</caption>
                        <thead><tr>
                          <th scope="col" aria-sort="ascending"><a class="mat-table__sort" href="#">Name{I("arrow-up")}</a></th>
                          <th scope="col" aria-sort="none"><a class="mat-table__sort" href="#">Date{I("arrow-up")}</a></th>
                          <th scope="col" class="mat-num" aria-sort="none"><a class="mat-table__sort" href="#">Size{I("arrow-up")}</a></th>
                        </tr></thead>
                        <tbody>
                          <tr><td data-label="Name">Documents</td><td data-label="Date" class="mat-mono">2026-09-28</td><td data-label="Size" class="mat-num mat-mono">412 MB</td></tr>
                          <tr><td data-label="Name">Family photos</td><td data-label="Date" class="mat-mono">2026-09-21</td><td data-label="Size" class="mat-num mat-mono">5.1 GB</td></tr>
                          <tr><td data-label="Name">Mail</td><td data-label="Date" class="mat-mono">2026-09-14</td><td data-label="Size" class="mat-num mat-mono">1.2 GB</td></tr>
                          <tr><td data-label="Name">Router</td><td data-label="Date" class="mat-mono">2026-09-02</td><td data-label="Size" class="mat-num mat-mono">14 MB</td></tr>
                          <tr><td data-label="Name">Server</td><td data-label="Date" class="mat-mono">2026-08-24</td><td data-label="Size" class="mat-num mat-mono">64 MB</td></tr>
                          <tr><td data-label="Name">Notes</td><td data-label="Date" class="mat-mono">2026-08-19</td><td data-label="Size" class="mat-num mat-mono">22 MB</td></tr>
                          <tr><td data-label="Name">Archive</td><td data-label="Date" class="mat-mono">2026-08-11</td><td data-label="Size" class="mat-num mat-mono">9.8 GB</td></tr>
                          <tr><td data-label="Name">Projects</td><td data-label="Date" class="mat-mono">2026-07-29</td><td data-label="Size" class="mat-num mat-mono">310 MB</td></tr>
                        </tbody>
                      </table>
                    </div>
                    """, "The heading of a sorted column has aria-sort and a link that sorts the other way (sorting is a request, so the result can be shared). mat-table-wrap--sticky gives the table a height and keeps its header in view."),
                new("Selectable rows with a bulk bar", """
                    <div class="mat-stack" data-selectable>
                      <div class="mat-bulk" data-bulk-bar hidden role="status">
                        <span class="mat-bulk__count"><strong data-bulk-count>0</strong> selected</span>
                        <button type="button" class="mat-btn mat-btn--secondary mat-btn--sm">Download</button>
                        <button type="button" class="mat-btn mat-btn--danger mat-btn--sm" data-confirm="Delete the selected backups? This cannot be undone." data-confirm-ok="Delete">Delete</button>
                      </div>
                      <div class="mat-table-wrap">
                        <table class="mat-table">
                          <caption class="mat-sr-only">Backups with a check per row</caption>
                          <thead><tr><th scope="col" class="mat-table__select"><input type="checkbox" id="tsel-all" data-select-all aria-label="Select all backups" /></th><th scope="col">Name</th><th scope="col" class="mat-num">Size</th></tr></thead>
                          <tbody>
                            <tr><td data-label="Select" class="mat-table__select"><input type="checkbox" id="tsel-1" data-select-row aria-label="Select Documents" /></td><td data-label="Name">Documents</td><td data-label="Size" class="mat-num mat-mono">412 MB</td></tr>
                            <tr><td data-label="Select" class="mat-table__select"><input type="checkbox" id="tsel-2" data-select-row aria-label="Select Family photos" /></td><td data-label="Name">Family photos</td><td data-label="Size" class="mat-num mat-mono">5.1 GB</td></tr>
                            <tr><td data-label="Select" class="mat-table__select"><input type="checkbox" id="tsel-3" data-select-row aria-label="Select Mail" /></td><td data-label="Name">Mail</td><td data-label="Size" class="mat-num mat-mono">1.2 GB</td></tr>
                          </tbody>
                        </table>
                      </div>
                    </div>
                    """, "Select a row: its check marks it and the bulk bar appears with the count; the check in the header selects all and shows the state in between. The bar's destructive action asks first (FORM-11)."),
            ],
            [
                "A bulk action works on the rows that are selected on this page; it says so when there are more pages (\"7 selected on this page\").",
                "Every check in a row has an accessible name that says which row (`aria-label=\"Select Documents\"`).",
                "Items that are not rows are selected the same way: a `.mat-tile` (Gallery) and any element with `data-select-item` (Cards) are marked `is-selected` when their check is ticked.",
            ]),

        new("cards", [
                new("A picture, or a picture beside the text", """
                    <div class="mat-grid">
                      <div class="mat-card"><img class="mat-card__media" src="/favicon.svg" alt="" /><div class="mat-card__body mat-stack mat-stack--sm"><strong>With a picture</strong><p class="mat-muted">The picture is decoration here, so its alt text is empty.</p></div></div>
                      <div class="mat-card mat-card--horizontal"><img class="mat-card__media" src="/favicon.svg" alt="" /><div class="mat-card__body mat-stack mat-stack--sm"><strong>Beside the text</strong><p class="mat-muted">Below 600 px the picture goes above.</p></div></div>
                    </div>
                    """),
                new("Selectable cards with a bulk bar", """
                    <div class="mat-stack" data-selectable>
                      <div class="mat-bulk" data-bulk-bar hidden role="status">
                        <span class="mat-bulk__count"><strong data-bulk-count>0</strong> selected</span>
                        <button type="button" class="mat-btn mat-btn--secondary mat-btn--sm">Archive</button>
                      </div>
                      <div class="mat-card" data-select-item>
                        <div class="mat-card__head"><label class="mat-check"><input type="checkbox" id="csel-1" data-select-row aria-label="Select Rent agreement" /></label><h3 class="mat-card__title">Rent agreement</h3><span class="mat-pill">Contract</span></div>
                        <div class="mat-card__body"><p class="mat-muted">Signed on 2026-03-01 and valid until 2029-02-28.</p></div>
                      </div>
                      <div class="mat-card" data-select-item>
                        <div class="mat-card__head"><label class="mat-check"><input type="checkbox" id="csel-2" data-select-row aria-label="Select Insurance policy" /></label><h3 class="mat-card__title">Insurance policy</h3><span class="mat-pill">Contract</span></div>
                        <div class="mat-card__body"><p class="mat-muted">Renews on 2027-01-01.</p></div>
                      </div>
                      <div class="mat-card" data-select-item>
                        <div class="mat-card__head"><label class="mat-check"><input type="checkbox" id="csel-3" data-select-row aria-label="Select Electricity bill" /></label><h3 class="mat-card__title">Electricity bill</h3><span class="mat-pill mat-pill--ok">Paid</span></div>
                        <div class="mat-card__body"><p class="mat-muted">September 2026, 86.40.</p></div>
                      </div>
                    </div>
                    """, "Cards, tiles and the rows of a list of your own are selected like the rows of a table (see Table): the check has data-select-row, and the item around it is marked is-selected, a tint behind the whole item and a line in the accent colour. A table row and a mat-tile are items by themselves; for anything else put data-select-item on the element that is the item."),
            ],
            [
                "The check of a card has an accessible name that says which card (`aria-label=\"Select Rent agreement\"`); a bulk action works on the cards that are selected on this page.",
                "Without `data-select-item` (and not a `tr` or a `.mat-tile`) nothing is marked, but the check still counts and shows the bar.",
            ]),

        new("tabs", [
                new("Pills, filling and vertical", """
                    <div class="mat-stack">
                      <nav class="mat-tabs mat-tabs--pills" aria-label="Period">
                        <a class="mat-tab is-active" href="#" aria-current="page">Day</a>
                        <a class="mat-tab" href="#">Week</a>
                        <a class="mat-tab" href="#">Month</a>
                      </nav>
                      <nav class="mat-tabs mat-tabs--fill" aria-label="Sections">
                        <a class="mat-tab is-active" href="#" aria-current="page">General</a>
                        <a class="mat-tab" href="#">Network</a>
                        <a class="mat-tab" href="#">Backup</a>
                      </nav>
                      <div data-tabs class="mat-row">
                        <div class="mat-tabs mat-tabs--vertical" role="tablist" aria-orientation="vertical" aria-label="Settings">
                          <button type="button" class="mat-tab" role="tab" id="vt-tab-a" aria-controls="vt-panel-a" aria-selected="true">Account</button>
                          <button type="button" class="mat-tab" role="tab" id="vt-tab-b" aria-controls="vt-panel-b" aria-selected="false" tabindex="-1">Security</button>
                        </div>
                        <div class="mat-tabpanel" role="tabpanel" id="vt-panel-a" aria-labelledby="vt-tab-a"><p>Account settings.</p></div>
                        <div class="mat-tabpanel" role="tabpanel" id="vt-panel-b" aria-labelledby="vt-tab-b" hidden><p>Security settings.</p></div>
                      </div>
                    </div>
                    """, "mat-tabs--pills for a switch between periods or filters, mat-tabs--fill to spread the tabs over the width, mat-tabs--vertical beside a panel on a wide screen."),
            ],
            []),

        new("dialogs", [
                new("A panel from the side", $"""
                    <button type="button" class="mat-btn mat-btn--secondary" data-dialog-open="dlg-side">{I("sliders")}<span>Details</span></button>
                    <dialog class="mat-dialog mat-dialog--end" id="dlg-side" aria-labelledby="dlg-side-title">
                      <div class="mat-dialog__form">
                        <div class="mat-dialog__head">{I("file-text")}<h2 class="mat-dialog__title" id="dlg-side-title">Quarterly report</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                        <div class="mat-dialog__body mat-stack">
                          <dl class="mat-kv"><dt>Category</dt><dd>Finance</dd><dt>Size</dt><dd class="mat-mono">412 KB</dd><dt>Date</dt><dd class="mat-mono">2026-09-28</dd></dl>
                        </div>
                        <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="button" class="mat-btn mat-btn--primary" data-dialog-close>Done</button></div></div>
                      </div>
                    </dialog>
                    """, "The same dialog, fixed to the right edge (mat-dialog--end) or the left (mat-dialog--start): for details beside a list. On a phone it is full screen with the X at the top right, like every dialog."),
            ],
            []),

        new("skeleton", [
                new("The shape of a row", """
                    <div class="mat-card"><div class="mat-card__body mat-stack" aria-busy="true">
                      <div class="mat-row"><span class="mat-skeleton mat-skeleton--circle"></span><div class="mat-stack mat-stack--sm"><span class="mat-skeleton mat-skeleton--w-50"></span><span class="mat-skeleton mat-skeleton--w-75"></span></div></div>
                      <span class="mat-skeleton mat-skeleton--block"></span>
                      <span class="mat-skeleton mat-skeleton--btn"></span>
                    </div></div>
                    """, "Circle, lines of 25, 50 and 75 % width, a block and a button: put them where the content will be."),
            ],
            []),

        new("alerts", [
                new("With a title, a link and actions", $"""
                    <div class="mat-alert mat-alert--warning">{I("alert")}<div class="mat-alert__body">
                      <span class="mat-alert__title">The backup of Documents has not run for 7 days</span>
                      <span>Check the <a href="#">schedule</a> or the free space on the target.</span>
                      <div class="mat-alert__actions"><button type="button" class="mat-btn mat-btn--secondary mat-btn--sm">Back up now</button><button type="button" class="mat-btn mat-btn--ghost mat-btn--sm">Remind me tomorrow</button></div>
                    </div></div>
                    """, "A title says it in a line; the text and the link say what to do; the actions are small buttons with the usual order (positive first)."),
            ],
            []),

        new("pills", [
                new("A colour a user chose, next to the name", """
                    <div class="mat-stack">
                      <div class="mat-row">
                        <span class="mat-tag" data-colour="#dc2626">Urgent</span>
                        <span class="mat-tag" data-colour="#2563eb">Finance</span>
                        <span class="mat-tag" data-colour="#16a34a">Done</span>
                        <span class="mat-tag" data-colour="#fff">White</span>
                        <span class="mat-tag" data-colour="#000000">Black</span>
                        <span class="mat-tag">No colour</span>
                      </div>
                      <div class="mat-row">
                        <span class="mat-row mat-row--nowrap mat-gap-2"><span class="mat-dot" data-colour="#d97706" aria-hidden="true"></span>Inbox</span>
                        <span class="mat-row mat-row--nowrap mat-gap-2"><span class="mat-dot" data-colour="#0d9488" aria-hidden="true"></span>Receipts</span>
                        <span class="mat-row mat-row--nowrap mat-gap-2"><span class="mat-dot" aria-hidden="true"></span>No colour: a grey dot</span>
                      </div>
                      <div class="mat-row">
                        <span class="mat-row mat-row--nowrap mat-gap-2"><svg class="mat-icon" data-colour="#7c3aed" aria-hidden="true" focusable="false"><use href="#mat-i-folder"></use></svg><strong>Project Alpha</strong></span>
                        <span class="mat-row mat-row--nowrap mat-gap-2"><svg class="mat-icon" data-colour="#db2777" aria-hidden="true" focusable="false"><use href="#mat-i-tag"></use></svg><strong>Invoices</strong></span>
                      </div>
                    </div>
                    """, "`data-colour=\"#rrggbb\"` (`#rgb` too, any case) is written into `--mat-swatch` by the script: no inline style. The text of a tag stays muted whatever the colour, and a ring keeps a white or a black dot visible. Anything that is not a hex colour is ignored: the neutral look."),
                new("Tags on the rows of a list", """
                    <div class="mat-table-wrap">
                      <table class="mat-table">
                        <caption class="mat-sr-only">Documents with their tags</caption>
                        <thead><tr><th scope="col">Name</th><th scope="col">Tags</th></tr></thead>
                        <tbody>
                          <tr><td data-label="Name">Invoice 2026-04</td><td data-label="Tags"><span class="mat-chips"><span class="mat-tag" data-colour="#2563eb">Finance</span><span class="mat-tag" data-colour="#dc2626">Urgent</span></span></td></tr>
                          <tr><td data-label="Name">Lease agreement</td><td data-label="Tags"><span class="mat-chips"><span class="mat-tag" data-colour="#0d9488">Contracts</span></span></td></tr>
                          <tr><td data-label="Name">Holiday photos</td><td data-label="Tags"><span class="mat-chips"><span class="mat-tag" data-colour="#f5f5f5">Family</span><span class="mat-tag">Archive</span></span></td></tr>
                        </tbody>
                      </table>
                    </div>
                    """, "The colour is a mark next to the name, never the only information (COLOR-04): even a colour that is hard to see, like the nearly white one, has its name beside it."),
            ],
            [
                "A colour a user chose is data (COLOR-10): `data-colour=\"#rrggbb\"` on `mat-tag` (a dot before the text), `mat-dot`, `mat-icon` (the icon in that colour) or an option of a select picker (a dot before the name). Never an inline style.",
                "The colour is a mark, never the colour of the text: the text of a tag stays muted and reaches AA, so any colour a user can pick is safe. A thin ring in the colour of the text keeps a white dot visible in light mode and a black one in dark mode.",
                "Markup that is added later (a row from a request) needs `MatUi.refresh(scope)`. A picker takes `colour` in `MatUi.picker.addOption(wrapper, { value, name, colour })`.",
            ],
            Rules: ["COLOR-10"]),
    ];
}
