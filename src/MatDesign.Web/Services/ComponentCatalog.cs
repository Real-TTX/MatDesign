namespace MatDesign.Web.Services;

/// <summary>
/// One example of a component. <see cref="Html"/> is rendered live and shown as source, so the example and what a developer copies can never
/// differ. <see cref="Js"/> is the script call that does the same (for dialogs and toasts); <see cref="Demo"/> adds a "Try it" button.
/// </summary>
public sealed record ComponentExample(string Title, string Html, string? Note = null, string? Js = null, string? Demo = null, bool Live = true);

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
/// The catalogue of controls. The markup is the markup of <c>MatUi</c> as it stands in the CSS and the script; a unit test parses every example
/// (cell labels, no inline styles, icons that exist, ids that are unique) so that the documentation cannot drift from the toolkit.
/// </summary>
public static class ComponentCatalog
{
    public static readonly IReadOnlyList<string> Groups = ["Actions", "Forms", "Lists and data", "Content", "Navigation", "Feedback"];

    public static ComponentDoc? Get(string id) => All.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static string I(string name)
        => $"<svg class=\"mat-icon\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{name}\"></use></svg>";

    public static IReadOnlyList<ComponentDoc> All { get; } =
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
            ],
            [
                "The knob of a switch is always white (--mat-thumb), in light and dark.",
                "A switch is not used for something that needs a Save button.",
            ]),

        new("form-layout", "Form layout", "Forms", "template",
            "A card with a two-column grid, full-width fields where needed, a dependent field and the buttons under it.",
            ["FORM-01", "FORM-04", "FORM-07", "FORM-10", "LIST-01"],
            [
                new("An edit form", $"""
                    <form class="mat-form" method="get" action="/Components/Detail" novalidate>
                      <input type="hidden" name="id" value="form-layout" />
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
            "Search, Filter and Sort are three buttons, each opens a dialog. The toolbar is always above the list or the gallery.",
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

                      <form id="tb-form" method="get" action="/Components/Detail" hidden><input type="hidden" name="id" value="toolbar" /></form>

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
            ],
            [
                "A tool in effect is visible: the button is highlighted, the filter shows its count, the search and the sort show their value, and a chip with an X removes each one.",
                "Press / on the page to open the search dialog (data-shortcut=\"/\"); it is not triggered while typing in a field.",
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
                "A tile that is selected carries is-selected.",
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
                      <a class="mat-card" href="#"><div class="mat-card__body"><strong>A card that is a link</strong><p class="mat-muted">It lifts a little on hover.</p></div></a>
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
            ],
            ["A block scrolls sideways instead of wrapping; it never breaks the layout on a phone."]),

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
                    <button type="button" class="mat-btn mat-btn--secondary" data-menu-toggle="demo-menu" aria-haspopup="menu" aria-expanded="false">{I("more-horizontal")}<span>Actions</span></button>
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
            ["Toasts stack at the bottom, are announced politely (role=status) and stop after a few seconds; hovering keeps them."]),

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
}
