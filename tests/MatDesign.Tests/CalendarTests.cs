using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-18: the calendar that is always shown and the appointment picker: their documentation, their texts, their formats and their CSS contract.</summary>
public partial class CalendarTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.pickers.css");

    [GeneratedRegex(@"(?s)var CAL_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"(?s)\ben: \{(.*?)\n    \},\n    de: \{")]
    private static partial Regex PickerTextsEnglish();

    [GeneratedRegex(@"pkText\('([A-Za-z]+)'")]
    private static partial Regex TextCall();

    [GeneratedRegex(@"<div class=""mat-calendar""([^>]*)>\s*<input class=""mat-calendar__input"" id=""([^""]+)""([^>]*)>", RegexOptions.Singleline)]
    private static partial Regex CalendarMarkup();

    [GeneratedRegex(@"\bvalue=""([^""]*)""")]
    private static partial Regex ValueAttribute();

    [GeneratedRegex(@"data-mode=""([a-z]+)""")]
    private static partial Regex ModeAttribute();

    [GeneratedRegex(@"data-picker=""slot""")]
    private static partial Regex SlotPicker();

    private static IEnumerable<(string Attributes, string Id, string Value, string Html)> Calendars() =>
        ComponentCatalog.All.Single(c => c.Id == "calendar").Examples.SelectMany(e => CalendarMarkup().Matches(e.Html).Select(m =>
            (m.Groups[1].Value, m.Groups[2].Value, ValueAttribute().Match(m.Groups[3].Value).Groups[1].Value, e.Html)));

    private static string Section(string from, string to)
    {
        var start = Js.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, from);
        var end = Js.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, to);
        return Js[start..end];
    }

    [Theory]
    [InlineData("calendar")]
    [InlineData("slot-picker")]
    public void The_calendar_and_the_appointment_picker_are_documented_among_the_forms_and_follow_their_rule(string id)
    {
        var component = ComponentCatalog.All.Single(c => c.Id == id);
        Assert.Equal("Forms", component.Group);
        Assert.Contains("FORM-18", component.Rules);
        Assert.Contains("FORM-15", component.Rules);
        Assert.True(component.Examples.Count >= 3, id);
        Assert.True(component.Behaviour.Count >= 5, id);
    }

    [Fact]
    public void The_rule_is_a_should_of_the_design_and_names_the_formats_the_minimum_and_the_event()
    {
        var rule = RuleBook.Get("FORM-18");
        Assert.NotNull(rule);
        Assert.Equal("FORM", rule.Area);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        foreach (var part in new[] { "2026-10-14T09:30", "2026-10-14T09:30..09:45", "data-min=\"today\"", "mat:slots-needed", "FORM-15", "never only coloured" })
        {
            Assert.Contains(part, rule.Statement + " " + rule.Dont, StringComparison.Ordinal);
        }

        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.Contains("2026-10-14T09:30", RuleBook.Get("FORM-15")!.Statement);
    }

    [Fact]
    public void The_calendar_examples_show_every_mode_and_post_the_fixed_formats()
    {
        var calendars = Calendars().ToList();
        Assert.True(calendars.Count >= 4, $"only {calendars.Count} calendars in the examples");
        Assert.All(calendars, c => Assert.Contains($"for=\"{c.Id}\"", c.Html));

        var formats = new Dictionary<string, string>
        {
            ["single"] = @"^(\d{4}-\d\d-\d\d)?$",
            ["range"] = @"^(\d{4}-\d\d-\d\d\.\.\d{4}-\d\d-\d\d)?$",
            ["multiple"] = @"^(\d{4}-\d\d-\d\d(,\d{4}-\d\d-\d\d)*)?$",
        };
        foreach (var calendar in calendars)
        {
            var mode = ModeAttribute().Match(calendar.Attributes).Groups[1].Value;
            Assert.Matches(formats[mode.Length == 0 ? "single" : mode], calendar.Value);
        }

        var modes = calendars.Select(c => ModeAttribute().Match(c.Attributes).Groups[1].Value).Select(m => m.Length == 0 ? "single" : m).Distinct().Order().ToList();
        Assert.Equal(["multiple", "range", "single"], modes);
    }

    [Fact]
    public void The_calendar_examples_show_the_limits_the_closed_days_the_events_and_the_week_numbers()
    {
        var all = string.Join("\n", Calendars().Select(c => c.Attributes));
        foreach (var option in new[] { "data-min=\"today\"", "data-min=\"+3d\"", "data-max=\"+90d\"", "data-disabled=\"", "data-disabled-weekdays=\"0,6\"", "data-events='{", "data-agenda", "data-week-numbers", "data-months=\"2\"", "data-max-dates=\"4\"", "data-week-start=\"0\"", "data-clearable" })
        {
            Assert.Contains(option, all, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_events_of_the_example_are_valid_json_with_dates_as_keys()
    {
        var events = Regex.Match(Calendars().Single(c => c.Id == "cal-events").Attributes, @"data-events='([^']*)'").Groups[1].Value;
        using var json = System.Text.Json.JsonDocument.Parse(events);
        var days = json.RootElement.EnumerateObject().ToList();
        Assert.True(days.Count >= 4);
        Assert.All(days, d =>
        {
            Assert.Matches(@"^\d{4}-\d\d-\d\d$", d.Name);
            Assert.All(d.Value.EnumerateArray(), e => Assert.True(e.TryGetProperty("title", out _)));
        });
    }

    [Fact]
    public void The_appointment_examples_are_lazy_ask_for_the_future_and_show_the_options()
    {
        var slot = ComponentCatalog.All.Single(c => c.Id == "slot-picker");
        var all = string.Join("\n", slot.Examples.Select(e => e.Html));
        Assert.True(SlotPicker().Matches(all).Count >= 3);
        foreach (var option in new[] { "data-min=\"today\"", "data-min=\"+3d\"", "data-min=\"tomorrow\"", "data-min-notice=\"2h\"", "data-duration=\"15\"", "data-group", "data-unavailable=\"hide\"", "data-hour12=\"true\"", "data-clearable", "required" })
        {
            Assert.Contains(option, all, StringComparison.Ordinal);
        }

        // no dates in the markup: the availability comes from the script of the site, near today, so that the examples do not go stale
        Assert.DoesNotContain("data-slots", all);
        var doc = File.ReadAllText(TestPaths.Web("wwwroot/js/doc.js"));
        Assert.Contains("'mat:slots-needed'", doc);
        Assert.Contains("MatUi.picker.setSlots(need.picker, demoSlots(need.month), need.month)", doc);

        var behaviour = string.Join("\n", slot.Behaviour);
        foreach (var part in new[] { "mat:slots-needed", "MatUi.picker.setSlots", "data-slots", "2026-10-14T09:30..09:45", "\"Taken\"", "data-min-notice" })
        {
            Assert.Contains(part, behaviour, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_texts_of_the_calendar_exist_in_both_languages_and_every_text_the_script_asks_for_is_there()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text table of the calendar was not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 35, $"only {en.Count} texts");
        Assert.Equal(en, de);
        Assert.Equal(en.Count, en.Distinct().Count());

        // a text that the script asks for and no table has would show as its key
        var picker = PickerTextsEnglish().Match(Js);
        Assert.True(picker.Success);
        var known = TextKey().Matches(picker.Groups[1].Value).Select(m => m.Groups[1].Value).Concat(en).ToHashSet();
        var asked = TextCall().Matches(Js).Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(asked.Count > 30);
        Assert.All(asked, key => Assert.Contains(key, known));
        // the group titles of the times are asked for by a name that is in a list
        Assert.All(new[] { "morning", "afternoon", "evening" }, key => Assert.Contains(key, known));
    }

    [Fact]
    public void The_script_has_the_calendar_the_slot_kind_the_event_and_the_api()
    {
        Assert.Contains("KINDS.slot = {", Js);
        Assert.Contains("mat:slots-needed", Js);
        Assert.Contains("calendar: CALENDAR_API", Js);
        Assert.Contains("setSlots: setSlots", Js);
        Assert.Contains("slotsFailed: slotsFailed", Js);
        var api = Section("var CALENDAR_API = {", "doc.addEventListener('click'");
        foreach (var member in new[] { "init:", "setEvents:", "setDisabled:", "setValue:", "go:" })
        {
            Assert.Contains(member, api);
        }

        Assert.Contains("initCalendars();", Js);
        Assert.Contains("initCalendars(scope);", Js);
        // the formats of FORM-15 are written down in the header of the script
        Assert.Contains("2026-10-14T09:30", Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)]);
        Assert.Contains("2026-10-10..2026-10-14 or several dates 2026-10-10,2026-10-12", Regex.Replace(Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)], @"\s+", " "));
    }

    [Fact]
    public void A_limit_can_be_a_date_a_word_or_a_distance_from_today()
    {
        var expr = Section("function dateExpr(text) {", "// \"2026-10-14, 2026-10-20..2026-10-24\"");
        foreach (var part in new[] { "'today'", "'tomorrow'", "'yesterday'", "[dwmy]", "addMonths(today" })
        {
            Assert.Contains(part, expr, StringComparison.Ordinal);
        }

        // the date picker takes the same limits as the calendar
        Assert.Contains("min = dateExpr(p.wrap.getAttribute('data-min'))", Js);
        Assert.DoesNotContain("dateOf(p.wrap.getAttribute('data-min'))", Js);
    }

    [Fact]
    public void The_grid_of_the_date_picker_is_shared_not_copied()
    {
        // one month grid, one set of keys, one paint for the picker, the calendar and the appointment picker
        Assert.Single(Regex.Matches(Js, @"function calMonth\("));
        Assert.Single(Regex.Matches(Js, @"function calKeys\("));
        Assert.Single(Regex.Matches(Js, @"function calPaint\("));
        foreach (var user in new[] { "var CALCTL = { pick: calcPick };", "pick: calPick,", "pick: slotPickDay," })
        {
            Assert.Contains(user, Js, StringComparison.Ordinal);
        }

        var calendar = Section("// ---- calendar: a month grid that is always shown (FORM-18)", "// ---- tags: a field of chips");
        Assert.Contains("calClick(c, t, e)", calendar);
        Assert.Contains("calKeys(c, e)", calendar);
        Assert.Contains("calSelect(s, d)", calendar);
    }

    [Fact]
    public void A_disabled_day_is_stepped_over_by_the_keys_and_a_period_may_not_run_across_a_taken_day()
    {
        var engine = Section("// ---- the month grid that three things share", "// ---- time and time range");
        Assert.Contains("function calMove(", engine);
        Assert.Contains("calMove(s, cur, -7)", engine);
        Assert.Contains("calNearest(", engine);
        Assert.Contains("s.blocked[isoOf(x)]", engine);
        // Home and End, Page up and down (with Shift the year)
        Assert.Contains("e.key === 'PageUp' || e.key === 'PageDown'", engine);
        Assert.Contains("e.shiftKey ? 12 : 1", engine);
    }

    [Fact]
    public void The_slot_picker_never_offers_the_past_and_posts_the_end_only_with_a_duration()
    {
        var slot = Section("// ---- slot: a day and a time to book (FORM-18)", "// ---- tags: a field of chips");
        Assert.Contains("if (!min || min < today) { min = today; }", slot);
        Assert.Contains("p.notice * 60000", slot);
        Assert.Contains("(p.duration ? '..' + timeStr(slotEnd(timeOf(s.slot), p.duration)) : '')", slot);
        Assert.Contains("'T' + s.slot", slot);
        Assert.Contains("data-lazy", slot);
        Assert.Contains("data-unavailable", slot);
        Assert.Contains("data-min-notice", slot);
        Assert.Contains("data-group", slot);
        Assert.Contains("data-duration", slot);
        Assert.Contains("'slotTaken'", slot);
        // the shortcut asks for the following months and gives up after a year (or at data-max)
        Assert.Contains("limit: s.max || addMonths(s.min, 12)", slot);
        // the page answers per month
        Assert.Contains("detail: { month: key, from: key + '-01'", slot);
    }

    [Theory]
    [InlineData(".mat-calendar__input { position: absolute; inset: 0;")]
    [InlineData(".mat-cal__day.is-unavailable { text-decoration: line-through; }")]
    [InlineData(".mat-cal__day.is-absent { visibility: hidden; }")]
    [InlineData(".mat-slot:disabled .mat-slot__time { text-decoration: line-through; }")]
    [InlineData(".mat-cal__grid--weeks { grid-template-columns: 2.25rem repeat(7, minmax(0, 1fr)); }")]
    public void What_cannot_be_chosen_says_so_in_a_second_way_besides_its_colour(string rule)
        => Assert.Contains(rule, Css);

    [Fact]
    public void The_times_sit_beside_the_month_on_a_wide_screen_and_below_it_on_a_phone_and_the_panel_keeps_the_phone_sheet()
    {
        Assert.Matches(@"@media \(min-width: 641px\) \{\s*\.mat-picker__panel--slots \{ width: min\(96vw, 42rem\); \}", Css);
        Assert.Matches(@"@media \(min-width: 641px\) \{\s*\.mat-slots \{ grid-template-columns: 19rem minmax\(0, 1fr\);", Css);
        // the times scroll inside their column so that the panel does not change its height with the day
        Assert.Contains(".mat-slots__scroll { position: absolute; inset: 0; overflow-y: auto;", Css);
        // the base rule of the phone sheet (inset 0, the whole screen) is not overridden by a width of the slot panel outside the wide-screen query
        var outside = Regex.Replace(Css, @"@media \(min-width: 641px\) \{[^@]*?\n\}\n", string.Empty);
        Assert.DoesNotContain(".mat-picker__panel--slots { width", outside);
    }

    [Fact]
    public void The_touch_targets_are_44_px_on_a_phone_and_a_time_is_as_high_as_a_control()
    {
        var phone = Css[Css.IndexOf("@media (max-width: 640px)", StringComparison.Ordinal)..];
        Assert.Contains(".mat-cal__day { height: 44px; }", phone);
        Assert.Matches(@"\.mat-slot \{[^}]*min-height: var\(--mat-control-h\);", Css);
        Assert.Matches(@"\.mat-cal__cell \{", Css);
        Assert.Contains("grid-auto-rows: minmax(48px, 1fr)", Css);
    }

    [Fact]
    public void What_is_chosen_is_a_tint_or_a_fill_behind_the_whole_item_and_never_a_bar()
    {
        // COLOR-08 holds for the new controls too
        Assert.Matches(@"\.mat-slot\[aria-pressed=""true""\][^{]*\{ background: var\(--mat-accent\);", Css);
        Assert.Contains(".mat-cal__cell.is-current { background: var(--mat-accent-soft-strong);", Css);
        Assert.Empty(OneSidedAccentTests.Violations("matui.pickers.css", Css));
    }

    [Fact]
    public void The_marks_are_small_dots_in_the_colour_of_the_accent_or_of_the_chosen_day()
    {
        Assert.Contains(".mat-cal__mark { width: 4px; height: 4px; border-radius: 50%; background: var(--mat-accent); }", Css);
        Assert.Contains(".mat-cal__day.is-selected .mat-cal__mark { background: currentColor; }", Css);
    }
}
