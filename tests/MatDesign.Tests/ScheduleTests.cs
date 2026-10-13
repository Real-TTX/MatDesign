using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>LIST-14: the schedule, its documentation, its texts, its week numbers and its CSS and script contract.</summary>
public partial class ScheduleTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.lists.css");
    private static readonly string E2e = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "tests", "e2e", "run.js"));

    private static readonly string[] Tones = ["accent", "ok", "warn", "error", "info", "plain"];

    [GeneratedRegex(@"var SCH_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};", RegexOptions.Singleline)]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"data-events='(?<single>[^']*)'|data-events=""(?<double>[^""]*)""")]
    private static partial Regex EventsAttribute();

    [GeneratedRegex(@"class=""([^""]*)""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex(@"\['(\d{4}-\d{2}-\d{2})', (\d+)\]")]
    private static partial Regex WeekCase();

    [GeneratedRegex(@"schText\(c, '([A-Za-z]+)'")]
    private static partial Regex UsedText();

    private static ComponentDoc Schedule => ComponentCatalog.All.Single(c => c.Id == "schedule");

    private static string Section()
    {
        var start = Js.IndexOf("var SCH_TEXT", StringComparison.Ordinal);
        var end = Js.IndexOf("function initSchedules", StringComparison.Ordinal);
        return Js[start..end];
    }

    private static string CssSection() => Css[Css.IndexOf("/* ---- Schedule (LIST-14)", StringComparison.Ordinal)..];

    private static IReadOnlyList<JsonElement> EventsOf(string html)
    {
        var match = EventsAttribute().Match(html);
        Assert.True(match.Success, "the example has no data-events");
        var json = match.Groups["single"].Success ? match.Groups["single"].Value : match.Groups["double"].Value;
        return [.. JsonDocument.Parse(json).RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    // ---------------------------------------------------------------------------------------------------- the documentation

    [Fact]
    public void The_schedule_is_documented_among_the_lists_and_follows_its_rule()
    {
        Assert.Equal("Lists and data", Schedule.Group);
        foreach (var rule in new[] { "LIST-14", "COLOR-04", "A11Y-02", "A11Y-05", "MOBILE-04" })
        {
            Assert.Contains(rule, Schedule.Rules);
        }

        Assert.True(Schedule.Examples.Count >= 4);
        Assert.True(Schedule.Behaviour.Count >= 6);
        var rule14 = RuleBook.Get("LIST-14");
        Assert.NotNull(rule14);
        Assert.Equal(RuleLevel.May, rule14.Level);
        Assert.Equal(RuleBook.Design, rule14.Source);
        Assert.Equal("LIST", rule14.Area);
        Assert.False(string.IsNullOrWhiteSpace(rule14.Why));
        Assert.False(string.IsNullOrWhiteSpace(rule14.Do));
        Assert.False(string.IsNullOrWhiteSpace(rule14.Dont));
    }

    [Fact]
    public void Every_schedule_of_an_example_is_a_named_region_with_a_fixed_date_and_a_pinned_clock()
    {
        // an example (and a test that looks at it) must not change with the day it is looked at: the date is written and the clock is pinned
        Assert.All(Schedule.Examples, e =>
        {
            Assert.Matches(@"<section class=""mat-schedule"" id=""sched-[a-z]+"" data-schedule[^>]*aria-label=""[^""]+""", e.Html);
            Assert.Matches(@"data-date=""\d{4}-\d{2}-\d{2}""", e.Html);
            Assert.Matches(@"data-now=""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}""", e.Html);
            Assert.Equal(["mat-schedule"], ClassAttribute().Matches(e.Html).SelectMany(m => m.Groups[1].Value.Split(' ')).Distinct().ToArray());
        });
        Assert.True(Schedule.Examples.Select(e => Regex.Match(e.Html, @"id=""(sched-[a-z]+)""").Groups[1].Value).Distinct().Count() == Schedule.Examples.Count, "the ids of the examples are not unique");
    }

    [Fact]
    public void The_events_of_the_examples_are_json_with_the_fields_the_script_reads()
    {
        foreach (var example in Schedule.Examples)
        {
            var events = EventsOf(example.Html);
            var ids = new HashSet<string>();
            foreach (var e in events)
            {
                var id = e.GetProperty("id").GetString()!;
                Assert.True(ids.Add(id), $"{example.Title}: the id {id} appears twice");
                Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("title").GetString()), id);
                var start = e.GetProperty("start").GetString()!;
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2})?$", start);
                Assert.True(DateTime.TryParse(start, CultureInfo.InvariantCulture, out _), $"{id}: {start} is not a date");
                if (e.TryGetProperty("end", out var endProperty))
                {
                    var end = endProperty.GetString()!;
                    Assert.Matches(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2})?$", end);
                    Assert.True(string.CompareOrdinal(end[..10], start[..10]) >= 0, $"{id}: ends before it starts");
                    if (start.Contains('T') && end.Contains('T'))
                    {
                        Assert.True(string.CompareOrdinal(end, start) >= 0, $"{id}: ends before it starts");
                    }
                }

                if (e.TryGetProperty("tone", out var tone))
                {
                    Assert.Contains(tone.GetString(), Tones);
                }
            }
        }
    }

    [Fact]
    public void The_examples_have_different_contents()
    {
        var byId = Schedule.Examples.ToDictionary(e => Regex.Match(e.Html, @"id=""(sched-[a-z]+)""").Groups[1].Value);

        // a practice: appointments with every tone, a status, overlapping ones and one that lasts two days
        var practice = EventsOf(byId["sched-practice"].Html);
        Assert.Equal(Tones.Order(), practice.Select(e => e.TryGetProperty("tone", out var t) ? t.GetString()! : "plain").Distinct().Order());
        Assert.Contains(practice, e => e.TryGetProperty("status", out _));
        Assert.Contains(practice, e => e.GetProperty("start").GetString() == "2026-10-15" && e.GetProperty("end").GetString() == "2026-10-16");
        Assert.Contains("data-week-numbers", byId["sched-practice"].Html);

        // a project: items that last for days, milestones, a link, and limits for the paging
        var project = EventsOf(byId["sched-project"].Html);
        Assert.True(project.Count(e => !e.GetProperty("start").GetString()!.Contains('T') && e.TryGetProperty("end", out _)) >= 3, "items that last for days");
        Assert.Contains(project, e => e.TryGetProperty("status", out var s) && s.GetString() == "Milestone");
        Assert.Contains(project, e => e.TryGetProperty("url", out _));
        Assert.Contains("data-min=", byId["sched-project"].Html);
        Assert.Contains("data-max=", byId["sched-project"].Html);

        // a school calendar: German, all-day events, calendar weeks, and the views a school needs
        var school = EventsOf(byId["sched-school"].Html);
        Assert.Contains("lang=\"de\"", byId["sched-school"].Html);
        Assert.Contains("data-week-numbers", byId["sched-school"].Html);
        Assert.Contains("data-views=\"month,agenda,year\"", byId["sched-school"].Html);
        Assert.True(school.Count(e => !e.GetProperty("start").GetString()!.Contains('T')) >= 10, "all-day events");

        // an empty schedule
        Assert.Empty(EventsOf(byId["sched-empty"].Html));
        Assert.Contains("data-empty=", byId["sched-empty"].Html);
    }

    // ---------------------------------------------------------------------------------------------------- the texts

    [Fact]
    public void The_schedule_has_every_text_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the schedule were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 40, $"only {en.Count} texts");
        Assert.Equal(en.Count, en.Distinct().Count());
        Assert.Equal(en, de);
    }

    [Fact]
    public void Every_text_the_script_asks_for_is_in_the_table()
    {
        var table = TextKey().Matches(Texts().Match(Js).Groups[1].Value).Select(m => m.Groups[1].Value).ToHashSet();
        var asked = UsedText().Matches(Section()).Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(asked.Count >= 25, $"only {asked.Count} texts are asked for by name");
        Assert.All(asked, key => Assert.Contains(key, table));
        // asked for by a name that is put together: the views, the tones, the arrows of every unit
        foreach (var view in new[] { "agenda", "day", "week", "month", "year" })
        {
            Assert.Contains(view, table);
        }

        foreach (var tone in new[] { "toneAccent", "toneOk", "toneWarn", "toneError", "toneInfo" })
        {
            Assert.Contains(tone, table);
        }

        foreach (var side in new[] { "prev", "next" })
        {
            foreach (var unit in new[] { "Day", "Week", "Month", "Year", "Days" })
            {
                Assert.Contains(side + unit, table);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------- the calendar weeks

    /// <summary>The week of a date by the formula of the script (<c>isoWeek</c> in matui.js), written the same way, so that it can be tried on every day of many years.</summary>
    private static int WeekLikeTheScript(DateTime d)
    {
        var t = new DateTime(d.Year, d.Month, d.Day);
        t = t.AddDays(-(((int)t.DayOfWeek + 6) % 7) + 3);
        var jan4 = new DateTime(t.Year, 1, 4);
        return 1 + (int)Math.Round(((t - jan4).TotalDays - 3 + (((int)jan4.DayOfWeek + 6) % 7)) / 7, MidpointRounding.AwayFromZero);
    }

    [Fact]
    public void The_formula_of_the_script_gives_the_ISO_week_of_every_day_of_many_years()
    {
        for (var day = new DateTime(2000, 1, 1); day < new DateTime(2040, 1, 1); day = day.AddDays(1))
        {
            Assert.Equal(ISOWeek.GetWeekOfYear(day), WeekLikeTheScript(day));
        }
    }

    [Fact]
    public void The_script_still_uses_that_formula()
    {
        Assert.Contains("t.setDate(t.getDate() - ((t.getDay() + 6) % 7) + 3);", Js);
        Assert.Contains("var jan4 = new Date(t.getFullYear(), 0, 4);", Js);
        Assert.Contains("return 1 + Math.round(((t - jan4) / 864e5 - 3 + ((jan4.getDay() + 6) % 7)) / 7);", Js);
        // a week that starts on another day than Monday has the number of the week that holds most of it: its fourth day
        Assert.Contains("function schWeekNo(c, from) { return isoWeek(addDays(from, 3)); }", Js);
    }

    public static TheoryData<string, int> WeekCases()
    {
        var line = E2e.Split('\n').Single(l => l.TrimStart().StartsWith("const weekCases = [", StringComparison.Ordinal));
        var data = new TheoryData<string, int>();
        foreach (Match m in WeekCase().Matches(line))
        {
            data.Add(m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(WeekCases))]
    public void The_dates_the_browser_check_uses_are_in_the_week_it_expects(string date, int week)
        => Assert.Equal(week, ISOWeek.GetWeekOfYear(DateTime.Parse(date, CultureInfo.InvariantCulture)));

    [Fact]
    public void The_browser_check_has_dates_that_are_hard_for_week_numbers()
    {
        var cases = WeekCases().Select(c => ((string)c[0]!, (int)c[1]!)).ToList();
        Assert.True(cases.Count >= 10);
        Assert.Contains(cases, c => c.Item2 == 1 && c.Item1.EndsWith("-12-29", StringComparison.Ordinal) || c.Item2 == 1 && c.Item1.EndsWith("-12-30", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Item2 == 53 && c.Item1.EndsWith("-01-03", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Item2 == 53 && c.Item1.EndsWith("-12-31", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------------- the script

    [Fact]
    public void The_script_pages_announces_keeps_the_focus_and_respects_reduced_motion()
    {
        var section = Section();
        foreach (var part in new[] { "aria-live", "role=status", "aria-pressed", "aria-busy", "aria-expanded", "aria-current", "aria-selected", "aria-label", "reducedMotion()", "composedPath", "pointerup", "pointerdown", "pointercancel", "ResizeObserver", "preventScroll" })
        {
            Assert.Contains(part, section);
        }

        // the buttons at the ends are dimmed, not disabled: a disabled button loses the focus that is on it
        Assert.Contains("aria-disabled", section);
        Assert.DoesNotContain(".disabled =", section);
        // the keys: the arrow keys and PageUp and PageDown page, and in the month grid they move from day to day
        foreach (var key in new[] { "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End", "Escape" })
        {
            Assert.Contains($"'{key}'", section);
        }
    }

    [Fact]
    public void The_script_tells_the_app_what_it_shows_and_what_was_chosen()
    {
        var section = Section();
        foreach (var name in new[] { "mat:schedule-select", "mat:schedule-range", "mat:schedule-day" })
        {
            Assert.Contains($"'{name}'", section);
        }

        // the choice of an event can be stopped (a link that the app wants to open itself)
        Assert.Matches(@"'mat:schedule-select', \{ bubbles: true, cancelable: true, detail: ev\.raw \}", section);
        Assert.Contains("var detail = { from: isoOf(r.from), to: isoOf(r.to), view: c.view, date: isoOf(c.date) };", section);
        var api = Js[Js.IndexOf("var schedule = {", StringComparison.Ordinal)..];
        api = api[..api.IndexOf("};", StringComparison.Ordinal)];
        foreach (var member in new[] { "init:", "setEvents:", "go:", "setLoading:", "get:" })
        {
            Assert.Contains(member, api);
        }

        var global = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains("schedule: schedule,", global);
        Assert.Contains("initSchedules(scope)", global);
        Assert.Contains("initSchedules();", Js);
    }

    [Fact]
    public void What_comes_from_the_data_is_escaped_and_never_becomes_a_style_or_a_script_address()
    {
        var section = Section();
        // the text of an event is escaped where it is drawn; an address is checked first; the tone is looked up in a list, never written into the markup as it is
        Assert.Contains("esc(ev.title)", section);
        Assert.Contains("esc(url)", section);
        Assert.Contains("(javascript|data|vbscript):", section);
        Assert.Contains("SCH_TONES.hasOwnProperty(raw.tone)", section);
        // no inline style: a length that is data is set from the script
        Assert.DoesNotContain("style=\"", section);
        Assert.DoesNotContain("cssText", section);
        Assert.DoesNotContain("setAttribute('style'", section);
        Assert.Contains("style.setProperty('--mat-sch-'", section);
    }

    [Fact]
    public void The_script_documents_its_attributes_in_the_header_and_reads_them()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        foreach (var attribute in new[] { "data-schedule", "data-views", "data-week-numbers", "data-week-start", "data-min", "data-max", "data-agenda", "data-day-start", "data-lazy", "data-now", "data-empty-days", "data-heading-level", "data-sch-<name>" })
        {
            Assert.Contains(attribute, header);
        }

        var section = Section();
        foreach (var attribute in new[] { "data-events", "data-view", "data-views", "data-date", "data-week-start", "data-week-numbers", "data-min", "data-max", "data-agenda", "data-day-start", "data-day-end", "data-lazy", "data-now", "data-empty", "data-heading-level", "data-sch-" })
        {
            Assert.Contains(attribute, section);
        }
    }

    // ---------------------------------------------------------------------------------------------------- the CSS

    [Fact]
    public void What_the_script_hides_stays_hidden_where_a_rule_sets_display()
    {
        var css = CssSection();
        Assert.Contains(".mat-schedule__jump[hidden] { display: none; }", css);
        Assert.Contains(".mat-schedule__busy[hidden] { display: none; }", css);
    }

    [Fact]
    public void The_schedule_follows_its_own_width_and_not_the_one_of_the_window()
    {
        var css = CssSection();
        Assert.Contains(".mat-schedule.is-wide", css);
        Assert.Contains(".mat-schedule.is-narrow", css);
        Assert.DoesNotContain("@media (max-width", css);
        Assert.DoesNotContain("@media (min-width", css);
        Assert.Contains("SCH_WIDE = 640", Js);
    }

    [Fact]
    public void Every_tone_is_a_tint_with_a_line_all_around_and_the_headings_and_the_top_of_a_grid_stay()
    {
        var css = CssSection();
        foreach (var tone in Tones.Where(t => t != "plain"))
        {
            Assert.Matches($@"\.mat-schedule__event--{tone} \{{ --mat-sch-bg: [^;]*; --mat-sch-line: [^;]*; --mat-sch-ink: [^;]*; \}}", css);
        }

        Assert.Matches(@"\.mat-schedule__event \{[^}]*border: 1px solid var\(--mat-sch-line\);", css);
        Assert.Matches(@"\.mat-schedule__dayhead \{\s*position: sticky; top: 0;", css);
        Assert.Matches(@"\.mat-schedule__top \{ position: sticky; top: 0;", css);
        // the tint is opaque where it lies over a line or under a heading: it is mixed with the surface
        Assert.Contains("color-mix(in srgb, var(--mat-success) 14%, var(--mat-surface))", css);
    }

    [Fact]
    public void A_pan_up_and_down_is_the_browsers_and_a_swipe_to_the_side_is_the_scripts()
    {
        Assert.Contains("touch-action: pan-y pinch-zoom;", CssSection());
    }

    [Fact]
    public void Targets_are_44_px_on_a_phone_and_the_week_stays_inside_the_page()
    {
        var css = CssSection();
        // MOBILE-04
        Assert.Matches(@"\.mat-schedule__daybtn \{[^}]*min-height: 2\.75rem;", css);
        Assert.Contains(".mat-schedule__period {", css);
        Assert.Matches(@"\.mat-schedule__period \{[^}]*min-height: var\(--mat-control-h\);", css);
        Assert.Matches(@"\.mat-schedule\.is-narrow\[data-view=""day""\] \{ --mat-schedule-hour: 5\.5rem; \}", css);
        Assert.Contains("overflow: hidden;", css[css.IndexOf(".mat-schedule__body {", StringComparison.Ordinal)..][..300]);
        // a page that slides moves the content, never the body: the page must not get wider while it does
        Assert.Contains(".mat-schedule__body.is-from-next > *", css);
    }

    [Fact]
    public void Reduced_motion_is_honoured_and_nothing_uses_important_or_a_literal_colour()
    {
        var css = CssSection();
        Assert.Contains("prefers-reduced-motion: reduce", css);
        Assert.Contains("reducedMotion()", Section());
        Assert.DoesNotContain("!important", css);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(", Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline));
    }

    [Fact]
    public void The_properties_that_the_script_sets_are_the_ones_the_css_reads_and_all_start_with_mat()
    {
        var css = CssSection();
        foreach (var name in new[] { "top", "len", "lane", "lanes", "hours", "cols", "c1", "c2", "span" })
        {
            Assert.Contains($"--mat-sch-{name}", css);
        }

        Assert.All(Regex.Matches(css, @"(?<![\w-])(--[a-z0-9-]+)\s*:").Select(m => m.Groups[1].Value).Distinct(), p => Assert.StartsWith("--mat-", p));
    }
}
