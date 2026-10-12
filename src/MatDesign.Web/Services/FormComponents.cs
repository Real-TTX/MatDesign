namespace MatDesign.Web.Services;

/// <summary>
/// The form controls of the catalogue, in the order of a manual: input types and sizes, selects, checks and radios, ranges, input groups,
/// special inputs, floating labels, layouts and validation. Like the others they are rendered live and shown as source from one string.
/// </summary>
internal static class FormComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("input-types", "Input types and sizes", "Forms", "type",
            "The right type for the value gives a phone the right keyboard and the browser the right check. Three sizes, and values that cannot be changed.",
            ["FORM-03", "FORM-12", "BUTTON-05", "A11Y-01"],
            [
                new("Types", """
                    <div class="mat-form__grid">
                      <div class="mat-field"><label class="mat-label" for="it-text">Text</label><input class="mat-input" id="it-text" name="text" value="Quarterly report" autocomplete="off" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-mail">E-mail</label><input class="mat-input" id="it-mail" name="mail" type="email" value="alex@example.com" autocomplete="email" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-num">Number</label><input class="mat-input mat-mono" id="it-num" name="num" type="number" value="8030" min="1" max="65535" inputmode="numeric" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-tel">Phone</label><input class="mat-input" id="it-tel" name="tel" type="tel" value="+49 30 1234567" autocomplete="tel" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-url">Address</label><input class="mat-input" id="it-url" name="url" type="url" value="https://example.com" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-search">Search</label><input class="mat-input" id="it-search" name="q" type="search" placeholder="Name or number" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-date">Date</label><input class="mat-input" id="it-date" name="date" type="date" value="2026-09-28" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-time">Time</label><input class="mat-input" id="it-time" name="time" type="time" value="09:30" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-when">Date and time</label><input class="mat-input" id="it-when" name="when" type="datetime-local" value="2026-09-28T09:30" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-colour">Colour</label><input class="mat-input" id="it-colour" name="colour" type="color" value="#2f6feb" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-pick">With suggestions</label><input class="mat-input" id="it-pick" name="pick" list="it-pick-list" placeholder="Start typing" /><datalist id="it-pick-list"><option value="Finance"></option><option value="Contracts"></option><option value="Family"></option></datalist></div>
                      <div class="mat-field mat-field--full"><label class="mat-label" for="it-area">Text area</label><textarea class="mat-textarea" id="it-area" name="area" rows="3">Numbers are final after the review.</textarea></div>
                    </div>
                    """, "Use the type that fits the value: e-mail, phone, number, date. Add autocomplete and inputmode where they help (FORM-12)."),
                new("Sizes", """
                    <div class="mat-stack">
                      <div class="mat-field"><label class="mat-label" for="it-sm">Small</label><input class="mat-input mat-input--sm" id="it-sm" name="sm" value="For a table row or a toolbar" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-md">Default</label><input class="mat-input" id="it-md" name="md" value="For every form" /></div>
                      <div class="mat-field"><label class="mat-label" for="it-lg">Large</label><input class="mat-input mat-input--lg" id="it-lg" name="lg" value="For the main field of a page" /></div>
                    </div>
                    """, "On a touch screen every size grows by 4 px, so nothing a finger has to hit is smaller than 36 px (BUTTON-05)."),
                new("Read-only, plain text and disabled", """
                    <div class="mat-stack">
                      <div class="mat-field"><label class="mat-label" for="it-ro">Read-only</label><input class="mat-input" id="it-ro" name="ro" value="mattiverse-matdock" readonly /><span class="mat-hint">It is sent with the form and can be selected.</span></div>
                      <div class="mat-field"><label class="mat-label" for="it-plain">Plain text</label><input class="mat-input mat-input--plain" id="it-plain" name="plain" value="Administrator" readonly /></div>
                      <div class="mat-field"><label class="mat-label" for="it-off">Disabled</label><input class="mat-input" id="it-off" name="off" value="Not available now" disabled /><span class="mat-hint">It is not sent and cannot be focused.</span></div>
                      <fieldset class="mat-fieldset" disabled>
                        <legend class="mat-legend">A disabled group</legend>
                        <div class="mat-form__grid">
                          <div class="mat-field"><label class="mat-label" for="it-fs1">First</label><input class="mat-input" id="it-fs1" name="fs1" value="One" /></div>
                          <div class="mat-field"><label class="mat-label" for="it-fs2">Second</label><select class="mat-select" id="it-fs2" name="fs2"><option>Two</option></select></div>
                        </div>
                      </fieldset>
                    </div>
                    """),
            ],
            [
                "Labels sit above the field and point to it with `for`; a placeholder is an example of a value, never the label.",
                "A field that is not needed right now is read-only (it still counts and is sent) or disabled (it does not); a disabled field says why in its hint.",
                "`inputmode` and `autocomplete` are the cheap way to a good phone keyboard and to a form that fills itself.",
            ]),

        new("selects", "Select", "Forms", "list",
            "A choice from a list. A select for more than five options, radios for fewer; a list shown open or with several choices when the user picks many.",
            ["FORM-03", "FORM-12", "BUTTON-05", "MOBILE-04"],
            [
                new("One choice", """
                    <div class="mat-stack">
                      <div class="mat-field"><label class="mat-label" for="sel-one">Category</label><select class="mat-select" id="sel-one" name="category"><option>Finance</option><option>Contracts</option><option selected>Family</option></select></div>
                      <div class="mat-field">
                        <label class="mat-label" for="sel-groups">Server</label>
                        <select class="mat-select" id="sel-groups" name="server">
                          <optgroup label="Europe"><option>Frankfurt</option><option>Amsterdam</option></optgroup>
                          <optgroup label="America"><option>Virginia</option><option>Oregon</option></optgroup>
                        </select>
                        <span class="mat-hint">Options can be grouped.</span>
                      </div>
                      <div class="mat-field"><label class="mat-label" for="sel-off">Disabled</label><select class="mat-select" id="sel-off" name="off" disabled><option>Not available</option></select></div>
                    </div>
                    """, "On a phone the browser draws its own picker for a select: that is what the user knows."),
                new("Sizes", """
                    <div class="mat-stack">
                      <select class="mat-select mat-select--sm" id="sel-sm" name="sm" aria-label="Small"><option>Small</option></select>
                      <select class="mat-select" id="sel-md" name="md" aria-label="Default"><option>Default</option></select>
                      <select class="mat-select mat-select--lg" id="sel-lg" name="lg" aria-label="Large"><option>Large</option></select>
                    </div>
                    """),
                new("Several choices", """
                    <div class="mat-form__grid">
                      <div class="mat-field"><label class="mat-label" for="sel-multi">Labels (several)</label><select class="mat-select" id="sel-multi" name="labels" multiple><option selected>Urgent</option><option>Billing</option><option selected>Customer</option><option>Internal</option><option>Archive</option></select><span class="mat-hint">Ctrl or Cmd for more than one.</span></div>
                      <div class="mat-field"><label class="mat-label" for="sel-open">Open list</label><select class="mat-select" id="sel-open" name="open" size="4"><option>Daily</option><option selected>Weekly</option><option>Monthly</option><option>Yearly</option></select></div>
                    </div>
                    """, "For many choices a group of checks (see Checks and radios) is easier to use than a multiple select."),
            ],
            [
                "A select needs a label and an id; without a visible label it has an `aria-label` (a select in a toolbar or in a group).",
                "The first option is a real choice, not \"Please select\": a required select shows its message when it is left empty.",
            ]),

        new("checks", "Checks and radios", "Forms", "check-square",
            "Several yes-or-no choices (checks) or exactly one of a few (radios), as groups with a legend; buttons that toggle; cards to choose from.",
            ["FORM-03", "FORM-12", "FORM-08", "A11Y-02", "MOBILE-04"],
            [
                new("Checks and radios in groups", """
                    <div class="mat-grid">
                      <fieldset class="mat-field mat-fieldset">
                        <legend class="mat-legend">Tell me when</legend>
                        <div class="mat-checks">
                          <label class="mat-check"><input type="checkbox" id="chk-a" name="notify" value="failed" checked /><span class="mat-check__text"><strong>A backup failed</strong><small class="mat-muted">Sent at once.</small></span></label>
                          <label class="mat-check"><input type="checkbox" id="chk-b" name="notify" value="update" /><span class="mat-check__text">A new version is available</span></label>
                          <label class="mat-check"><input type="checkbox" id="chk-c" name="notify" value="summary" disabled /><span class="mat-check__text">Every week (not available)</span></label>
                        </div>
                      </fieldset>
                      <fieldset class="mat-field mat-fieldset">
                        <legend class="mat-legend">Backup</legend>
                        <div class="mat-checks">
                          <label class="mat-check"><input type="radio" id="chk-r1" name="backup" value="daily" checked /><span class="mat-check__text">Every day</span></label>
                          <label class="mat-check"><input type="radio" id="chk-r2" name="backup" value="weekly" /><span class="mat-check__text">Every week</span></label>
                          <label class="mat-check"><input type="radio" id="chk-r3" name="backup" value="never" /><span class="mat-check__text">Never</span></label>
                        </div>
                      </fieldset>
                    </div>
                    """, "A group is a fieldset with a legend: a screen reader says the legend with every choice. Radios of one group share a name."),
                new("In a row, and the state in between", """
                    <div class="mat-stack">
                      <fieldset class="mat-field mat-fieldset">
                        <legend class="mat-legend">Days</legend>
                        <div class="mat-checks mat-checks--inline">
                          <label class="mat-check"><input type="checkbox" id="chk-mon" name="days" value="mon" checked /><span class="mat-check__text">Mon</span></label>
                          <label class="mat-check"><input type="checkbox" id="chk-tue" name="days" value="tue" checked /><span class="mat-check__text">Tue</span></label>
                          <label class="mat-check"><input type="checkbox" id="chk-wed" name="days" value="wed" /><span class="mat-check__text">Wed</span></label>
                        </div>
                      </fieldset>
                      <label class="mat-check"><input type="checkbox" id="chk-all" name="all" data-indeterminate /><span class="mat-check__text"><strong>Select all</strong><small class="mat-muted">Some days are selected: the check is in between.</small></span></label>
                    </div>
                    """),
                new("Toggle buttons", $"""
                    <div class="mat-stack">
                      <div class="mat-row">
                        <input type="checkbox" class="mat-btn-check" id="chk-t1" name="bold" checked /><label class="mat-btn mat-btn--secondary" for="chk-t1">{I("check")}<span>Active</span></label>
                        <input type="checkbox" class="mat-btn-check" id="chk-t2" name="mine" /><label class="mat-btn mat-btn--secondary" for="chk-t2"><span>Only mine</span></label>
                        <input type="checkbox" class="mat-btn-check" id="chk-t3" name="locked" disabled /><label class="mat-btn mat-btn--secondary" for="chk-t3"><span>Locked</span></label>
                      </div>
                      <div class="mat-row" role="radiogroup" aria-label="Period">
                        <input type="radio" class="mat-btn-check" id="chk-p1" name="period" value="day" /><label class="mat-btn mat-btn--secondary" for="chk-p1"><span>Day</span></label>
                        <input type="radio" class="mat-btn-check" id="chk-p2" name="period" value="week" checked /><label class="mat-btn mat-btn--secondary" for="chk-p2"><span>Week</span></label>
                        <input type="radio" class="mat-btn-check" id="chk-p3" name="period" value="month" /><label class="mat-btn mat-btn--secondary" for="chk-p3"><span>Month</span></label>
                      </div>
                    </div>
                    """, "A check or a radio drawn as a button, for a filter or a view. For a few exclusive choices that apply at once, the segmented control is the same thing."),
                new("Cards to choose from", """
                    <fieldset class="mat-field mat-fieldset">
                      <legend class="mat-legend">Plan</legend>
                      <div class="mat-choices">
                        <label class="mat-choice"><input type="radio" id="chk-c1" name="plan" value="small" /><span class="mat-choice__text"><strong>Small</strong><small>One user, 10 GB</small></span></label>
                        <label class="mat-choice"><input type="radio" id="chk-c2" name="plan" value="family" checked /><span class="mat-choice__text"><strong>Family</strong><small>Five users, 200 GB</small></span></label>
                        <label class="mat-choice"><input type="radio" id="chk-c3" name="plan" value="team" /><span class="mat-choice__text"><strong>Team</strong><small>Unlimited users, 2 TB</small></span></label>
                      </div>
                    </fieldset>
                    """, "For three or four choices that need a sentence of explanation each."),
            ],
            [
                "Checks are saved with the form; a switch applies at once (see Switches, checks and segments). Do not mix them in one group.",
                "More than five radios become a select; more than five checks are fine in a scrolling group with a search.",
                "A check or a radio is at least 36 px high with its label, and the whole label is the target, not only the box.",
            ]),

        new("range", "Range", "Forms", "sliders",
            "A value on a scale when the exact number does not matter: volume, brightness, retention in steps.",
            ["FORM-12", "FORM-03", "A11Y-02", "COLOR-04"],
            [
                new("A slider with its value", """
                    <div class="mat-stack">
                      <div class="mat-field">
                        <label class="mat-label" for="rng-vol">Volume</label>
                        <div class="mat-range-row"><input class="mat-range" type="range" id="rng-vol" name="volume" min="0" max="100" value="40" data-output="rng-vol-out" /><output class="mat-range-out" id="rng-vol-out" for="rng-vol">40</output></div>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="rng-days">Keep backups for (days)</label>
                        <div class="mat-range-row"><input class="mat-range" type="range" id="rng-days" name="days" min="7" max="365" step="7" value="28" data-output="rng-days-out" /><output class="mat-range-out" id="rng-days-out" for="rng-days">28</output></div>
                        <span class="mat-hint">In steps of a week.</span>
                      </div>
                      <div class="mat-field"><label class="mat-label" for="rng-off">Disabled</label><input class="mat-range" type="range" id="rng-off" name="off" value="70" disabled /></div>
                    </div>
                    """, "The filled part of the track and the value next to the slider are drawn by the script from the value (data-output). The arrow keys move it by one step, Page Up and Page Down by more."),
            ],
            [
                "Show the value: a slider alone does not say what it is set to. For an exact number use a number field with the slider as a help.",
                "The track is the accent colour in the filled part and a neutral grey in the rest, so it never relies on a colour that means something.",
                "A value bubble above the thumb, marks, a unit and a range with two ends are the slider below (FORM-17); the range here stays for the plain case.",
            ]),

        new("slider", "Slider and range slider", "Forms", "sliders",
            "A value on a scale (a slider) or a from and to (a range slider): a bubble with the value, marks, a unit, thumbs that cannot cross. Native range inputs underneath, so a form posts them and a screen reader reads them.",
            ["FORM-17", "FORM-12", "FORM-03", "FORM-08", "A11Y-01", "A11Y-02", "MOBILE-04", "COLOR-04"],
            [
                new("A slider with its value", """
                    <div class="mat-stack">
                      <div class="mat-field">
                        <div class="mat-field__meta"><label class="mat-label" for="sl-vol">Volume</label><output class="mat-range-out" id="sl-vol-out" for="sl-vol">40 %</output></div>
                        <div class="mat-slider" data-slider data-format="{0} %" data-output="sl-vol-out">
                          <input class="mat-slider__input" type="range" id="sl-vol" name="volume" min="0" max="100" step="1" value="40" />
                        </div>
                      </div>
                      <div class="mat-field">
                        <div class="mat-field__meta"><label class="mat-label" for="sl-bright">Brightness</label><output class="mat-range-out" id="sl-bright-out" for="sl-bright">65 %</output></div>
                        <div class="mat-slider" data-slider data-format="{0} %" data-output="sl-bright-out" data-bubble="always">
                          <input class="mat-slider__input" type="range" id="sl-bright" name="brightness" min="0" max="100" step="5" value="65" />
                        </div>
                        <span class="mat-hint">The bubble with the value is always there (data-bubble="always"); in the first slider it shows while the thumb is dragged or has the focus.</span>
                      </div>
                    </div>
                    """, "Drag the thumb, press on the track (the thumb goes there), or use the keys: the arrows move it by one step, Page Up and Page Down by a tenth of the scale, Home and End to the ends. The minimum and the maximum are labelled at the ends; data-labels=\"none\" takes them away."),
                new("Marks, labels and a unit", """
                    <div class="mat-stack">
                      <div class="mat-field">
                        <div class="mat-field__meta"><label class="mat-label" for="sl-disk">Storage</label><output class="mat-range-out" id="sl-disk-out" for="sl-disk">40 GB</output></div>
                        <div class="mat-slider" data-slider data-format="{0} GB" data-output="sl-disk-out" data-bubble="always" data-ticks data-tick-labels="3">
                          <input class="mat-slider__input" type="range" id="sl-disk" name="disk" min="10" max="100" step="10" value="40" />
                        </div>
                        <span class="mat-hint">A mark at every step (data-ticks), a label under every third (data-tick-labels).</span>
                      </div>
                      <div class="mat-field">
                        <div class="mat-field__meta"><label class="mat-label" for="sl-keep">Keep backups for</label><output class="mat-range-out" id="sl-keep-out" for="sl-keep">14 days</output></div>
                        <div class="mat-slider" data-slider data-format="{0} days" data-output="sl-keep-out" data-ticks data-tick-labels="1">
                          <input class="mat-slider__input" type="range" id="sl-keep" name="keep" min="7" max="28" step="7" value="14" />
                        </div>
                      </div>
                    </div>
                    """, "data-format is a template for every value that is shown (the bubble, the labels, the output) and for what a screen reader says (aria-valuetext): {0} stands for the number, written for the language of the page. data-ticks=\"10\" divides the scale in ten parts."),
                new("Sizes and disabled", """
                    <div class="mat-stack">
                      <div class="mat-slider mat-slider--sm" data-slider data-labels="none"><input class="mat-slider__input" type="range" id="sl-sm" name="sm" aria-label="Small slider" min="0" max="100" value="30" /></div>
                      <div class="mat-slider" data-slider data-labels="none"><input class="mat-slider__input" type="range" id="sl-md" name="md" aria-label="Default slider" min="0" max="100" value="50" /></div>
                      <div class="mat-slider mat-slider--lg" data-slider data-labels="none"><input class="mat-slider__input" type="range" id="sl-lg" name="lg" aria-label="Large slider" min="0" max="100" value="70" /></div>
                      <div class="mat-field">
                        <label class="mat-label" for="sl-off">Disabled</label>
                        <div class="mat-slider" data-slider data-format="{0} %"><input class="mat-slider__input" type="range" id="sl-off" name="off" min="0" max="100" value="70" disabled /></div>
                        <span class="mat-hint">It is not sent and cannot be focused.</span>
                      </div>
                    </div>
                    """, "The thumb is a target of 44 px in every size but the small one, which is a compact control (36 px, MOBILE-04); what is seen is a knob of 16, 20 or 26 px."),
                new("A range slider: from and to", """
                    <div class="mat-field">
                      <div class="mat-field__meta"><label class="mat-label" for="sl-price-from">Price</label><output class="mat-range-out" id="sl-price-out">From 20 € to 80 €</output></div>
                      <div class="mat-slider mat-slider--range" data-slider data-min-gap="10" data-format="{0} €" data-range-format="From {0} to {1}" data-output="sl-price-out" data-bubble="always">
                        <input class="mat-slider__input" type="range" id="sl-price-from" name="from" min="0" max="200" step="5" value="20" />
                        <input class="mat-slider__input" type="range" id="sl-price-to" name="to" min="0" max="200" step="5" value="80" />
                      </div>
                    </div>
                    """, "Two native inputs in one wrapper, from and to, and one track. The thumbs cannot cross and stay at least data-min-gap apart (here 10 €); a press on the track moves the nearest thumb, and the thumb that was moved last is on top. Drag one thumb up to the other: the two bubbles become one."),
                new("Time of day", """
                    <div class="mat-field">
                      <div class="mat-field__meta"><label class="mat-label" for="sl-hours-from">Opening hours</label><output class="mat-range-out" id="sl-hours-out">8 h – 18 h</output></div>
                      <div class="mat-slider mat-slider--range" data-slider data-min-gap="1" data-format="{0} h" data-output="sl-hours-out" data-ticks="6" data-tick-labels="1">
                        <input class="mat-slider__input" type="range" id="sl-hours-from" name="open" min="0" max="24" step="1" value="8" />
                        <input class="mat-slider__input" type="range" id="sl-hours-to" name="close" min="0" max="24" step="1" value="18" />
                      </div>
                    </div>
                    """, "Without data-range-format the output reads \"8 h – 18 h\". Each input has a name of its own for a screen reader, made from the label: \"Opening hours from\" and \"Opening hours to\" (in German \"von\" and \"bis\"), and says its value in the format (\"8 h\")."),
                new("In a form: a filter that is sent and reset", $$"""
                    <form class="mat-form" method="get" action="/Components#slider" novalidate>
                      <div class="mat-field">
                        <div class="mat-field__meta"><label class="mat-label" for="sl-f-from">Price</label><output class="mat-range-out" id="sl-f-price-out">From 20 € to 80 €</output></div>
                        <div class="mat-slider mat-slider--range" data-slider data-min-gap="10" data-format="{0} €" data-range-format="From {0} to {1}" data-output="sl-f-price-out">
                          <input class="mat-slider__input" type="range" id="sl-f-from" name="price_from" min="0" max="200" step="5" value="20" />
                          <input class="mat-slider__input" type="range" id="sl-f-to" name="price_to" min="0" max="200" step="5" value="80" />
                        </div>
                      </div>
                      <div class="mat-field">
                        <div class="mat-field__meta"><label class="mat-label" for="sl-f-rating">At least</label><output class="mat-range-out" id="sl-f-rating-out" for="sl-f-rating">3 / 5</output></div>
                        <div class="mat-slider" data-slider data-format="{0} / 5" data-output="sl-f-rating-out" data-ticks data-tick-labels="1">
                          <input class="mat-slider__input" type="range" id="sl-f-rating" name="rating" min="1" max="5" step="1" value="3" />
                        </div>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">{{I("sliders")}}<span>Apply</span></button><button type="reset" class="mat-btn mat-btn--secondary">Reset</button></div>
                    </form>
                    ""","Apply sends price_from, price_to and rating as they are set; Reset puts the values of the markup back and the sliders are drawn again. Both inputs of the range slider are in the form and are checked like any other (min, max, required)."),
                new("In a dialog (a filter)", $$"""
                    <button type="button" class="mat-btn mat-btn--secondary" data-dialog-open="sl-dialog" aria-haspopup="dialog">{{I("sliders")}}<span>Filter by price</span></button>
                    <dialog class="mat-dialog mat-dialog--sm" id="sl-dialog" aria-labelledby="sl-dialog-title">
                      <form class="mat-dialog__form" method="dialog">
                        <div class="mat-dialog__head">{{I("sliders")}}<h2 class="mat-dialog__title" id="sl-dialog-title">Filter by price</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="Close">{{I("x")}}</button></div>
                        <div class="mat-dialog__body mat-form">
                          <div class="mat-field">
                            <div class="mat-field__meta"><label class="mat-label" for="sl-d-from">Price</label><output class="mat-range-out" id="sl-d-out">20 € – 140 €</output></div>
                            <div class="mat-slider mat-slider--range" data-slider data-min-gap="10" data-format="{0} €" data-output="sl-d-out" data-bubble="always">
                              <input class="mat-slider__input" type="range" id="sl-d-from" name="price_from" min="0" max="200" step="5" value="20" />
                              <input class="mat-slider__input" type="range" id="sl-d-to" name="price_to" min="0" max="200" step="5" value="140" />
                            </div>
                          </div>
                        </div>
                        <div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain"><button type="submit" class="mat-btn mat-btn--primary">Apply</button><button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>Cancel</button></div></div>
                      </form>
                    </dialog>
                    """, "The slider is drawn while the dialog is closed and has no width yet: it is placed again when the dialog opens. On a phone the dialog is full screen like every dialog (DIALOG-03)."),
            ],
            [
                "The markup is a wrapper `mat-slider[data-slider]` around a native `input.mat-slider__input[type=range]` with its own id, name, `min`, `max`, `step` and `value`, so a form posts it and it works without the script. `mat-slider--range` takes two inputs, from and to.",
                "Under the script the inputs are invisible but stay in place (opacity, never `display: none`): they keep the Tab key, the keyboard, validation, the form and the screen reader. The thumbs, the track and the bubbles are drawn over them and marked `aria-hidden`.",
                "The label of the field points to the slider (`for`); for a range slider to the first input, and the script names the two inputs from it (\"Price from\", \"Price to\") unless they have an `aria-label` of their own. The value is also said in words: `aria-valuetext` follows `data-format`.",
                "Keys: arrows by one `step`, Page Up and Page Down by a tenth of the scale (at least one step), Home and End to the ends. A thumb of a range slider stops at the other one (and at `data-min-gap`); Home on the right thumb goes to the left one, not past it.",
                "Pointer: a press on the track moves the nearest thumb, a press on a thumb grabs it where it is. With both thumbs at one value a press beside them takes the one on that side, a press on them waits for the first movement. `input` is sent while it moves, `change` when it lets go.",
                "`data-min`, `data-max` and `data-step` on the wrapper are copied to the inputs, which can carry them as well. `MatUi.refresh(scope)` and `MatUi.slider.init(scope)` pick up new sliders; `MatUi.slider.update(wrapper)` reads a changed `min`, `max`, `step` or format again.",
                "The value is always shown as text as well (the output, the labels at the ends or a bubble that stays): a slider alone does not say what it is set to (FORM-12). For an exact number use a number field.",
            ]),

        new("input-groups", "Input groups", "Forms", "link",
            "Text, a select or a button attached to a field: an address with its scheme, a key with a copy button, a port with a protocol.",
            ["FORM-03", "BUTTON-04", "BUTTON-05"],
            [
                new("Text and buttons around a field", $"""
                    <div class="mat-stack">
                      <div class="mat-field">
                        <label class="mat-label" for="grp-host">Address</label>
                        <div class="mat-input-group"><span class="mat-input-addon">https://</span><input class="mat-input" id="grp-host" name="host" placeholder="example" /><span class="mat-input-addon">.mat.local</span></div>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="grp-key">API key</label>
                        <div class="mat-input-group"><input class="mat-input mat-mono" id="grp-key" name="key" value="mk_3f9a71c2e4" readonly /><button type="button" class="mat-btn mat-btn--secondary" data-copy="#grp-key">{I("copy")}<span>Copy</span></button></div>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="grp-port">Port</label>
                        <div class="mat-input-group"><select class="mat-select" id="grp-proto" name="proto" aria-label="Protocol"><option>TCP</option><option>UDP</option></select><input class="mat-input mat-mono" id="grp-port" name="port" type="number" value="8030" /></div>
                      </div>
                    </div>
                    """, "The group draws one border around its parts; the first and the last part have the rounded corners."),
                new("Sizes", $"""
                    <div class="mat-stack">
                      <div class="mat-input-group mat-input-group--sm"><span class="mat-input-addon">@</span><input class="mat-input" id="grp-sm" name="sm" aria-label="User name, small" placeholder="user" /><button type="button" class="mat-btn mat-btn--primary">Add</button></div>
                      <div class="mat-input-group"><span class="mat-input-addon">@</span><input class="mat-input" id="grp-md" name="md" aria-label="User name" placeholder="user" /><button type="button" class="mat-btn mat-btn--primary">Add</button></div>
                      <div class="mat-input-group mat-input-group--lg"><input class="mat-input" id="grp-lg" name="lg" type="search" aria-label="Search, large" placeholder="Search" /><button type="button" class="mat-btn mat-btn--primary">{I("search")}<span>Search</span></button></div>
                    </div>
                    """),
            ],
            [
                "A field in a group still needs a label: the visible one above the group, or an `aria-label` when the group sits in a toolbar.",
                "A button in a group does one thing to the field next to it (copy, search, generate); it is not the Save of the form.",
            ]),

        new("special-inputs", "Search, password, counter and files", "Forms", "search",
            "Fields with something extra: an icon, an eye that shows the password, a counter for a limit, a file chooser and a drop zone.",
            ["FORM-03", "ICON-03", "A11Y-02", "BUTTON-05"],
            [
                new("Search and password", $"""
                    <div class="mat-form__grid">
                      <div class="mat-field"><label class="mat-label" for="spc-search">Search</label><div class="mat-input-icon">{I("search")}<input class="mat-input" id="spc-search" name="q" type="search" placeholder="Name or number" autocomplete="off" /></div></div>
                      <div class="mat-field">
                        <label class="mat-label" for="spc-pw">Password</label>
                        <div class="mat-input-icon mat-input-icon--end">
                          <input class="mat-input" id="spc-pw" name="pw" type="password" value="a secret" autocomplete="current-password" />
                          <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm mat-input-icon__btn" data-password-toggle="spc-pw" aria-pressed="false" aria-label="Show the password" data-tip="Show or hide">{I("eye", "mat-toggle-on")}{I("eye-off", "mat-toggle-off")}</button>
                        </div>
                      </div>
                    </div>
                    """, "The eye is a toggle button: aria-pressed says whether the password is shown. The field keeps its autocomplete value so that a password manager works."),
                new("A counter", """
                    <div class="mat-field">
                      <label class="mat-label" for="spc-note">Note</label>
                      <textarea class="mat-textarea" id="spc-note" name="note" rows="3" maxlength="120" data-counter="spc-note-count">Numbers are final after the review.</textarea>
                      <div class="mat-field__meta"><span class="mat-hint">Shown in the list.</span><span class="mat-counter" id="spc-note-count">0 / 120</span></div>
                    </div>
                    """, "The script writes the length and the limit into the element named by data-counter; the browser stops typing at maxlength."),
                new("Files", $"""
                    <div class="mat-stack">
                      <div class="mat-field"><label class="mat-label" for="spc-file">One file</label><input class="mat-input" id="spc-file" name="file" type="file" accept=".pdf,.png" /></div>
                      <div class="mat-field">
                        <span class="mat-label" id="spc-drop-label">Several files</span>
                        <label class="mat-dropzone" for="spc-drop">{I("upload")}<strong>Drop files here or click to choose</strong><span class="mat-muted">PDF, up to 10 MB each</span><input type="file" id="spc-drop" name="files" multiple accept=".pdf" aria-describedby="spc-drop-label" /><span class="mat-dropzone__files"></span></label>
                      </div>
                    </div>
                    """, "Drag files onto the zone: their names appear in it. The file input covers the whole zone, so a click and the keyboard work as they do for any file field."),
            ],
            [
                "An icon in a field is decoration (it has no name); the field's label says what it is.",
                "A file field says what it accepts (`accept`) and the limit in a hint; the server checks it again.",
            ]),

        new("floating-labels", "Floating labels", "Forms", "tag",
            "The label sits in the field and moves up when the field has focus or a value: for short forms such as a sign-in, never for long ones.",
            ["FORM-13", "FORM-03", "A11Y-03"],
            [
                new("Floating", """
                    <div class="mat-form__grid">
                      <div class="mat-field mat-field--float"><input class="mat-input" id="flt-name" name="name" placeholder=" " value="Alex Morgan" /><label class="mat-label" for="flt-name">Name</label></div>
                      <div class="mat-field mat-field--float"><input class="mat-input" id="flt-mail" name="mail" type="email" placeholder=" " /><label class="mat-label" for="flt-mail">E-mail</label></div>
                      <div class="mat-field mat-field--float"><select class="mat-select" id="flt-cat" name="category"><option>Finance</option><option>Contracts</option></select><label class="mat-label" for="flt-cat">Category</label></div>
                      <div class="mat-field mat-field--float"><input class="mat-input" id="flt-date" name="date" type="date" placeholder=" " /><label class="mat-label" for="flt-date">Date</label></div>
                      <div class="mat-field mat-field--float mat-field--full"><textarea class="mat-textarea" id="flt-note" name="note" rows="3" placeholder=" "></textarea><label class="mat-label" for="flt-note">Note</label></div>
                    </div>
                    """, "The input comes first and the label after it; the input has placeholder=\" \" (a space), which lets CSS know whether the field is empty. A select always shows its label above."),
            ],
            [
                "The label is a real label with `for`; the floating is only how it is drawn. A placeholder is never the only label.",
                "A date or time field shows its format in the field, so its label stays above from the start.",
            ]),

        new("form-grid", "Form layouts", "Forms", "grid",
            "Fields in columns, with the label beside the field, in one line, or in sections with their heading on the left.",
            ["FORM-07", "FORM-14", "FORM-10", "MOBILE-02"],
            [
                new("Columns", """
                    <div class="mat-form__grid mat-form__grid--3">
                      <div class="mat-field"><label class="mat-label" for="grid-city">City</label><input class="mat-input" id="grid-city" name="city" value="Berlin" /></div>
                      <div class="mat-field"><label class="mat-label" for="grid-zip">Postcode</label><input class="mat-input mat-mono" id="grid-zip" name="zip" value="10115" inputmode="numeric" /></div>
                      <div class="mat-field"><label class="mat-label" for="grid-country">Country</label><select class="mat-select" id="grid-country" name="country"><option>Germany</option><option>Austria</option></select></div>
                      <div class="mat-field mat-field--full"><label class="mat-label" for="grid-street">Street</label><input class="mat-input" id="grid-street" name="street" value="Invalidenstrasse 1" /></div>
                    </div>
                    """, "Two columns by default, three or four with mat-form__grid--3 and --4; one column below 700 px. A field spans the row with mat-field--full."),
                new("The label beside the field", """
                    <form class="mat-form mat-form--horizontal" method="get" action="/Components#form-grid" novalidate>
                      <div class="mat-field"><label class="mat-label" for="grid-h-name">Name</label><input class="mat-input" id="grid-h-name" name="name" value="Alex Morgan" /><span class="mat-hint">As others see it.</span></div>
                      <div class="mat-field"><label class="mat-label" for="grid-h-role">Role</label><select class="mat-select" id="grid-h-role" name="role"><option>Administrator</option><option>User</option></select></div>
                      <div class="mat-field"><label class="mat-label" for="grid-h-about">About</label><textarea class="mat-textarea" id="grid-h-about" name="about" rows="2"></textarea></div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">Save</button><button type="button" class="mat-btn mat-btn--secondary">Back</button></div>
                    </form>
                    """, "For settings pages on a wide screen. Below 700 px the label goes back above the field."),
                new("In one line", $"""
                    <form class="mat-form mat-form--inline" method="get" action="/Components#form-grid" novalidate>
                      <div class="mat-field"><label class="mat-label" for="grid-i-from">From</label><input class="mat-input" id="grid-i-from" name="from" type="date" value="2026-09-01" /></div>
                      <div class="mat-field"><label class="mat-label" for="grid-i-to">To</label><input class="mat-input" id="grid-i-to" name="to" type="date" value="2026-09-30" /></div>
                      <div class="mat-field"><label class="mat-label" for="grid-i-kind">Kind</label><select class="mat-select" id="grid-i-kind" name="kind"><option>All</option><option>Invoices</option></select></div>
                      <div class="mat-field mat-field--auto"><button type="submit" class="mat-btn mat-btn--primary">{I("check")}<span>Show</span></button></div>
                    </form>
                    """, "For a filter or a short action that fits in a line; it wraps on a narrow screen. A list's own filter belongs in the toolbar's dialog (LIST-03)."),
                new("Sections", """
                    <form class="mat-form" method="get" action="/Components#form-grid" novalidate>
                      <div class="mat-form__section">
                        <div class="mat-form__section-head"><h3>Profile</h3><p>How you appear to others.</p></div>
                        <div class="mat-form__grid">
                          <div class="mat-field"><label class="mat-label" for="grid-s-name">Name</label><input class="mat-input" id="grid-s-name" name="name" value="Alex Morgan" /></div>
                          <div class="mat-field"><label class="mat-label" for="grid-s-mail">E-mail</label><input class="mat-input" id="grid-s-mail" name="mail" type="email" value="alex@example.com" /></div>
                        </div>
                      </div>
                      <div class="mat-form__section">
                        <div class="mat-form__section-head"><h3>Language</h3><p>Used in the interface and in e-mails.</p></div>
                        <div class="mat-field"><label class="mat-label" for="grid-s-lang">Language</label><select class="mat-select" id="grid-s-lang" name="lang"><option>English</option><option>Deutsch</option></select></div>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">Save</button><button type="button" class="mat-btn mat-btn--secondary">Back</button></div>
                    </form>
                    """, "For a long settings page: the heading and a sentence on the left, the fields on the right; one Save under all sections. Together with the index of a long page they keep it easy to find things."),
            ],
            [
                "Buttons are under the content of the form in every layout (FORM-01); an inline form has its one button at the end of the line.",
                "A long form is split into sections or cards with a title each (FORM-10).",
            ]),

        new("validation", "Validation", "Forms", "check-circle",
            "Errors at the field and in a summary that links to every field; the toolkit writes the messages, the browser shows no bubble of its own.",
            ["FORM-03", "FORM-05", "FORM-06", "A11Y-04", "COLOR-04"],
            [
                new("States", $"""
                    <div class="mat-form__grid">
                      <div class="mat-field is-valid"><label class="mat-label" for="val-ok">Valid</label><input class="mat-input" id="val-ok" name="ok" value="alex@example.com" type="email" /><span class="mat-valid">{I("check")} This address is free.</span></div>
                      <div class="mat-field is-invalid"><label class="mat-label" for="val-bad">Invalid</label><input class="mat-input" id="val-bad" name="bad" value="alex@" type="email" aria-invalid="true" aria-describedby="val-bad-err" /><span class="mat-error" id="val-bad-err">Please enter an e-mail address like name@example.com.</span></div>
                    </div>
                    """, "The border and the text carry the state; the text says what is wrong and how to fix it (COLOR-04)."),
                new("Required fields, messages and the summary", $"""
                    <form class="mat-form" method="get" action="/Components#validation" novalidate>
                      <div class="mat-alert mat-alert--danger" role="alert" data-validation-summary hidden>{I("x-circle")}<div class="mat-alert__body"><strong>Please check the form.</strong><ul class="mat-alert__list"></ul></div></div>
                      <div class="mat-form__grid">
                        <div class="mat-field"><label class="mat-label" for="val-name">Name <span class="mat-req" aria-hidden="true">*</span></label><input class="mat-input" id="val-name" name="name" required data-msg-required="Please enter a name." /><span class="mat-error" aria-live="polite"></span></div>
                        <div class="mat-field"><label class="mat-label" for="val-mail">E-mail <span class="mat-req" aria-hidden="true">*</span></label><input class="mat-input" id="val-mail" name="mail" type="email" required data-msg-required="Please enter an e-mail address." data-msg-type="Please enter an address like name@example.com." /><span class="mat-error" aria-live="polite"></span></div>
                        <div class="mat-field"><label class="mat-label" for="val-age">Age <span class="mat-opt">(optional)</span></label><input class="mat-input" id="val-age" name="age" type="number" min="18" max="120" data-msg-range="Please enter an age from 18 to 120." /><span class="mat-error" aria-live="polite"></span></div>
                        <div class="mat-field"><label class="mat-label" for="val-code">Code</label><input class="mat-input mat-mono" id="val-code" name="code" pattern="[A-Z][A-Z][A-Z]-[0-9][0-9][0-9][0-9]" title="Three capitals, a dash and four digits, for example ABC-1234" data-msg-pattern="Three capitals, a dash and four digits, for example ABC-1234." /><span class="mat-error" aria-live="polite"></span></div>
                      </div>
                      <fieldset class="mat-field mat-fieldset">
                        <legend class="mat-legend">Contact me by <span class="mat-req" aria-hidden="true">*</span></legend>
                        <div class="mat-checks mat-checks--inline">
                          <label class="mat-check"><input type="radio" id="val-r1" name="how" value="mail" required data-msg-required="Please choose one." /><span class="mat-check__text">E-mail</span></label>
                          <label class="mat-check"><input type="radio" id="val-r2" name="how" value="phone" /><span class="mat-check__text">Phone</span></label>
                        </div>
                        <span class="mat-error" aria-live="polite"></span>
                      </fieldset>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">{I("check")}<span>Send</span></button></div>
                    </form>
                    """, "Press Send with the fields empty: every field with an error shows its message, the summary above lists them as links, and the focus goes to the first one. Typing a valid value removes its message."),
            ],
            [
                "`novalidate` on the form switches the bubbles of the browser off; the attributes `required`, `type`, `pattern`, `min` and `max` still decide what is valid, and `data-msg-*` carries the translated text.",
                "The space for the message is reserved, so the form does not jump when it appears. A server that finds an error draws the same markup (an `is-invalid` field with its `mat-error`, and the summary alert with its list).",
                "A required field has the star in its label (`mat-req`); an optional one in a form with mostly required fields says `(optional)`.",
            ]),
    ];
}
