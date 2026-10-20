using System.Globalization;

namespace MatDesign.Web.Services;

/// <summary>
/// The rating (FORM-22): stars, hearts or dots to give a value on a short scale, or to show an average. A choice is a group of native radio buttons
/// (so a form posts it and, without the script, it is a row of radio buttons); the script draws the shapes over them. The radios of the examples are
/// written out by a helper, so that the examples and the source that is shown to copy are one string.
/// </summary>
internal static class RatingComponents
{
    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Word(double value, string one, string many) => $"{Number(value)} {(Math.Abs(value - 1) < 1e-9 ? one : many)}";

    /// <summary>The radios of a rating, each with its label: the values, and the value 0 as "No rating" when asked for.</summary>
    private static string Radios(string prefix, string name, double max, double step, double? chosen, bool none = false, bool required = false, string requiredMessage = "", Func<double, string>? label = null)
    {
        label ??= v => Word(v, "star", "stars");
        var rows = new List<string>();
        if (none)
        {
            rows.Add($"""<input class="mat-rating__input" type="radio" id="{prefix}-0" name="{name}" value="0"{(chosen is null ? " checked" : "")} /><label class="mat-rating__label" for="{prefix}-0">No rating</label>""");
        }

        var first = true;
        for (var v = step; v <= max + 1e-9; v += step)
        {
            var text = Number(Math.Round(v / step) * step);
            var attributes = (Math.Abs(v - (chosen ?? -1)) < 1e-9 ? " checked" : "")
                + (required ? " required" : "")
                + (required && first && requiredMessage.Length > 0 ? $" data-msg-required=\"{requiredMessage}\"" : "");
            rows.Add($"""<input class="mat-rating__input" type="radio" id="{prefix}-{text.Replace(".", "_", StringComparison.Ordinal)}" name="{name}" value="{text}"{attributes} /><label class="mat-rating__label" for="{prefix}-{text.Replace(".", "_", StringComparison.Ordinal)}">{label(Math.Round(v / step) * step)}</label>""");
            first = false;
        }

        return string.Join("\n", rows);
    }

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("rating", "Rating", "Forms", "star",
            "Stars, hearts or dots to give a value on a short scale (a review, a priority, how likely someone is to recommend), or to show an average. A choice is a group of native radio buttons that the script draws as shapes.",
            ["FORM-22", "FORM-12", "FORM-03", "COLOR-04", "A11Y-02", "A11Y-03", "MOBILE-04"],
            [
                new("Stars to choose, with the value beside them", $"""
                    <div class="mat-form__grid">
                      <fieldset class="mat-field mat-fieldset mat-rating" data-rating data-clearable data-show-value>
                        <legend class="mat-legend">Overall</legend>
                        {Radios("rt-overall", "overall", 5, 1, 3, none: true)}
                        <span class="mat-hint">Point at a star to see its value, click to choose it. A click on the chosen star clears it.</span>
                      </fieldset>
                      <fieldset class="mat-field mat-fieldset mat-rating" data-rating data-show-value>
                        <legend class="mat-legend">Support</legend>
                        {Radios("rt-support", "support", 5, 1, null)}
                        <span class="mat-hint">Nothing is chosen yet and this one cannot be cleared.</span>
                      </fieldset>
                    </div>
                    """, "Move the pointer over the stars, click, and use the arrow keys after a click or with Tab: the group is a set of radio buttons. Without the script this is a row of radio buttons with their labels."),
                new("Half steps", $"""
                    <fieldset class="mat-field mat-fieldset mat-rating" data-rating data-clearable data-show-value>
                      <legend class="mat-legend">Taste</legend>
                      {Radios("rt-taste", "taste", 5, 0.5, 3.5, none: true)}
                      <span class="mat-hint">The left half of a star is a half value.</span>
                    </fieldset>
                    """, "A half step is a radio of its own (0.5, 1, 1.5 ...): the radios say what can be chosen, so the same row works with the pointer, the keyboard and a form."),
                new("Hearts, dots and sizes", $"""
                    <div class="mat-stack">
                      <fieldset class="mat-field mat-fieldset mat-rating mat-rating--heart mat-rating--lg" data-rating data-show-value>
                        <legend class="mat-legend">Favourite (large hearts)</legend>
                        {Radios("rt-heart", "heart", 5, 1, 4, label: v => Word(v, "heart", "hearts"))}
                      </fieldset>
                      <fieldset class="mat-field mat-fieldset mat-rating mat-rating--dot mat-rating--sm" data-rating data-show-value>
                        <legend class="mat-legend">How likely are you to recommend us? (small dots)</legend>
                        {Radios("rt-nps", "nps", 10, 1, 8, label: v => $"{Number(v)} of 10")}
                      </fieldset>
                      <fieldset class="mat-field mat-fieldset mat-rating mat-rating--sm" data-rating data-show-value>
                        <legend class="mat-legend">Difficulty (small stars)</legend>
                        {Radios("rt-diff", "difficulty", 3, 1, 2, label: v => $"{Number(v)} of 3")}
                      </fieldset>
                    </div>
                    """, "Ten dots shrink to fit on a phone rather than run over the edge. A rating has three to ten shapes."),
                new("Showing an average", """
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly" data-rating data-value="4.5" data-max="5" data-count="128" data-show-value>4.5 out of 5</span><span class="mat-muted">Quarterly report</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly" data-rating data-value="3.7" data-count="1284" data-show-value>3.7 out of 5</span><span class="mat-muted">Parts a value that is not a half</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly" data-rating data-value="5" data-count="1" data-show-value>5 out of 5</span><span class="mat-muted">One rating</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly" data-rating data-value="0" data-show-value>Not rated</span><span class="mat-muted">Nobody has rated it</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly mat-rating--heart" data-rating data-value="3.5" data-max="5">3.5 out of 5</span><span class="mat-muted">Hearts, no number</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly mat-rating--dot mat-rating--sm" data-rating data-value="7" data-max="10" data-show-value>7 out of 10</span><span class="mat-muted">Ten dots, small</span></div>
                    </div>
                    """, "The text inside is what is seen without the script. The script draws the shapes, partly filled for any value, and gives the element `role=\"img\"` and a label in words (\"Rated 4.5 out of 5, 128 ratings\")."),
                new("Sizes", """
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly mat-rating--sm" data-rating data-value="4" data-show-value data-count="12">4 out of 5</span><span class="mat-muted">Small</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly" data-rating data-value="4" data-show-value data-count="12">4 out of 5</span><span class="mat-muted">Default</span></div>
                      <div class="mat-row"><span class="mat-rating mat-rating--readonly mat-rating--lg" data-rating data-value="4" data-show-value data-count="12">4 out of 5</span><span class="mat-muted">Large</span></div>
                    </div>
                    """),
                new("Disabled", $"""
                    <fieldset class="mat-field mat-fieldset mat-rating" data-rating data-show-value disabled>
                      <legend class="mat-legend">Rating (closed)</legend>
                      {Radios("rt-closed", "closed", 5, 1, 2)}
                      <span class="mat-hint">Ratings of this order have closed.</span>
                    </fieldset>
                    """, "A disabled fieldset disables the radios: the pointer does nothing, the value is not sent and the shapes are dimmed."),
                new("In a form: required, with the message at the field", $"""
                    <form class="mat-form" method="get" action="/Components/rating" novalidate>
                      <fieldset class="mat-field mat-fieldset mat-rating" data-rating data-show-value>
                        <legend class="mat-legend">Your rating <span class="mat-req" aria-hidden="true">*</span></legend>
                        {Radios("rt-req", "rating", 5, 1, null, required: true, requiredMessage: "Please choose a rating.")}
                        <span class="mat-error" aria-live="polite"></span>
                      </fieldset>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">{I("check")}<span>Send</span></button></div>
                    </form>
                    """, "Press Send without a rating: the message appears under the shapes, the empty shapes take the colour of the error and the focus goes to the group. A chosen value is posted as `rating=3`."),
            ],
            [
                "The markup is a `fieldset` with `mat-rating` and `data-rating` (add `mat-field mat-fieldset` for the message line), a `legend`, and a native radio (`mat-rating__input`) with its label (`mat-rating__label`) per value, all with one `name`: a form posts the value and the keys of the browser apply.",
                "The radios say what can be chosen: half stars are 0.5, 1, 1.5 ... (twice the radios), the highest value is the number of shapes (3 to 10), a radio with the value 0 is \"no rating\". The labels are what a screen reader says (\"3 stars\").",
                "The script adds `is-ready`, which makes the radios invisible (they keep the focus and the keys), and draws the shapes over them: an outline each and, over it, the filled ones up to `--mat-rating-fill`. Filled is the accent and empty an outline, so it is more than colour.",
                "A mouse or a pen shows the value it is on: the shapes fill up to it and the text beside them says it. A click chooses it; with `data-clearable` a click on the chosen value clears it. A touch screen has no preview.",
                "The arrow keys move and choose like any radio group, Tab enters on the chosen one, and the focus ring is drawn around the shapes. `data-show-value` writes the value as text beside them (\"Not rated\" while there is none).",
                "A read-only rating is a `span.mat-rating--readonly` with `data-value` (any number, drawn in part), `data-max` (3 to 10), maybe `data-count` and `data-show-value`. It gets `role=\"img\"` and an `aria-label` in words (\"Rated 4.5 out of 5, 128 ratings\") unless it has one.",
                "The shape is `mat-rating--heart` or `--dot` (star by default), the size `mat-rating--sm` or `--lg`. The shapes are as high as a control (44 px on a touch screen, 36 px small) and shrink in width instead of overflowing.",
                "A change sends `input`, `change` and then `mat:rating-change` (bubbles; `detail.value`). `MatUi.rating.get(el)` and `.set(el, value)` (0 clears), `MatUi.refresh(scope)` wires new ratings. Texts in English and German; `data-rt-<name>` on `<html>` changes them.",
                "An error is the `is-invalid` of the `mat-field`: the empty shapes take the colour of the error and the message is at the field. For a wide scale use a slider (FORM-17), for an exact number a number field.",
            ]),
    ];

    private static string I(string name) => ComponentCatalog.Icon(name, null);
}
