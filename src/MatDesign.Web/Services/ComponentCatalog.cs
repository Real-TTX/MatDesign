namespace MatDesign.Web.Services;

/// <summary>
/// One example of a component. <see cref="Html"/> is rendered live and shown as source, so the example and what a developer copies can never
/// differ. <see cref="Js"/> is the script call that does the same (for dialogs and toasts); <see cref="Demo"/> adds a "Try it" button.
/// <see cref="Frame"/> names a look of the live area that is not part of the markup (a menu is shown in a box as wide as the menu).
/// </summary>
public sealed record ComponentExample(string Title, string Html, string? Note = null, string? Js = null, string? Demo = null, bool Live = true, string? Frame = null)
{
    /// <summary>The classes of the toolkit whose examples need the whole width: with them the code is shown below the example, not beside it.</summary>
    private static readonly string[] WideClasses =
    [
        "mat-table-wrap", "mat-toolbar", "mat-topbar", "mat-footer", "mat-with-toc", "mat-gallery", "mat-cols", "mat-grid", "mat-form__grid", "mat-auth", "mat-pager",
        "mat-tabs--vertical", "mat-nav ", "mat-switcher", "mat-carousel",
        "mat-schedule",
    ];

    /// <summary>
    /// True when the example is better with the code below it than beside it: it has a frame of its own that is wider than half a page (the bars), or it is a
    /// table, a bar, a grid or a page layout. The code is always shown; this only decides where.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Wide => Frame == "bar" || WideClasses.Any(c => Html.Contains(c, StringComparison.Ordinal));
}

/// <summary>A control of the toolkit as the component pages document it.</summary>
public sealed record ComponentDoc(
    string Id,
    string Name,
    string Group,
    string Icon,
    string Summary,
    IReadOnlyList<string> Rules,
    IReadOnlyList<ComponentExample> Examples,
    IReadOnlyList<string> Behaviour);

/// <summary>
/// A page of the documentation that shows some groups of the catalogue, each with its index ("On this page"): the components, the layout, the utilities.
/// </summary>
public sealed record CatalogSection(string Id, string Title, string Path, string Icon, string Sub, IReadOnlyList<string> Groups);

/// <summary>
/// The catalogue of controls. The markup is the markup of <c>MatUi</c> as it stands in the CSS and the script; a unit test parses every example
/// (cell labels, no inline styles, icons that exist, ids that are unique) so that the documentation cannot drift from the toolkit.
/// The layout classes and the utilities are in the catalogue too (the groups Layout and Utilities), so every rule that holds for an example holds for them.
/// </summary>
public static class ComponentCatalog
{
    public static readonly IReadOnlyList<string> Groups = ["Actions", "Forms", "Lists and data", "Content", "Navigation", "Feedback", "Layout", "Utilities"];

    /// <summary>The pages of the documentation of the toolkit and the groups each one shows.</summary>
    public static readonly IReadOnlyList<CatalogSection> Sections =
    [
        new("components", "Components", "/Components", "toggle",
            "Every control of the toolkit on one page: live, with the markup to copy and the rules it follows. Jump with the index.",
            ["Actions", "Forms", "Lists and data", "Content", "Navigation", "Feedback"]),
        new("layout", "Layout", "/Layout", "grid",
            "Breakpoints, containers and the grid of twelve columns, and the three small layouts that most pages are built from. Live, with the markup to copy.",
            ["Layout"]),
        new("utilities", "Utilities", "/Utilities", "sliders",
            "One class, one property: spacing, display, flex, text, colour, borders and sizing on the scales of the toolkit. Live, with the markup to copy.",
            ["Utilities"]),
    ];

    /// <summary>One line per group, shown above its components on the components page.</summary>
    public static readonly IReadOnlyDictionary<string, string> GroupSummaries = new Dictionary<string, string>
    {
        ["Actions"] = "Buttons, and how they are arranged under a form or a list.",
        ["Forms"] = "Fields, choices, pickers and the layout of a form: labels above, hints below, errors at the field.",
        ["Lists and data"] = "Everything for a list of items: the toolbar, the table, pagination, the gallery.",
        ["Content"] = "Text and pictures, cards, lists, accordions, labels, facts, meters, empty states and code.",
        ["Navigation"] = "The items of the menu with badges and submenus, switching between menus, tabs, the breadcrumb, steps and the index of a long page.",
        ["Feedback"] = "Alerts, toasts, dialogs, popovers, tooltips and placeholders.",
        ["Layout"] = "The breakpoints, the containers and the layouts a page is built from.",
        ["Utilities"] = "Classes that set one property from the scales of the toolkit: they are for your own page, not for repairing a component.",
    };

    /// <summary>The documentation of a section: its groups' components, in reading order.</summary>
    public static IEnumerable<ComponentDoc> In(CatalogSection section) => All.Where(c => section.Groups.Contains(c.Group));

    /// <summary>The section a component belongs to.</summary>
    public static CatalogSection SectionOf(ComponentDoc doc) => Sections.First(s => s.Groups.Contains(doc.Group));

    /// <summary>The anchor of a group on the components page.</summary>
    public static string GroupAnchor(string group) => "group-" + System.Text.RegularExpressions.Regex.Replace(group.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    public static ComponentDoc? Get(string id) => All.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>An icon of the sprite as markup, for the examples.</summary>
    internal static string Icon(string name, string? extraClass = null) => I(name, extraClass);

    private static string I(string name, string? extraClass = null)
        => $"<svg class=\"mat-icon{(extraClass is null ? string.Empty : " " + extraClass)}\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{name}\"></use></svg>";

    private static readonly IReadOnlyList<ComponentDoc> Core =
    [
        new("buttons", "Buttons", "Actions", "play",
            "Four kinds, three sizes. Every button is 40 px high on a mouse and 44 px on a touch screen.",
            ["BUTTON-01", "BUTTON-02", "BUTTON-03", "BUTTON-04", "BUTTON-05", "BUTTON-06"],
            [
                new("The four kinds", $"""
                    <div class="mat-row">
                      <button type="button" class="mat-btn mat-btn--primary">{I("save")}<span>Save</span></button>
                      <button type="button" class="mat-btn mat-btn--secondary">{I("back")}<span>Back</span></button>
                      <button type="button" class="mat-btn mat-btn--ghost">{I("refresh")}<span>Reload</span></button>
                      <button type="button" class="mat-btn mat-btn--danger">{I("trash")}<span>Delete</span></button>
                    </div>
                    """, "Primary is the one thing the page is for; secondary is everything else that is positive or neutral; ghost is quiet; danger is destructive."),
                new("Sizes, icon only, disabled", $"""
                    <div class="mat-row">
                      <button type="button" class="mat-btn mat-btn--primary mat-btn--sm">Small</button>
                      <button type="button" class="mat-btn mat-btn--primary">Default</button>
                      <button type="button" class="mat-btn mat-btn--primary mat-btn--lg">Large</button>
                      <button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Edit" data-tip="Edit">{I("edit")}</button>
                      <button type="button" class="mat-btn mat-btn--secondary" disabled>Disabled</button>
                    </div>
                    """, "An icon without a label has an aria-label and a tooltip (ICON-03)."),
                new("Full width and the solid danger button", $"""
                    <div class="mat-stack mat-stack--sm">
                      <button type="submit" class="mat-btn mat-btn--primary mat-btn--block">Sign in</button>
                      <button type="button" class="mat-btn mat-btn--danger mat-btn--solid">{I("trash")}<span>Delete for good</span></button>
                    </div>
                    """, "Block buttons belong on the sign-in page and on phones. The solid danger button is the last step of a destructive confirmation."),
            ],
            [
                "Labels start with a verb (Save, Add account, Delete), never OK or Submit.",
                "There is one primary button per page or dialog.",
                "A link that goes somewhere is an <a class=\"mat-btn\">; a button that does something is a <button>.",
            ]),

        new("button-row", "Button row", "Actions", "sliders",
            "Buttons under the content of a form, from positive to negative. The control enforces the order, not the author.",
            ["FORM-01", "FORM-02", "LIST-07", "LIST-08"],
            [
                new("Save, Back, space, Delete", $"""
                    <div class="mat-btn-row">
                      <button type="submit" class="mat-btn mat-btn--primary">{I("save")}<span>Save</span></button>
                      <a class="mat-btn mat-btn--secondary" href="#">{I("back")}<span>Back</span></a>
                      <span class="mat-btn-row__spacer"></span>
                      <button type="button" class="mat-btn mat-btn--danger">{I("trash")}<span>Delete</span></button>
                    </div>
                    """),
                new("The source order does not matter", $"""
                    <div class="mat-btn-row">
                      <button type="button" class="mat-btn mat-btn--danger">{I("trash")}<span>Delete</span></button>
                      <a class="mat-btn mat-btn--secondary" href="#">{I("back")}<span>Back</span></a>
                      <span class="mat-btn-row__spacer"></span>
                      <button type="submit" class="mat-btn mat-btn--primary">{I("save")}<span>Save</span></button>
                    </div>
                    """, "Delete is written first and the space in the middle, and the row still shows Save, Back, space, Delete: primary first, secondary next, the space, danger last."),
                new("Actions under a list", $"""
                    <div class="mat-actions">
                      <a class="mat-btn mat-btn--primary" href="#">{I("plus")}<span>New item</span></a>
                      <a class="mat-btn mat-btn--secondary" href="#">{I("upload")}<span>Import</span></a>
                    </div>
                    """, "On a list page the actions sit below the table and the pagination, left-aligned. Delete lives on the edit page, not in the list."),
            ],
            [
                "The row wraps on a phone; primary and secondary stay in front, the danger button goes to its own line at the end.",
                "In a dialog the same row is used with mat-btn-row--plain (no top border).",
                "mat-btn-row--sticky pins the row to the bottom of the screen for long forms on a phone.",
            ]),

        new("fields", "Fields", "Forms", "edit",
            "Label above, hint below, error at the field. Validation messages are written by the toolkit, not by the browser.",
            ["FORM-03", "FORM-05", "FORM-06", "FORM-08", "A11Y-04"],
            [
                new("A text field", """
                    <div class="mat-field">
                      <label class="mat-label" for="c-name">Name <span class="mat-req" aria-hidden="true">*</span></label>
                      <input class="mat-input" id="c-name" name="name" value="Quarterly report" required data-msg-required="Please enter a name." />
                      <span class="mat-hint">Shown in the list and in the breadcrumb.</span>
                      <span class="mat-error" aria-live="polite"></span>
                    </div>
                    """, "Every control has an id and a label that points to it (FORM-08). The empty mat-error is where the message appears."),
                new("A field with an error", """
                    <div class="mat-field is-invalid">
                      <label class="mat-label" for="c-mail">E-mail</label>
                      <input class="mat-input" id="c-mail" name="mail" type="email" value="alex@" aria-invalid="true" aria-describedby="c-mail-err" />
                      <span class="mat-error" id="c-mail-err" aria-live="polite">Please enter an e-mail address like name@example.com.</span>
                    </div>
                    """, "The toolkit sets is-invalid and the message when a form is submitted; it removes both as soon as the value is valid."),
                new("Select, text area and input group", """
                    <div class="mat-stack">
                      <div class="mat-field">
                        <label class="mat-label" for="c-cat">Category</label>
                        <select class="mat-select" id="c-cat" name="category"><option>Finance</option><option>Contracts</option><option>Family</option></select>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="c-note">Note</label>
                        <textarea class="mat-textarea" id="c-note" name="note" rows="3">Numbers are final after the review.</textarea>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="c-port">Port</label>
                        <div class="mat-input-group"><span class="mat-input-addon">http://localhost:</span><input class="mat-input mat-mono" id="c-port" name="port" value="8030" inputmode="numeric" /></div>
                      </div>
                    </div>
                    """),
            ],
            [
                "Required fields get the star (mat-req) in the label and the attribute required; the message comes from data-msg-required.",
                "novalidate is set on the form: the browser's own bubbles are never shown.",
                "On a touch screen inputs are 44 px high and 16 px type, so iOS does not zoom.",
            ]),

        new("choices", "Switches, checks and segments", "Forms", "toggle",
            "A switch turns something on or off at once; a check belongs to a form that is saved; a segmented control chooses one of a few.",
            ["FORM-04", "FORM-03"],
            [
                new("Switch and check", """
                    <div class="mat-stack mat-stack--sm">
                      <label class="mat-switch"><input type="checkbox" id="c-sw" name="notify" checked /><span class="mat-switch__track"></span><span>Tell me when it changes</span></label>
                      <label class="mat-check"><input type="checkbox" id="c-check" name="remember" /><span class="mat-check__text"><strong>Remember me</strong><small class="mat-muted">On this device only.</small></span></label>
                    </div>
                    """),
                new("Segmented control", """
                    <div class="mat-seg" role="group" aria-label="View">
                      <button type="button" class="mat-seg__btn" aria-pressed="true">Day</button>
                      <button type="button" class="mat-seg__btn" aria-pressed="false">Week</button>
                      <button type="button" class="mat-seg__btn" aria-pressed="false">Month</button>
                    </div>
                    """, "The pressed state is aria-pressed; the page's own script moves it. For the list and gallery switch use data-view-switch, which does it for you."),
                new("Settings rows", """
                    <div class="mat-card"><div class="mat-card__body">
                      <div class="mat-setting">
                        <label class="mat-setting__text" for="set-fail"><strong>Backup notifications</strong><small>Tell me when a backup fails.</small></label>
                        <label class="mat-switch mat-switch--bare"><input type="checkbox" id="set-fail" name="fail" checked /><span class="mat-switch__track"></span><span class="mat-sr-only">Backup notifications</span></label>
                      </div>
                      <div class="mat-setting">
                        <label class="mat-setting__text" for="set-update"><strong>Updates</strong><small>Install new versions of the apps at night.</small></label>
                        <label class="mat-switch mat-switch--bare"><input type="checkbox" id="set-update" name="update" /><span class="mat-switch__track"></span><span class="mat-sr-only">Updates</span></label>
                      </div>
                    </div></div>
                    """, "A setting: what it is and what it does on the left, the switch on the right. The whole text is a label for the switch."),
            ],
            [
                "The knob of a switch is always white (--mat-thumb), in light and dark.",
                "A switch is not used for something that needs a Save button.",
            ]),

        new("form-layout", "Edit form", "Forms", "template",
            "A card with a two-column grid, full-width fields where needed, a dependent field and the buttons under it.",
            ["FORM-01", "FORM-04", "FORM-07", "FORM-10", "LIST-01"],
            [
                new("An edit form", $"""
                    <form class="mat-form" method="get" action="/Components#form-layout" novalidate>
                      <div class="mat-card">
                        <div class="mat-card__head">{I("edit")}<span class="mat-card__title">Item</span></div>
                        <div class="mat-card__body">
                          <div class="mat-form__grid">
                            <div class="mat-field mat-field--full">
                              <label class="mat-label" for="fl-title">Title <span class="mat-req" aria-hidden="true">*</span></label>
                              <input class="mat-input" id="fl-title" name="title" value="Quarterly report" required data-msg-required="Please enter a title." />
                              <span class="mat-error" aria-live="polite"></span>
                            </div>
                            <div class="mat-field">
                              <label class="mat-label" for="fl-cat">Category</label>
                              <select class="mat-select" id="fl-cat" name="category"><option>Finance</option><option>Contracts</option></select>
                            </div>
                            <div class="mat-field">
                              <label class="mat-label" for="fl-date">Date</label>
                              <input class="mat-input" id="fl-date" name="date" type="date" value="2026-09-28" />
                            </div>
                            <div class="mat-field mat-field--full">
                              <label class="mat-switch"><input type="checkbox" id="fl-notify" name="notify" checked /><span class="mat-switch__track"></span><span>Tell me when it changes</span></label>
                            </div>
                            <div class="mat-field mat-field--full" data-show-if="#fl-notify" data-show-value="true">
                              <label class="mat-label" for="fl-mail">E-mail for the notification</label>
                              <input class="mat-input" id="fl-mail" name="mail" type="email" value="alex@example.com" />
                              <span class="mat-hint">Only asked while the switch is on.</span>
                            </div>
                          </div>
                        </div>
                      </div>
                      <div class="mat-btn-row">
                        <button type="submit" class="mat-btn mat-btn--primary">{I("save")}<span>Save</span></button>
                        <a class="mat-btn mat-btn--secondary" href="#">{I("back")}<span>Back</span></a>
                        <span class="mat-btn-row__spacer"></span>
                        <button type="button" class="mat-btn mat-btn--danger" data-confirm="Delete this item? This cannot be undone." data-confirm-ok="Delete">{I("trash")}<span>Delete</span></button>
                      </div>
                    </form>
                    """, "Switch the notification off: the e-mail field disappears. The buttons are under the content, positive to negative."),
            ],
            [
                "data-show-if takes a selector (#id) or a field name; data-show-value is a value, a comma list, * (anything) or true (checked).",
                "A hidden dependent field is disabled, so it is not sent and not validated.",
                "A long form is split into cards with a title each (FORM-10); the buttons stay under the last one.",
            ]),

        new("toolbar", "Toolbar", "Lists and data", "sliders",
            "Filter and Sort are buttons that open a dialog, and the search is a button too or a field right in the bar. The toolbar is always above the list or the gallery.",
            ["LIST-02", "LIST-03", "LIST-04", "LIST-05", "DIALOG-02", "DIALOG-03"],
            [
                new("The toolbar with its three dialogs", $"""
                    <div class="mat-list">
                      <div class="mat-toolbar" role="toolbar" aria-label="Items tools">
                        <button type="button" class="mat-btn mat-btn--ghost mat-toolbar__btn" data-dialog-open="tb-search" aria-haspopup="dialog">{I("search")}<span>Search</span></button>
                        <button type="button" class="mat-btn mat-btn--ghost mat-toolbar__btn is-active" data-dialog-open="tb-filter" aria-haspopup="dialog">{I("sliders")}<span>Filter</span><span class="mat-badge">1</span></button>
                        <button type="button" class="mat-btn mat-btn--ghost mat-toolbar__btn" data-dialog-open="tb-sort" aria-haspopup="dialog">{I("sort")}<span>Sort</span></button>
                        <span class="mat-toolbar__spacer"></span>
                        <span class="mat-toolbar__info" aria-live="polite">4 of 12 items</span>
                      </div>
                      <div class="mat-chips" role="group" aria-label="Applied tools">
                        <span class="mat-chip">Category: Finance<a class="mat-chip__x" href="#" aria-label="Remove the filter Category">{I("x")}</a></span>
                      </div>

                      <form id="tb-form" method="get" action="/Components#toolbar" hidden></form>

                      <dialog class="mat-dialog mat-dialog--sm" id="tb-search" aria-labelledby="tb-search-title">
                        <div class="mat-dialog__form">
                          <div class="mat-dialog__head">{I("search")}<h2 class="mat-dialog__title" id="tb-search-title">Search</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                          <div class="mat-dialog__body"><div class="mat-field"><label class="mat-label" for="tb-q">Search for</label><input class="mat-input" type="search" id="tb-q" name="q" form="tb-form" placeholder="Name or category" autocomplete="off" /></div></div>
                          <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" form="tb-form" class="mat-btn mat-btn--primary">{I("search")}<span>Apply</span></button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button><span class="mat-btn-row__spacer"></span><a class="mat-btn mat-btn--ghost mat-btn--end" href="#">Clear search</a></div></div>
                        </div>
                      </dialog>

                      <dialog class="mat-dialog mat-dialog--sm" id="tb-filter" aria-labelledby="tb-filter-title">
                        <div class="mat-dialog__form">
                          <div class="mat-dialog__head">{I("sliders")}<h2 class="mat-dialog__title" id="tb-filter-title">Filter</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                          <div class="mat-dialog__body mat-form"><div class="mat-field"><label class="mat-label" for="tb-cat">Category</label><select class="mat-select" id="tb-cat" name="category" form="tb-form"><option value="">All</option><option selected>Finance</option><option>Contracts</option></select></div></div>
                          <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" form="tb-form" class="mat-btn mat-btn--primary">{I("sliders")}<span>Apply</span></button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button><span class="mat-btn-row__spacer"></span><a class="mat-btn mat-btn--ghost mat-btn--end" href="#">Reset everything</a></div></div>
                        </div>
                      </dialog>

                      <dialog class="mat-dialog mat-dialog--sm" id="tb-sort" aria-labelledby="tb-sort-title">
                        <div class="mat-dialog__form">
                          <div class="mat-dialog__head">{I("sort")}<h2 class="mat-dialog__title" id="tb-sort-title">Sort</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                          <div class="mat-dialog__body mat-form">
                            <fieldset class="mat-field mat-fieldset"><legend class="mat-label">Sort by</legend><div class="mat-options"><label class="mat-option"><input type="radio" id="tb-sort-date" name="sort" value="date" form="tb-form" checked /><span>Date</span></label><label class="mat-option"><input type="radio" id="tb-sort-name" name="sort" value="name" form="tb-form" /><span>Name</span></label></div></fieldset>
                            <fieldset class="mat-field mat-fieldset"><legend class="mat-label">Order</legend><div class="mat-options"><label class="mat-option"><input type="radio" id="tb-dir-asc" name="dir" value="asc" form="tb-form" /><span>Ascending</span></label><label class="mat-option"><input type="radio" id="tb-dir-desc" name="dir" value="desc" form="tb-form" checked /><span>Descending</span></label></div></fieldset>
                          </div>
                          <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" form="tb-form" class="mat-btn mat-btn--primary">{I("sort")}<span>Apply</span></button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button><span class="mat-btn-row__spacer"></span><a class="mat-btn mat-btn--ghost mat-btn--end" href="#">Default order</a></div></div>
                        </div>
                      </dialog>
                    </div>
                    """, "In Razor the partial _Toolbar draws this from a ToolbarModel. Every field of every dialog belongs to the one GET form (the form attribute), so each dialog sends the complete state."),
                new("The search as a field, Filter and Sort at the right end", $"""
                    <div class="mat-list">
                      <div class="mat-toolbar" role="toolbar" aria-label="Documents tools">
                        <div class="mat-input-icon mat-toolbar__search">
                          {I("search")}
                          <input class="mat-input" type="search" id="tb2-q" name="q" form="tb2-form" placeholder="Search documents" aria-label="Documents: search" autocomplete="off" enterkeyhint="search" data-shortcut="/" />
                        </div>
                        <span class="mat-toolbar__spacer"></span>
                        <span class="mat-toolbar__info" aria-live="polite">12 documents</span>
                        <div class="mat-toolbar__end">
                          <button type="button" class="mat-btn mat-btn--ghost mat-toolbar__btn" data-dialog-open="tb2-filter" aria-haspopup="dialog">{I("sliders")}<span>Filter</span></button>
                          <button type="button" class="mat-btn mat-btn--ghost mat-toolbar__btn is-active" data-dialog-open="tb2-sort" aria-haspopup="dialog">{I("sort")}<span>Sort</span><small>Date ↓</small></button>
                        </div>
                      </div>

                      <form id="tb2-form" method="get" action="/Components#toolbar" hidden></form>

                      <dialog class="mat-dialog mat-dialog--sm" id="tb2-filter" aria-labelledby="tb2-filter-title">
                        <div class="mat-dialog__form">
                          <div class="mat-dialog__head">{I("sliders")}<h2 class="mat-dialog__title" id="tb2-filter-title">Filter</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                          <div class="mat-dialog__body mat-form"><div class="mat-field"><label class="mat-label" for="tb2-cat">Category</label><select class="mat-select" id="tb2-cat" name="category" form="tb2-form"><option value="">All</option><option>Finance</option><option>Contracts</option></select></div></div>
                          <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" form="tb2-form" class="mat-btn mat-btn--primary">{I("sliders")}<span>Apply</span></button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button><span class="mat-btn-row__spacer"></span><a class="mat-btn mat-btn--ghost mat-btn--end" href="#">Reset everything</a></div></div>
                        </div>
                      </dialog>

                      <dialog class="mat-dialog mat-dialog--sm" id="tb2-sort" aria-labelledby="tb2-sort-title">
                        <div class="mat-dialog__form">
                          <div class="mat-dialog__head">{I("sort")}<h2 class="mat-dialog__title" id="tb2-sort-title">Sort</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                          <div class="mat-dialog__body mat-form">
                            <fieldset class="mat-field mat-fieldset"><legend class="mat-label">Sort by</legend><div class="mat-options"><label class="mat-option"><input type="radio" id="tb2-sort-date" name="sort" value="date" form="tb2-form" checked /><span>Date</span></label><label class="mat-option"><input type="radio" id="tb2-sort-name" name="sort" value="name" form="tb2-form" /><span>Name</span></label></div></fieldset>
                            <fieldset class="mat-field mat-fieldset"><legend class="mat-label">Order</legend><div class="mat-options"><label class="mat-option"><input type="radio" id="tb2-dir-asc" name="dir" value="asc" form="tb2-form" /><span>Ascending</span></label><label class="mat-option"><input type="radio" id="tb2-dir-desc" name="dir" value="desc" form="tb2-form" checked /><span>Descending</span></label></div></fieldset>
                          </div>
                          <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" form="tb2-form" class="mat-btn mat-btn--primary">{I("sort")}<span>Apply</span></button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button><span class="mat-btn-row__spacer"></span><a class="mat-btn mat-btn--ghost mat-btn--end" href="#">Default order</a></div></div>
                        </div>
                      </dialog>
                    </div>
                    """, "For a list that is searched all the time: the field sits at the left, the count and the dialog buttons at the right end. Enter sends the search with the filter and the sort in effect (they share the one GET form). In Razor: SearchField = true on the ToolbarModel."),
            ],
            [
                "A tool in effect is visible: the button is highlighted, the filter shows its count, the search and the sort show their value, and a chip with an X removes each one.",
                "Press / on the page to open the search dialog, or to put the cursor in the search field (data-shortcut=\"/\"); it is not triggered while typing in a field.",
                "Use the field when searching is the main thing to do with the list; use the button when the toolbar should stay short. Never both, and never selects or date ranges in the bar itself (they go in the Filter dialog).",
                "mat-toolbar__end keeps its buttons together at the right end, whatever the width; on a phone the field takes the first row and the buttons stand right-aligned in the next.",
                "On a phone every dialog is full screen with the X at the top right (DIALOG-03).",
                "The toolbar is the first child of mat-list; the table or gallery, the pager and the actions follow in that order, whatever the source order is.",
            ]),

        new("table", "Table", "Lists and data", "table",
            "Rows with a name column, status pills, right-aligned numbers and row actions. On a phone the rows become cards.",
            ["TABLE-01", "TABLE-02", "TABLE-03", "TABLE-04", "LIST-09", "LIST-11", "MOBILE-03"],
            [
                new("A list table", $"""
                    <div class="mat-table-wrap">
                      <table class="mat-table">
                        <caption class="mat-sr-only">Items</caption>
                        <thead><tr><th scope="col">Name</th><th scope="col">Status</th><th scope="col" class="mat-num">Size</th><th scope="col" class="mat-hide-sm">Date</th><th scope="col"><span class="mat-sr-only">Actions</span></th></tr></thead>
                        <tbody>
                          <tr>
                            <td data-label="Name"><div class="mat-table__name"><span>Quarterly report</span></div></td>
                            <td data-label="Status"><span class="mat-pill mat-pill--ok">Done</span></td>
                            <td data-label="Size" class="mat-num mat-mono">412 KB</td>
                            <td data-label="Date" class="mat-hide-sm mat-mono">2026-09-28</td>
                            <td class="mat-table__cell-actions"><div class="mat-table__actions"><a class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" href="#" aria-label="Edit Quarterly report" data-tip="Edit">{I("edit")}</a></div></td>
                          </tr>
                          <tr>
                            <td data-label="Name"><div class="mat-table__name"><span>Insurance policy</span></div></td>
                            <td data-label="Status"><span class="mat-pill mat-pill--warn">Review</span></td>
                            <td data-label="Size" class="mat-num mat-mono">188 KB</td>
                            <td data-label="Date" class="mat-hide-sm mat-mono">2026-09-21</td>
                            <td class="mat-table__cell-actions"><div class="mat-table__actions"><a class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" href="#" aria-label="Edit Insurance policy" data-tip="Edit">{I("edit")}</a></div></td>
                          </tr>
                          <tr>
                            <td data-label="Name"><div class="mat-table__name"><span>Tax return 2025</span></div></td>
                            <td data-label="Status"><span class="mat-pill mat-pill--error">Failed</span></td>
                            <td data-label="Size" class="mat-num mat-mono">96 KB</td>
                            <td data-label="Date" class="mat-hide-sm mat-mono">2026-09-09</td>
                            <td class="mat-table__cell-actions"><div class="mat-table__actions"><a class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" href="#" aria-label="Edit Tax return 2025" data-tip="Edit">{I("edit")}</a></div></td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                    """, "Every td carries data-label: on a phone it becomes the label of that line of the card (TABLE-02). Narrow the window to see it."),
            ],
            [
                "Numbers are right-aligned (mat-num) and set in mono; IDs, versions, ports and paths are mono as well (TABLE-01).",
                "Status is a pill with text, never a colour alone (TABLE-03).",
                "Below 720 px the head is hidden and each row is a card; columns marked mat-hide-sm disappear.",
                "mat-table--zebra stripes the rows; use it for long reference tables only.",
            ]),

        new("pager", "Pagination", "Lists and data", "chevron-right",
            "Directly under the table, nothing in between. It renders nothing for a single page.",
            ["LIST-06", "LIST-12", "CODE-06"],
            [
                new("A pager on page 3 of 8", $"""
                    <nav class="mat-pager" aria-label="Pagination">
                      <span class="mat-pager__info">51 to 75 of 190</span>
                      <div class="mat-pager__pages">
                        <a class="mat-pager__btn" href="#" aria-label="Previous page" rel="prev">{I("chevron-left")}</a>
                        <a class="mat-pager__btn" href="#" aria-label="Page 1">1</a>
                        <a class="mat-pager__btn" href="#" aria-label="Page 2">2</a>
                        <span class="mat-pager__btn is-current" aria-current="page">3</span>
                        <a class="mat-pager__btn" href="#" aria-label="Page 4">4</a>
                        <a class="mat-pager__btn" href="#" aria-label="Page 5">5</a>
                        <span class="mat-pager__gap" aria-hidden="true">…</span>
                        <a class="mat-pager__btn" href="#" aria-label="Page 8">8</a>
                        <a class="mat-pager__btn" href="#" aria-label="Next page" rel="next">{I("chevron-right")}</a>
                      </div>
                    </nav>
                    """, "In Razor: the partial _Pager from a PagerModel. The page number is a query value (?page=3), never a route value."),
            ],
            [
                "The window shows the first and the last page and two on each side of the current one.",
                "The pager keeps the search, filter and sort in every link.",
            ]),

        new("gallery", "Gallery", "Lists and data", "grid",
            "The same items as tiles. A list can offer both; the choice is remembered per list.",
            ["LIST-10", "LIST-02", "MOBILE-02"],
            [
                new("Tiles", $"""
                    <div class="mat-gallery">
                      <a class="mat-tile" href="#"><span class="mat-tile__media">{I("dollar")}</span><span class="mat-tile__body"><span class="mat-tile__title">Quarterly report</span><span class="mat-tile__meta">Finance · 412 KB</span></span></a>
                      <a class="mat-tile" href="#"><span class="mat-tile__media">{I("file-text")}</span><span class="mat-tile__body"><span class="mat-tile__title">Insurance policy</span><span class="mat-tile__meta">Contracts · 188 KB</span></span></a>
                      <a class="mat-tile" href="#"><span class="mat-tile__media">{I("image")}</span><span class="mat-tile__body"><span class="mat-tile__title">Holiday photos</span><span class="mat-tile__meta">Family · 5.1 MB</span></span></a>
                    </div>
                    """, "The gallery has the same toolbar above it as the table. The grid fills the width with tiles at least 220 px wide and becomes one column on a phone."),
            ],
            [
                "To offer both views, wrap the list in an element with data-view-scope and give the table and the gallery data-view=\"list\" and data-view=\"gallery\"; the toolbar's view switch does the rest.",
                "A tile that is selected carries `is-selected`; in a `data-selectable` list the script sets it when the `data-select-row` check inside the tile is ticked, like for a table row (see Table and Cards).",
                "A check inside a link is not valid HTML, so a selectable tile is a `div` with the link inside it.",
            ]),

        new("cards", "Cards", "Content", "box",
            "The container for a group of content. Cards sit on the page background and carry the large radius.",
            ["SPACE-02", "SPACE-03", "FORM-10"],
            [
                new("Card with head and foot", $"""
                    <div class="mat-card">
                      <div class="mat-card__head">{I("server")}<h3 class="mat-card__title">Reverse proxy</h3><span class="mat-pill mat-pill--ok">Online</span></div>
                      <div class="mat-card__body"><p>Routes the hosts of your apps and renews their certificates.</p></div>
                      <div class="mat-card__foot"><a class="mat-btn mat-btn--secondary mat-btn--sm" href="#">{I("settings")}<span>Settings</span></a></div>
                    </div>
                    """),
                new("Accent card and link card", $"""
                    <div class="mat-grid">
                      <div class="mat-card mat-card--accent"><div class="mat-card__body"><strong>Highlighted</strong><p class="mat-muted">Use one accent card per page, for the thing to look at first.</p></div></div>
                      <a class="mat-card" href="#"><div class="mat-card__body"><strong>A card that is a link</strong><p class="mat-muted">Its border darkens on hover.</p></div></a>
                    </div>
                    """),
            ],
            [
                "mat-card--flush removes the padding of the body, for a table that fills the card.",
                "Card titles are h3 (h2 when the card is a section of its own).",
            ]),

        new("pills", "Pills, badges, tags and chips", "Content", "tag",
            "Small labels. A pill tells a state, a badge a count, a tag a category, a chip a tool that is in effect.",
            ["TABLE-03", "COLOR-03", "COLOR-04", "LIST-04"],
            [
                new("Status pills", """
                    <div class="mat-row">
                      <span class="mat-pill mat-pill--ok">Online</span>
                      <span class="mat-pill mat-pill--warn">Degraded</span>
                      <span class="mat-pill mat-pill--error">Offline</span>
                      <span class="mat-pill mat-pill--info">Info</span>
                      <span class="mat-pill mat-pill--busy">Running</span>
                      <span class="mat-pill">Unknown</span>
                      <span class="mat-pill mat-pill--accent">Accent</span>
                      <span class="mat-pill mat-pill--plain">Plain</span>
                    </div>
                    """, "Green, amber, red and blue have fixed meanings in every app (COLOR-03). The text always says it too (COLOR-04)."),
                new("Badge, tag and chip", $"""
                    <div class="mat-row">
                      <span class="mat-badge">3</span>
                      <span class="mat-badge mat-badge--soft">12</span>
                      <span class="mat-tag">Finance</span>
                      <span class="mat-chip">Category: Finance<a class="mat-chip__x" href="#" aria-label="Remove the filter Category">{I("x")}</a></span>
                    </div>
                    """),
            ],
            [
                "A pill is not a button. If it can be clicked, it is a chip with an X or a real button.",
            ]),

        new("kv", "Key and value", "Content", "list",
            "Facts about one thing, label left, value right. Technical values are mono.",
            ["TYPE-03", "TABLE-01"],
            [
                new("Facts", """
                    <div class="mat-card"><div class="mat-card__body">
                      <dl class="mat-kv">
                        <dt>Version</dt><dd class="mat-mono">1.4.212-20261010</dd>
                        <dt>Container</dt><dd class="mat-mono">mattiverse-matdock</dd>
                        <dt>Port</dt><dd class="mat-mono">8030</dd>
                        <dt>Updated</dt><dd>Today, 09:41</dd>
                      </dl>
                    </div></div>
                    """),
            ],
            ["The labels are muted; they sit left on a desktop and above the value on a phone."]),

        new("meter", "Meter", "Content", "bar-chart",
            "A bar for a share of something. It has a role and a name, and the width comes from data-value.",
            ["A11Y-03", "CODE-02"],
            [
                new("Storage", """
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-row mat-row--between"><span>Storage</span><span class="mat-mono">63 %</span></div>
                      <div class="mat-meter" role="progressbar" aria-valuenow="63" aria-valuemin="0" aria-valuemax="100" aria-label="Storage used"><div class="mat-meter__fill" data-value="63"></div></div>
                    </div>
                    """, "The width is not an inline style: the script reads data-value and sets it, so the markup stays free of style attributes."),
            ],
            ["The colour of the fill is the accent; use a status colour only when the value means something (for example a full disk)."]),

        new("empty", "Empty state", "Content", "search",
            "When a list has nothing to show: say why and offer the way out.",
            ["LIST-11", "CODE-06"],
            [
                new("Nothing matches", $"""
                    <div class="mat-empty">
                      <span class="mat-empty__icon">{I("search")}</span>
                      <div class="mat-empty__title">Nothing matches</div>
                      <p>Change the search or the filters, or reset them.</p>
                      <a class="mat-btn mat-btn--primary" href="#">{I("refresh")}<span>Reset the tools</span></a>
                    </div>
                    """),
            ],
            ["An empty list without any tool in effect offers the primary action instead (\"Add your first account\")."]),

        new("code", "Code", "Content", "terminal",
            "Inline code, keys and blocks. All set in the mono stack.",
            ["TYPE-03"],
            [
                new("Inline and block", """
                    <div class="mat-stack mat-stack--sm">
                      <p>Press <kbd>/</kbd> to search, or run <code>docker compose up -d</code>.</p>
                      <pre class="mat-code"><code>services:
                      app:
                        image: ghcr.io/example/app:latest
                        ports: ["8060:8060"]</code></pre>
                    </div>
                    """),
                new("Code with colours", """
                    <div class="mat-stack mat-stack--sm">
                      <pre class="mat-code"><code><span class="mat-code__com">&lt;!-- one save button --&gt;</span>
                    <span class="mat-code__pun">&lt;</span><span class="mat-code__tag">button</span> <span class="mat-code__attr">type</span><span class="mat-code__pun">=</span><span class="mat-code__str">"submit"</span> <span class="mat-code__attr">class</span><span class="mat-code__pun">=</span><span class="mat-code__str">"mat-btn mat-btn--primary"</span><span class="mat-code__pun">&gt;</span>Save<span class="mat-code__pun">&lt;/</span><span class="mat-code__tag">button</span><span class="mat-code__pun">&gt;</span></code></pre>
                      <pre class="mat-code"><code><span class="mat-code__com">// ask first</span>
                    <span class="mat-code__kw">const</span> ok = <span class="mat-code__kw">await</span> MatUi.confirm(<span class="mat-code__str">'Restart?'</span>, { ok: <span class="mat-code__str">'Restart'</span>, delay: <span class="mat-code__num">250</span> });</code></pre>
                    </div>
                    """, "The parts are spans: tag and keyword (mat-code__tag, mat-code__kw), attribute and number (__attr, __num), string (__str), comment (__com) and punctuation (__pun). The component pages of this site colour their code with them."),
            ],
            ["A block scrolls sideways instead of wrapping; it never breaks the layout on a phone.", "Colouring is optional: a block without the spans is plain mono text. The colours are the status text colours, so they hold in both modes."]),

        new("tabs", "Tabs", "Navigation", "layers",
            "Several panels of one page, or several pages that belong together. Both look the same.",
            ["A11Y-02", "SHELL-09"],
            [
                new("Tabs with panels", """
                    <div data-tabs>
                      <div class="mat-tabs" role="tablist" aria-label="Sections">
                        <button type="button" class="mat-tab" role="tab" id="tab-general" aria-controls="panel-general" aria-selected="true">General</button>
                        <button type="button" class="mat-tab" role="tab" id="tab-network" aria-controls="panel-network" aria-selected="false" tabindex="-1">Network</button>
                        <button type="button" class="mat-tab" role="tab" id="tab-backup" aria-controls="panel-backup" aria-selected="false" tabindex="-1">Backup</button>
                      </div>
                      <div class="mat-tabpanel" role="tabpanel" id="panel-general" aria-labelledby="tab-general"><p>General settings.</p></div>
                      <div class="mat-tabpanel" role="tabpanel" id="panel-network" aria-labelledby="tab-network" hidden><p>Network settings.</p></div>
                      <div class="mat-tabpanel" role="tabpanel" id="panel-backup" aria-labelledby="tab-backup" hidden><p>Backup settings.</p></div>
                    </div>
                    """, "Arrow keys move between the tabs, Home and End jump to the first and the last."),
                new("Tabs that are links", """
                    <nav class="mat-tabs" aria-label="Sections">
                      <a class="mat-tab is-active" href="#" aria-current="page">Colour</a>
                      <a class="mat-tab" href="#">Type</a>
                      <a class="mat-tab" href="#">Space</a>
                    </nav>
                    """, "When each tab is a page of its own, the tabs are links and the current one carries aria-current."),
            ],
            ["On a phone the tab bar scrolls sideways; its scrollbar shows only while scrolling."]),

        new("menu", "Context menu", "Navigation", "more-vertical",
            "A small menu next to the button that opened it: the user menu, the actions of a row.",
            ["SHELL-05", "DIALOG-07", "A11Y-02"],
            [
                new("A menu", $"""
                    <button type="button" class="mat-btn mat-btn--secondary" data-menu-toggle="demo-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false">{I("more-horizontal")}<span>Actions</span></button>
                    <div class="mat-menu" id="demo-menu" role="menu" aria-label="Actions" hidden>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("edit")}<span>Rename</span></a>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("copy")}<span>Duplicate</span></a>
                      <div class="mat-menu__sep" role="separator"></div>
                      <a class="mat-menu__item mat-menu__item--danger" role="menuitem" href="#">{I("trash")}<span>Delete</span></a>
                    </div>
                    """, "Open it, then use the arrow keys; Escape closes it and returns focus to the button."),
            ],
            [
                "The menu is placed by the script next to the button (data-menu-place=\"top\" or \"bottom\") and stays inside the screen.",
                "The destructive item comes last, behind a separator.",
            ]),

        new("breadcrumb", "Breadcrumb", "Navigation", "chevron-right",
            "Where the page is. It sits above the content in the top bar; on a phone it shows the last two items.",
            ["SHELL-08", "MOBILE-02"],
            [
                new("A trail", """
                    <nav class="mat-crumbs" aria-label="Breadcrumb">
                      <ol>
                        <li><a href="#">MatMail</a></li>
                        <li><a href="#">Settings</a></li>
                        <li><a href="#">Accounts</a></li>
                        <li aria-current="page">Alex Morgan</li>
                      </ol>
                    </nav>
                    """, "The first item is the app, the last is the page (not a link). The shell draws it from the page's Crumbs."),
            ],
            ["Every page of every app has one (SHELL-08), including the start page, where it is only the app name."]),

        new("topbar", "Header bar", "Navigation", "template",
            "The bar above every page: the breadcrumb on the left and, on the right, icons for the things of the whole app; the main actions may be highlighted.",
            ["SHELL-17", "SHELL-08", "ICON-03", "MOBILE-02"],
            [
                new("Breadcrumb and icons", $"""
                    <header class="mat-topbar">
                      <nav class="mat-crumbs" aria-label="Breadcrumb">
                        <ol><li><a href="#">MatMail</a></li><li><a href="#">Settings</a></li><li aria-current="page">Accounts</li></ol>
                      </nav>
                      <div class="mat-topbar__actions">
                        <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="Search" data-tip="Search">{I("search")}</button>
                        <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="Help" data-tip="Help">{I("help-circle")}</button>
                      </div>
                    </header>
                    """, "The icons sit at the right end, after the breadcrumb. Each is a ghost icon button with a name for screen readers and a tooltip.", Frame: "bar"),
                new("With a count and a dot", $"""
                    <header class="mat-topbar">
                      <nav class="mat-crumbs" aria-label="Breadcrumb">
                        <ol><li><a href="#">MatMail</a></li><li aria-current="page">Inbox</li></ol>
                      </nav>
                      <div class="mat-topbar__actions">
                        <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="Notifications, 3 new" data-tip="Notifications">{I("bell")}<span class="mat-topbar__badge" aria-hidden="true">3</span></button>
                        <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="Sync, one account needs attention" data-tip="Sync">{I("refresh")}<span class="mat-topbar__badge mat-topbar__badge--dot mat-topbar__badge--warn" aria-hidden="true"></span></button>
                        <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="More" data-tip="More">{I("more-vertical")}</button>
                      </div>
                    </header>
                    """, "A count or a dot goes in a badge on the icon; the name of the button says what the badge says, because the badge itself is hidden from screen readers. Tones: accent (default), warn, error.", Frame: "bar"),
                new("A main action and a secondary one", $"""
                    <header class="mat-topbar">
                      <nav class="mat-crumbs" aria-label="Breadcrumb">
                        <ol><li><a href="#">MatPaper</a></li><li aria-current="page">Documents</li></ol>
                      </nav>
                      <div class="mat-topbar__actions">
                        <button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Search" data-tip="Search">{I("search")}</button>
                        <button type="button" class="mat-btn mat-btn--primary mat-btn--icon" aria-label="Add a document" data-tip="Add a document">{I("plus")}</button>
                      </div>
                    </header>
                    """, "The main action of the app (scan a document, add something) is the one icon in the accent colour, last, at the right end. A second action that should be seen but is not the main one is a secondary icon button; the other icons stay ghost. All keep a name and a tooltip.", Frame: "bar"),
            ],
            ["At most four icons; more go behind one icon that opens a context menu (the `menu` component).",
             "The main action is highlighted with `mat-btn--primary` instead of `mat-btn--ghost`, at most one: with several icons in the accent colour nothing stands out any more. A second one beside it is `mat-btn--secondary`.",
             "Only things of the whole app belong here. Page actions stay with their content (FORM-01, LIST-07); the theme button and the user stay at the foot of the menu (SHELL-03).",
             "On a phone the icons stay and the breadcrumb shows only its last two items (MOBILE-02)."]),

        new("footer", "Footer", "Navigation", "minus",
            "The end of a page: what the app is on one side, the links that belong to every page on the other.",
            ["SHELL-18", "SHELL-11", "A11Y-03", "MOBILE-02"],
            [
                new("Name and links", """
                    <footer class="mat-footer">
                      <p class="mat-footer__text">MatMail · © 2026 Example GmbH</p>
                      <ul class="mat-footer__links">
                        <li><a href="#">Imprint</a></li>
                        <li><a href="#">Privacy</a></li>
                        <li><a href="#">Help</a></li>
                      </ul>
                    </footer>
                    """, "It comes after the content, inside the scrolling area, and lines up with the width of the content. It is never fixed over the page.", Frame: "bar"),
                new("Centred, for a page without a menu", """
                    <footer class="mat-footer mat-footer--center mat-footer--plain">
                      <ul class="mat-footer__links">
                        <li><a href="#">Imprint</a></li>
                        <li><a href="#">Privacy</a></li>
                        <li><a href="#">Contact</a></li>
                      </ul>
                    </footer>
                    """, "On a sign-in page (`mat-auth`) it is the last child, under the card: the card stays centred and the footer sits at the bottom. `mat-footer--plain` leaves out the line above.", Frame: "bar"),
            ],
            ["In the shell it is a `footer` element after the `main` of the page, so screen readers find it as the information about the page. On a page without a menu the page itself is the `main`, and the footer sits inside it.",
             "The version of the app stays in the menu (SHELL-06), not in the footer.",
             "On a phone the two sides wrap: the text above, the links below."]),

        new("steps", "Steps", "Navigation", "route",
            "Progress through a wizard: finished, current, to come.",
            ["A11Y-03", "COLOR-04"],
            [
                new("Three of five", """
                    <ol class="mat-steps" aria-label="Setup progress">
                      <li class="is-done"><span class="mat-sr-only">Step 1, done</span></li>
                      <li class="is-done"><span class="mat-sr-only">Step 2, done</span></li>
                      <li class="is-active" aria-current="step"><span class="mat-sr-only">Step 3, current</span></li>
                      <li><span class="mat-sr-only">Step 4</span></li>
                      <li><span class="mat-sr-only">Step 5</span></li>
                    </ol>
                    """, "The bar alone does not say which step it is: the hidden text does, for screen readers (COLOR-04)."),
            ],
            ["Put the title of the current step and \"Step 3 of 5\" next to the bar."]),

        new("navmenu", "Menu items, badges and submenus", "Navigation", "sidebar",
            "What the menu on the left is made of: items with an icon, badges at the right end, parents that open their children, and a main action filled with the accent.",
            ["SHELL-02", "SHELL-13", "SHELL-14", "SHELL-19", "COLOR-03", "COLOR-04", "A11Y-02"],
            [
                new("Items and badges", $"""
                    <nav class="mat-nav__scroll" aria-label="Demo menu">
                      <ul class="mat-nav__list">
                        <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("home")}<span>Dashboard</span></a></li>
                        <li class="mat-nav__group" role="presentation">Work</li>
                        <li><a class="mat-nav__item" href="#">{I("mail")}<span>Inbox</span><span class="mat-nav__badge">12<span class="mat-sr-only"> unread</span></span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("alert")}<span>Alerts</span><span class="mat-nav__badge mat-nav__badge--warn">3<span class="mat-sr-only"> to look at</span></span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("archive")}<span>Backups</span><span class="mat-nav__badge mat-nav__badge--error">1<span class="mat-sr-only"> failed</span></span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("bell")}<span>News</span><span class="mat-nav__badge mat-nav__badge--dot"><span class="mat-sr-only">something new</span></span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("folder")}<span>Files</span><span class="mat-nav__badge mat-nav__badge--plain">99+<span class="mat-sr-only"> files</span></span></a></li>
                      </ul>
                    </nav>
                    """, "A count at the right end; above 99 it reads 99+. Tones: accent (default), ok, warn, error, info, plain; a dot says that something is new. Every badge has a text for screen readers.", Frame: "menu"),
                new("A main action", $"""
                    <nav class="mat-nav__scroll" aria-label="Demo menu with a main action">
                      <ul class="mat-nav__list">
                        <li><a class="mat-nav__item mat-nav__item--primary" href="#">{I("plus")}<span>Add document</span></a></li>
                        <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("home")}<span>Overview</span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("mail")}<span>Inbox</span><span class="mat-nav__badge">2<span class="mat-sr-only"> to review</span></span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("file-text")}<span>Documents</span></a></li>
                      </ul>
                    </nav>
                    """, "The one thing the app is mostly used for, first in the menu and filled with the accent (SHELL-19): an icon and a label, no badge. On its own page it stays filled, a step darker.", Frame: "menu"),
                new("A list with its action", $"""
                    <nav class="mat-nav__scroll" aria-label="Demo menu with a list">
                      <ul class="mat-nav__list">
                        <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("home")}<span>Overview</span></a></li>
                        <li class="mat-nav__group mat-nav__group--action" role="presentation">
                          <span class="mat-nav__group-title">Lists</span>
                          <a class="mat-nav__group-action" href="#" aria-label="Manage lists" title="Manage lists">{I("settings")}</a>
                        </li>
                        <li><a class="mat-nav__item" href="#">{I("list")}<span>Groceries</span><span class="mat-nav__badge mat-nav__badge--plain">8<span class="mat-sr-only"> items</span></span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("list")}<span>Holiday</span></a></li>
                        <li><a class="mat-nav__item" href="#">{I("list")}<span>Home</span></a></li>
                        <li class="mat-nav__group mat-nav__group--action" role="presentation">
                          <span class="mat-nav__group-title">Folders</span>
                          <button type="button" class="mat-nav__group-action" data-menu-toggle="demo-folders-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false" aria-label="Folder actions" title="Folder actions">{I("plus")}</button>
                        </li>
                        <li><a class="mat-nav__item" href="#">{I("folder")}<span>Projects</span></a></li>
                      </ul>
                    </nav>
                    <div class="mat-menu" id="demo-folders-menu" role="menu" aria-label="Folder actions" hidden>
                      <button type="button" class="mat-menu__item" role="menuitem">{I("folder")}<span>New folder</span></button>
                      <button type="button" class="mat-menu__item" role="menuitem">{I("sort")}<span>Sort folders</span></button>
                    </div>
                    """, "A list that the user can change has its action in the heading, at the right end, instead of a Manage or New item below the list: a gear (settings) to manage it, a plus to add to it. It is a link, or a button that opens a menu (data-menu-toggle) when there is more than one thing to do. It has a name for screen readers (aria-label); the row keeps its height and on a touch screen the target is 44 px.", Frame: "menu"),
                new("A submenu", $"""
                    <nav class="mat-nav__scroll" aria-label="Demo submenu">
                      <ul class="mat-nav__list">
                        <li class="mat-nav__branch is-open is-current">
                          <button type="button" class="mat-nav__item mat-nav__parent" aria-expanded="true" aria-controls="demo-sub-items" data-nav-branch="demo-items">
                            {I("list")}<span>Items</span>{I("chevron-down", "mat-nav__chev")}
                          </button>
                          <div class="mat-nav__sub" id="demo-sub-items">
                            <ul class="mat-nav__list">
                              <li><a class="mat-nav__item mat-nav__item--sub is-active" href="#" aria-current="page"><span>All items</span><span class="mat-nav__badge mat-nav__badge--plain">12<span class="mat-sr-only"> items</span></span></a></li>
                              <li><a class="mat-nav__item mat-nav__item--sub" href="#"><span>Needs review</span><span class="mat-nav__badge mat-nav__badge--warn">3<span class="mat-sr-only"> to review</span></span></a></li>
                              <li><a class="mat-nav__item mat-nav__item--sub" href="#"><span>Gallery</span></a></li>
                            </ul>
                          </div>
                        </li>
                        <li class="mat-nav__branch">
                          <button type="button" class="mat-nav__item mat-nav__parent" aria-expanded="false" aria-controls="demo-sub-settings" data-nav-branch="demo-settings">
                            {I("settings")}<span>Settings</span><span class="mat-nav__badge mat-nav__badge--error mat-nav__badge--sum">1<span class="mat-sr-only"> failed</span></span>{I("chevron-down", "mat-nav__chev")}
                          </button>
                          <div class="mat-nav__sub" id="demo-sub-settings">
                            <ul class="mat-nav__list">
                              <li><a class="mat-nav__item mat-nav__item--sub" href="#"><span>General</span></a></li>
                              <li><a class="mat-nav__item mat-nav__item--sub" href="#"><span>Backups</span><span class="mat-nav__badge mat-nav__badge--error">1<span class="mat-sr-only"> failed</span></span></a></li>
                            </ul>
                          </div>
                        </li>
                      </ul>
                    </nav>
                    """, "The parent is a button, not a page: it opens its children. The branch that has the current page is open when the page loads (is-open, is-current); the sum of the children's badges stands in for them while it is closed.", Frame: "menu"),
            ],
            [
                "A group heading can carry one icon action at its right end (`NavGroup.Action`: a link or a menu, `mat-nav__group--action`, `mat-nav__group-action`); it needs a heading and an `aria-label`.",
                "A group action that is a link to the current page is marked like a current item: `aria-current=\"page\"` (or `is-active`), the tint of the accent behind it and the accent on its icon.",
                "A submenu is one level deep; the children show no icon. If the section has a page of its own, that page is its first child.",
                "The user's choice (open or closed) is remembered per app; the branch with the current page is open whatever was saved.",
                "Arrow keys: up and down walk the visible items, right opens a parent or goes to its first child, left closes it or goes back to the parent.",
                "In the shell the menu is drawn by the partial `_Menu` from a `NavSet` (items, badges, children); an app does not write this markup by hand.",
            ]),

        new("switcher", "Menu switcher", "Navigation", "toggle",
            "More than one menu in an app, for example the everyday work and the administration: a switcher under the logo, at the foot above the user, or at the end of the menu, swaps the items. Menus can be grouped below a parent.",
            ["SHELL-15", "SHELL-01", "SHELL-03", "SHELL-13", "SHELL-14", "SHELL-19", "DIALOG-07"],
            [
                new("Work and Administration", $"""
                    <div id="switch-demo">
                      <div class="mat-nav__switch">
                        <button type="button" class="mat-switcher" data-menu-toggle="switch-demo-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false">
                          <span class="mat-switcher__icon">{I("briefcase")}</span>
                          <span class="mat-switcher__text"><small data-nav-label="Menu">Menu</small><span class="mat-switcher__name">Work</span></span>
                          {I("chevron-down", "mat-switcher__chev")}
                          <span class="mat-switcher__dot"></span>
                        </button>
                      </div>
                      <nav class="mat-nav__scroll" aria-label="Work" data-nav-panel="work">
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("dashboard")}<span>Dashboard</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("list")}<span>Items</span><span class="mat-nav__badge mat-nav__badge--plain">12<span class="mat-sr-only"> items</span></span></a></li>
                        </ul>
                      </nav>
                      <nav class="mat-nav__scroll" aria-label="Administration" data-nav-panel="admin" hidden>
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item" href="#">{I("users")}<span>Users</span><span class="mat-nav__badge mat-nav__badge--warn">3<span class="mat-sr-only"> invitations pending</span></span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("shield")}<span>Roles</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("clock")}<span>Audit log</span></a></li>
                        </ul>
                      </nav>
                    </div>
                    <div class="mat-menu" id="switch-demo-menu" role="menu" aria-label="Menus" data-nav-scope="switch-demo" hidden>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work">{I("briefcase")}<span>Work</span>{I("check", "mat-menu__check")}</button>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration" data-nav-attention>{I("shield")}<span>Administration</span><span class="mat-nav__badge mat-nav__badge--dot mat-nav__badge--warn"><span class="mat-sr-only">needs attention</span></span>{I("check", "mat-menu__check")}</button>
                    </div>
                    """, "Click the switcher and choose Administration. The dot on the switcher says that the other menu needs attention (an item with a warning or an error badge).", Frame: "menu"),
                new("At the foot, above the user", $"""
                    <div id="switch-foot-demo">
                      <nav class="mat-nav__scroll" aria-label="Work" data-nav-panel="work">
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("dashboard")}<span>Dashboard</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("list")}<span>Items</span><span class="mat-nav__badge mat-nav__badge--plain">12<span class="mat-sr-only"> items</span></span></a></li>
                        </ul>
                      </nav>
                      <nav class="mat-nav__scroll" aria-label="Administration" data-nav-panel="admin" hidden>
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item" href="#">{I("users")}<span>Users</span><span class="mat-nav__badge mat-nav__badge--warn">3<span class="mat-sr-only"> invitations pending</span></span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("shield")}<span>Roles</span></a></li>
                        </ul>
                      </nav>
                      <div class="mat-nav__foot">
                        <div class="mat-nav__switch mat-nav__switch--foot">
                          <button type="button" class="mat-switcher" data-menu-toggle="switch-foot-menu" data-menu-place="top" aria-haspopup="menu" aria-expanded="false">
                            <span class="mat-switcher__icon">{I("briefcase")}</span>
                            <span class="mat-switcher__text"><small data-nav-label="Menu">Menu</small><span class="mat-switcher__name">Work</span></span>
                            {I("chevron-up", "mat-switcher__chev")}
                            <span class="mat-switcher__dot"></span>
                          </button>
                        </div>
                        <div class="mat-nav__account">
                          <button type="button" class="mat-user" data-menu-toggle="switch-foot-user-menu" aria-haspopup="menu" aria-expanded="false">
                            <span class="mat-avatar" aria-hidden="true">AM</span>
                            <span class="mat-user__text"><span class="mat-user__name">Alex Morgan</span><span class="mat-user__role">Administrator</span></span>
                            {I("chevron-up", "mat-user__chev")}
                          </button>
                        </div>
                      </div>
                    </div>
                    <div class="mat-menu" id="switch-foot-menu" role="menu" aria-label="Menus" data-nav-scope="switch-foot-demo" hidden>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work">{I("briefcase")}<span>Work</span>{I("check", "mat-menu__check")}</button>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration" data-nav-attention>{I("shield")}<span>Administration</span><span class="mat-nav__badge mat-nav__badge--dot mat-nav__badge--warn"><span class="mat-sr-only">needs attention</span></span>{I("check", "mat-menu__check")}</button>
                    </div>
                    <div class="mat-menu" id="switch-foot-user-menu" role="menu" aria-label="Alex Morgan" hidden>
                      <a class="mat-menu__item" role="menuitem" href="#">{I("logout")}<span>Sign out</span></a>
                    </div>
                    """, "The same switcher as the first example, as the first thing in the foot of the menu, directly above the user. It opens upwards (data-menu-place=\"top\"). An app can have the switcher under the logo, here, or in both places: the buttons stay in step.", Frame: "menu"),
                new("Menus in groups", $"""
                    <div id="switch-tree-demo">
                      <div class="mat-nav__switch">
                        <button type="button" class="mat-switcher" data-menu-toggle="switch-tree-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false">
                          <span class="mat-switcher__icon">{I("briefcase")}</span>
                          <span class="mat-switcher__text"><small data-nav-label="Menu">Menu</small><span class="mat-switcher__name">Work</span></span>
                          {I("chevron-down", "mat-switcher__chev")}
                          <span class="mat-switcher__dot"></span>
                        </button>
                      </div>
                      <nav class="mat-nav__scroll" aria-label="Work" data-nav-panel="work">
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("dashboard")}<span>Dashboard</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("list")}<span>Items</span></a></li>
                        </ul>
                      </nav>
                      <nav class="mat-nav__scroll" aria-label="People" data-nav-panel="people" hidden>
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item" href="#">{I("users")}<span>Users</span><span class="mat-nav__badge mat-nav__badge--warn">3<span class="mat-sr-only"> invitations pending</span></span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("shield")}<span>Roles</span></a></li>
                        </ul>
                      </nav>
                      <nav class="mat-nav__scroll" aria-label="System" data-nav-panel="system" hidden>
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item" href="#">{I("settings")}<span>Settings</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("clock")}<span>Audit log</span></a></li>
                        </ul>
                      </nav>
                    </div>
                    <div class="mat-menu" id="switch-tree-menu" role="menu" aria-label="Menus" data-nav-scope="switch-tree-demo" hidden>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work">{I("briefcase")}<span>Work</span>{I("check", "mat-menu__check")}</button>
                      <button type="button" class="mat-menu__item mat-menu__parent" role="menuitem" aria-expanded="false" aria-controls="switch-tree-group" data-nav-group>
                        {I("shield")}<span>Administration</span><span class="mat-nav__badge mat-nav__badge--dot mat-nav__badge--warn mat-nav__badge--sum"><span class="mat-sr-only">needs attention</span></span>{I("chevron-down", "mat-menu__chev")}
                      </button>
                      <div class="mat-menu__sub" id="switch-tree-group" role="group" aria-label="Administration" hidden>
                        <button type="button" class="mat-menu__item mat-menu__item--sub" role="menuitemradio" aria-checked="false" data-nav-set="people" data-nav-title="People" data-nav-parent="Administration" data-nav-attention>{I("users")}<span>People</span><span class="mat-nav__badge mat-nav__badge--dot mat-nav__badge--warn"><span class="mat-sr-only">needs attention</span></span>{I("check", "mat-menu__check")}</button>
                        <button type="button" class="mat-menu__item mat-menu__item--sub" role="menuitemradio" aria-checked="false" data-nav-set="system" data-nav-title="System" data-nav-parent="Administration">{I("settings")}<span>System</span>{I("check", "mat-menu__check")}</button>
                      </div>
                    </div>
                    """, "Open the switcher, open Administration and choose People: the switcher then names the group above the menu. The parent is a button that only opens its menus; a closed parent carries the dot of the menus inside it that need attention.", Frame: "menu"),
                new("At the end of the menu", $"""
                    <div id="switch-end-demo">
                      <nav class="mat-nav__scroll" aria-label="Work" data-nav-panel="work">
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item mat-nav__item--primary" href="#">{I("plus")}<span>Add item</span></a></li>
                          <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("dashboard")}<span>Dashboard</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("list")}<span>Items</span><span class="mat-nav__badge mat-nav__badge--plain">12<span class="mat-sr-only"> items</span></span></a></li>
                        </ul>
                      </nav>
                      <nav class="mat-nav__scroll" aria-label="Administration" data-nav-panel="admin" hidden>
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item" href="#">{I("users")}<span>Users</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("shield")}<span>Roles</span></a></li>
                        </ul>
                      </nav>
                      <div class="mat-nav__switch mat-nav__switch--end">
                        <button type="button" class="mat-switcher" data-menu-toggle="switch-end-menu" data-menu-place="top" aria-haspopup="menu" aria-expanded="false">
                          <span class="mat-switcher__icon">{I("briefcase")}</span>
                          <span class="mat-switcher__text"><small data-nav-label="Menu">Menu</small><span class="mat-switcher__name">Work</span></span>
                          {I("chevron-up", "mat-switcher__chev")}
                          <span class="mat-switcher__dot" hidden></span>
                        </button>
                      </div>
                    </div>
                    <div class="mat-menu" id="switch-end-menu" role="menu" aria-label="Menus" data-nav-scope="switch-end-demo" hidden>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work">{I("briefcase")}<span>Work</span>{I("check", "mat-menu__check")}</button>
                      <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration">{I("shield")}<span>Administration</span>{I("check", "mat-menu__check")}</button>
                    </div>
                    """, "For an app whose menu starts with a main action (SHELL-19): the switcher sits at the end of the menu, directly above the foot, and its popup opens upwards (data-menu-place=\"top\"). It comes after the panels in the markup.", Frame: "menu"),
                new("With start pages", $"""
                    <div id="switch-pages-demo">
                      <div class="mat-nav__switch">
                        <button type="button" class="mat-switcher" data-menu-toggle="switch-pages-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false">
                          <span class="mat-switcher__icon">{I("briefcase")}</span>
                          <span class="mat-switcher__text"><small data-nav-label="Menu">Menu</small><span class="mat-switcher__name">Work</span></span>
                          {I("chevron-down", "mat-switcher__chev")}
                          <span class="mat-switcher__dot" hidden></span>
                        </button>
                      </div>
                      <nav class="mat-nav__scroll" aria-label="Work" data-nav-panel="work">
                        <ul class="mat-nav__list">
                          <li><a class="mat-nav__item is-active" href="#" aria-current="page">{I("dashboard")}<span>Dashboard</span></a></li>
                          <li><a class="mat-nav__item" href="#">{I("list")}<span>Items</span></a></li>
                        </ul>
                      </nav>
                    </div>
                    <div class="mat-menu" id="switch-pages-menu" role="menu" aria-label="Menus" data-nav-scope="switch-pages-demo" hidden>
                      <a class="mat-menu__item" href="#" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work">{I("briefcase")}<span>Work</span>{I("check", "mat-menu__check")}</a>
                      <a class="mat-menu__item" href="#" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration">{I("shield")}<span>Administration</span>{I("check", "mat-menu__check")}</a>
                    </div>
                    """, "For an app whose menus are areas with a page each: the entries are links (the start page of a menu, `href`), so choosing one loads that page and the page that loads shows its own menu. Here the links go nowhere; in the app the page of the Administration draws only the menu of the Administration, with its entry checked.", Frame: "menu"),
                new("With choices (the tenant)", $"""
                    <div id="switch-choices-demo">
                      <div class="mat-nav__switch mat-nav__switch--foot">
                        <button type="button" class="mat-switcher" data-menu-toggle="switch-choices-menu" data-menu-place="top" aria-haspopup="menu" aria-expanded="false">
                          <span class="mat-switcher__icon">{I("briefcase")}</span>
                          <span class="mat-switcher__text"><small data-nav-label="Menu">Menu</small><span class="mat-switcher__name">Work</span></span>
                          {I("chevron-up", "mat-switcher__chev")}
                          <span class="mat-switcher__dot" hidden></span>
                        </button>
                      </div>
                    </div>
                    <div class="mat-menu" id="switch-choices-menu" role="menu" aria-label="Menus" data-nav-scope="switch-choices-demo" hidden>
                      <a class="mat-menu__item" href="#" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work">{I("briefcase")}<span>Work</span>{I("check", "mat-menu__check")}</a>
                      <a class="mat-menu__item" href="#" role="menuitemradio" aria-checked="false" data-nav-set="account" data-nav-title="My account">{I("user")}<span>My account</span>{I("check", "mat-menu__check")}</a>
                      <button type="button" class="mat-menu__item mat-menu__parent" role="menuitem" aria-expanded="false" aria-controls="switch-choices-tenants" data-nav-group>
                        {I("store")}<span>Tenants</span>{I("chevron-down", "mat-menu__chev")}
                      </button>
                      <div class="mat-menu__sub" id="switch-choices-tenants" role="group" aria-label="Tenants" hidden>
                        <form class="mat-menu__form" method="get" action="#">
                          <button type="submit" class="mat-menu__item mat-menu__item--sub" role="menuitemradio" aria-checked="true"><span>Muster GmbH</span>{I("check", "mat-menu__check")}</button>
                        </form>
                        <form class="mat-menu__form" method="get" action="#">
                          <button type="submit" class="mat-menu__item mat-menu__item--sub" role="menuitemradio" aria-checked="false"><span>Beispiel AG</span>{I("check", "mat-menu__check")}</button>
                        </form>
                      </div>
                      <a class="mat-menu__item" href="#" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration">{I("shield")}<span>Administration</span>{I("check", "mat-menu__check")}</a>
                    </div>
                    """, "For an app that serves several tenants or organisations: the switcher sits at the foot above the user, the menus come first, the tenants are a closed parent with a button of a form each (it posts, so a tenant is never changed by a link; here the forms go nowhere), and the Administration stands last.", Frame: "menu"),
            ],
            [
                "The switcher appears only when an app has at least two menus; a menu the user may not use is not offered.",
                "When a page loads, the menu that has the current page is shown. Choosing a menu swaps the items: nothing is loaded.",
                "A menu can have a start page: its entry is a link (`<a href>`, `NavSet.Href`), so choosing it loads that page, which shows its own menu. The other pages carry no hidden panel of it.",
                "Choices (`ShellModel.Choices`, for example the tenant) are a closed parent (`data-nav-group`) with a form button each (`mat-menu__form`, `role=\"menuitemradio\"`, the current one checked); they stand between the menus and the menus that are `NavSet.Last`.",
                "The popup is a context menu drawn at the end of the page and names the element that holds the switcher and the panels (`data-nav-scope`).",
                "Under the logo, above the items, at the foot of the menu directly above the user, or both: the logo and the foot (user, theme button, version) stay where they are, and the switchers show the same menu.",
                "Or at the end of the menu, above the foot (`mat-nav__switch--end`, after the panels in the markup, the popup opens upwards): for a menu that starts with a main action.",
                "Menus can be grouped, one level deep: the parent entry is a button (`data-nav-group`, `aria-expanded`, `aria-controls`), not a menu; the menus below it carry `data-nav-parent`.",
                "The group with the current menu is open when the page loads, and choosing a menu opens its group.",
                "Arrow keys in the popup: up and down walk the visible entries, right opens a parent or goes to its first menu, left closes it or goes back to the parent.",
                "In the shell the popup is drawn by `_MenuSwitchPopup` from the `NavSet`s (a `NavSetParent` groups them); where the switcher sits is `ShellModel.SwitcherPlace`.",
            ]),

        new("toc", "On this page", "Navigation", "list",
            "A long page with an index beside it, as in Bootstrap's documentation: one click jumps to a section, and the section in view is marked.",
            ["SHELL-16", "MOBILE-02", "DIALOG-03", "A11Y-03"],
            [
                new("The index", """
                    <nav class="mat-toc" aria-label="On this page">
                      <p class="mat-toc__title">On this page</p>
                      <ul class="mat-toc__list">
                        <li><a class="mat-toc__link mat-toc__link--group" href="#group-forms">Forms</a>
                          <ul class="mat-toc__list">
                            <li><a class="mat-toc__link" href="#fields">Fields</a></li>
                            <li><a class="mat-toc__link is-active" href="#choices" aria-current="location">Switches, checks and segments</a></li>
                            <li><a class="mat-toc__link" href="#form-layout">Form layout</a></li>
                          </ul>
                        </li>
                        <li><a class="mat-toc__link mat-toc__link--group" href="#group-lists-and-data">Lists and data</a></li>
                      </ul>
                    </nav>
                    """, "Groups and sections, the one in view marked (is-active, aria-current=\"location\"). The links lead to the sections of this very page: try them."),
                new("The layout with the index beside the content", """
                    <div class="mat-with-toc">
                      <div class="mat-with-toc__main">
                        <section id="intro" class="mat-anchor"><h2>Introduction</h2>...</section>
                        <section id="usage" class="mat-anchor"><h2>Usage</h2>...</section>
                      </div>
                      <aside class="mat-toc-aside">
                        <nav class="mat-toc" aria-label="On this page" data-scrollspy>
                          <p class="mat-toc__title">On this page</p>
                          <ul class="mat-toc__list">
                            <li><a class="mat-toc__link" href="#intro">Introduction</a></li>
                            <li><a class="mat-toc__link" href="#usage">Usage</a></li>
                          </ul>
                        </nav>
                      </aside>
                    </div>
                    <!-- below 1100 px the aside is hidden and an icon button in the header bar (class mat-topbar__index, with a name and a tooltip) opens the same index in a dialog (full screen on a phone); it is the last thing of the mat-topbar__actions after the breadcrumb -->
                    <dialog class="mat-dialog mat-dialog--sm" id="toc-dialog" aria-labelledby="toc-dialog-title">
                      <div class="mat-dialog__form">
                        <div class="mat-dialog__head"><h2 class="mat-dialog__title" id="toc-dialog-title">On this page</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">...</button></div>
                        <div class="mat-dialog__body">
                          <nav class="mat-toc" aria-label="On this page" data-scrollspy>
                            <ul class="mat-toc__list">
                              <li><a class="mat-toc__link" href="#intro">Introduction</a></li>
                              <li><a class="mat-toc__link" href="#usage">Usage</a></li>
                            </ul>
                          </nav>
                        </div>
                      </div>
                    </dialog>
                    """, "From 1100 px the index is a sticky column beside the content. Below that, an icon at the right end of the header bar (SHELL-17) opens the same list in a dialog, which closes when a link is chosen.", Live: false),
            ],
            [
                "`data-scrollspy` on the index marks the link of the section that is at the top of the screen; it works for the index beside the content and the one in the dialog at the same time.",
                "Give the sections `mat-anchor` so that they stop below the sticky top bar when a link jumps to them.",
                "Use it for long pages: documentation, a long settings page, a long form with sections. A short page needs no index.",
            ]),

        new("alerts", "Alerts", "Feedback", "alert",
            "A message inside the page. It stays until it is dismissed or the page changes.",
            ["COLOR-03", "COLOR-04", "A11Y-04", "SHELL-12"],
            [
                new("The four tones", $"""
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-alert mat-alert--info">{I("info")}<div class="mat-alert__body"><strong>Information.</strong> The accent changes with the app; the status colours never do.</div></div>
                      <div class="mat-alert mat-alert--success">{I("check-circle")}<div class="mat-alert__body"><strong>Saved.</strong> The backup finished in 14 seconds.</div></div>
                      <div class="mat-alert mat-alert--warning">{I("alert")}<div class="mat-alert__body"><strong>Almost full.</strong> 92 % of the storage is used.</div></div>
                      <div class="mat-alert mat-alert--danger">{I("x-circle")}<div class="mat-alert__body"><strong>Failed.</strong> The mail server did not answer.</div><button type="button" class="mat-alert__close" data-dismiss aria-label="Dismiss">{I("x")}</button></div>
                    </div>
                    """, "Every alert has an icon and a lead word, so colour is never the only signal. The last one can be dismissed."),
            ],
            ["An error that belongs to a field is shown at the field (mat-error), not in an alert."]),

        new("toasts", "Toasts", "Feedback", "bell",
            "A short message that goes away by itself: what just happened after a save or an action.",
            ["SHELL-12", "A11Y-04"],
            [
                new("From a script", "",
                    "Four tones: success, warning, danger, or none for information.",
                    Js: "MatUi.toast('Saved.', { tone: 'success' });", Demo: "toast"),
                new("After a redirect", "<span data-toast=\"Saved.\" data-tone=\"success\" hidden></span>",
                    "A page that redirects after a save puts this element on the next page; the script shows the toast and removes the element.",
                    Live: false),
            ],
            [
                "Toasts stack at the bottom, are announced politely (role=status) and stop after a few seconds; hovering keeps them.",
                "While a dialog is open the toasts are shown inside the topmost one and go back to the page when it closes: a modal dialog is in the top layer of the browser, so a message outside of it would be hidden behind it whatever its `z-index`. Nothing to do for the app.",
            ]),

        new("dialogs", "Dialogs", "Feedback", "message-square",
            "Confirmations, alerts and inputs are always dialogs. On a phone every dialog is full screen with the X at the top right.",
            ["DIALOG-01", "DIALOG-03", "DIALOG-04", "DIALOG-05", "DIALOG-06", "FORM-11"],
            [
                new("Confirm what cannot be undone", $"""
                    <button type="button" class="mat-btn mat-btn--danger" data-confirm="Delete “Quarterly report”? This cannot be undone." data-confirm-ok="Delete">{I("trash")}<span>Delete</span></button>
                    """, "One attribute on a button, a link or a form. A destructive confirmation puts Cancel first, a space, then the red Delete."),
                new("A dialog of your own", $"""
                    <button type="button" class="mat-btn mat-btn--primary" data-dialog-open="demo-rename">{I("edit")}<span>Rename</span></button>
                    <dialog class="mat-dialog mat-dialog--sm" id="demo-rename" aria-labelledby="demo-rename-title">
                      <form class="mat-dialog__form" method="dialog">
                        <div class="mat-dialog__head">{I("edit")}<h2 class="mat-dialog__title" id="demo-rename-title">Rename</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{I("x")}</button></div>
                        <div class="mat-dialog__body"><div class="mat-field"><label class="mat-label" for="demo-name">Name</label><input class="mat-input" id="demo-name" name="name" value="Quarterly report" /></div></div>
                        <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" class="mat-btn mat-btn--primary">Save</button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button></div></div>
                      </form>
                    </dialog>
                    """, "Head with icon, title and the X; body; foot with the buttons, positive first. Sizes: mat-dialog--sm, none (medium), --lg, --xl."),
                new("Confirm, alert and input from a script", "",
                    "They return a promise: confirm resolves to true or false, prompt to the text or null, alert to nothing. window.confirm, alert and prompt are never used (DIALOG-01).",
                    Js: "const ok = await MatUi.confirm('Restart the service?', { ok: 'Restart' });\nawait MatUi.alert('The backup is running.', { title: 'Backup' });\nconst name = await MatUi.prompt('Name of the folder', { value: 'New folder' });", Demo: "dialogs"),
            ],
            [
                "Escape and a click on the backdrop close a dialog, unless it holds unsaved input; focus returns to the button that opened it.",
                "While a dialog is open the page behind it does not scroll.",
                "A dialog that needs more than a screen of fields is a page of its own, not a dialog.",
            ]),

        new("tooltip", "Tooltip", "Feedback", "help-circle",
            "A name for an icon on a pointer device. Never the only place for information: a touch screen does not show it.",
            ["ICON-03", "MOBILE-04"],
            [
                new("An icon button with a tooltip", $"""
                    <div class="mat-row">
                      <button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Copy" data-tip="Copy">{I("copy")}</button>
                      <button type="button" class="mat-btn mat-btn--secondary mat-btn--icon" aria-label="Download" data-tip="Download">{I("download")}</button>
                    </div>
                    """, "Hover or focus a button. The aria-label is the real name; data-tip only shows it."),
            ],
            ["On a device without hover the tooltip is not drawn at all (hover: none)."]),

        new("skeleton", "Skeleton", "Feedback", "clock",
            "A placeholder while content loads, so the page does not jump.",
            ["A11Y-05"],
            [
                new("Lines in a card", """
                    <div class="mat-card"><div class="mat-card__body mat-stack mat-stack--sm" aria-busy="true">
                      <span class="mat-skeleton"></span>
                      <span class="mat-skeleton"></span>
                      <span class="mat-skeleton"></span>
                    </div></div>
                    """, "With reduced motion the shimmer stops."),
            ],
            ["Set aria-busy=\"true\" on the container while it loads and remove it when the content is there."]),
    ];

    /// <summary>The order inside a group where it matters (the forms read like a manual); everything else keeps the order it is written in.</summary>
    private static readonly string[] Order =
    [
        "buttons", "button-group", "dropdown", "button-row",
        "fields", "input-types", "selects", "checks", "choices", "range", "slider", "number-stepper", "input-groups", "special-inputs", "rich-select", "checkbox-select", "tag-input", "date-picker", "time-picker", "colour-picker", "code-input", "floating-labels", "form-layout", "form-grid", "validation",
        "toolbar", "table", "table-groups", "pager", "gallery",
        "typography", "cards", "list-group", "accordion", "carousel", "avatars", "stat", "pills", "kv", "meter", "empty", "code",
        "tabs", "menu", "breadcrumb", "topbar", "steps", "navmenu", "switcher", "toc", "footer",
        "alerts", "toasts", "dialogs", "popover", "tooltip", "spinner", "skeleton",
        "breakpoints", "grid-cols", "stack-row-grid",
        "u-spacing", "u-display", "u-flex", "u-text", "u-colour", "u-borders", "u-sizing",
        "tree",
    ];

    /// <summary>Every component, by group and in reading order.</summary>
    public static IReadOnlyList<ComponentDoc> All { get; } = Sorted(
    [
        .. Core.Select(Extend),
        .. FormComponents.All,
        .. PickerComponents.All,
        .. CodeInputComponents.All,
        .. ContentComponents.All,
        .. LayoutComponents.All,
        .. ScheduleComponents.All,
        .. GroupComponents.All,
        .. ListComponents.All,
        .. TreeComponents.All,
    ]);

    /// <summary>A component with the examples that other files add to it.</summary>
    private static ComponentDoc Extend(ComponentDoc doc)
    {
        var more = ContentComponents.Extensions.Where(e => e.Id == doc.Id).ToList();
        return more.Count == 0
            ? doc
            : doc with
            {
                Name = more.Select(e => e.Name).LastOrDefault(n => n is not null) ?? doc.Name,
                Summary = more.Select(e => e.Summary).LastOrDefault(s => s is not null) ?? doc.Summary,
                Examples = [.. doc.Examples, .. more.SelectMany(e => e.Examples)],
                Behaviour = [.. doc.Behaviour, .. more.SelectMany(e => e.Behaviour)],
                Rules = [.. doc.Rules, .. more.SelectMany(e => e.Rules ?? [])],
            };
    }

    private static IReadOnlyList<ComponentDoc> Sorted(IEnumerable<ComponentDoc> docs)
        => docs.Select((doc, index) => (doc, index))
            .OrderBy(x => Groups.ToList().IndexOf(x.doc.Group))
            .ThenBy(x => Array.IndexOf(Order, x.doc.Id) is var p and >= 0 ? p : int.MaxValue)
            .ThenBy(x => x.index)
            .Select(x => x.doc)
            .ToList();
}

