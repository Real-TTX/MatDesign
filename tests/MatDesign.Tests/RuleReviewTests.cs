using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The rules that the review of the rulebook (docs/RULES-REVIEW.md) added or changed, each held to what the code does: a rule that says the toolkit does something has a test that reads the CSS, the script,
/// the catalogue or the pages for it. Where the toolkit does not do it yet, the finding is in the review and not in a rule.
/// </summary>
public sealed partial class RuleReviewTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string Script => TestPaths.ReadToolkit("js/matui.js");

    private static string Adopting => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md"));

    private static string Css(string part) => TestPaths.ReadToolkit($"css/matui.{part}.css");

    private static readonly string[] Parts = ["tokens", "base", "shell", "controls", "forms", "pickers", "lists", "dialogs", "layout", "apps"];

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex DeclarationBlock();

    [GeneratedRegex(@"<(?:a|button|input|select|textarea|summary)\b[^>]*>")]
    private static partial Regex Focusable();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex Title();

    [GeneratedRegex(@"<html lang=""([^""]*)""")]
    private static partial Regex HtmlLang();

    private async Task<string> Html(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static IEnumerable<string> Examples(string componentId)
        => ComponentCatalog.Get(componentId)?.Examples.Select(e => e.Html) ?? throw new InvalidOperationException("no component " + componentId);

    private static IEnumerable<string> AllExamples() => ComponentCatalog.All.SelectMany(c => c.Examples.Select(e => e.Html));

    private static readonly string[] SitePages = ["/", "/Rules", "/Rules/Detail?id=SHELL-01", "/Rules/Changes", "/Download", "/Changelog", "/Identity", "/Components", "/Foundations", "/Patterns", "/Shell", "/Builder", "/Preview?view=list"];

    // ------------------------------------------------------------------------------------------------------ the rules that were added

    public static TheoryData<string, string> Added => new()
    {
        { "A11Y-06", "`<html lang>`" },
        { "A11Y-07", "`aria-disabled`, never `disabled`" },
        { "A11Y-08", "live region" },
        { "A11Y-09", "never falls to the top of the page" },
        { "A11Y-10", "A failure is never an empty list" },
        { "FORM-24", "when it is sent" },
        { "TABLE-05", "on this page" },
        { "CODE-11", "Every key is in both languages" },
        { "CODE-12", "never `display: none`" },
        { "CODE-13", "`mat:<noun>-<verb>`" },
        { "CODE-14", "`MATDESIGN_TRUSTED_PROXIES`" },
        { "CODE-15", "reading a script proves little" },
    };

    [Theory]
    [MemberData(nameof(Added))]
    public void A_rule_the_review_added_is_a_Should_of_the_design_with_a_reason_an_example_and_a_way_it_is_checked(string id, string phrase)
    {
        var rule = RuleBook.Get(id)!;

        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.Contains(phrase, rule.Statement);
        Assert.InRange(rule.Statement.Length, 200, 1200);
        Assert.InRange(rule.Title.Length, 10, 80);
        Assert.All(new[] { rule.Why, rule.Do, rule.Dont }, text => Assert.InRange(text?.Length ?? 0, 20, 400));
        Assert.NotEqual("Review", rule.Check);
        Assert.True(RuleValidator.Validate(rule, RuleBook.All.Where(r => r.Id != id)).Count == 0, id);
        // it is in the changelog of the rulebook as added on the day of the review, with a note
        var line = Assert.Single(RuleChanges.For(id));
        Assert.Equal(RuleChangeKind.Added, line.Kind);
        Assert.Equal(new DateOnly(2026, 10, 20), line.Date);
        Assert.NotEmpty(line.Note);
    }

    [Fact]
    public void The_added_rules_stand_at_the_end_of_their_areas_and_skip_the_ids_that_other_work_has_reserved()
    {
        Assert.Equal(["A11Y-01", "A11Y-02", "A11Y-03", "A11Y-04", "A11Y-05", "A11Y-06", "A11Y-07", "A11Y-08", "A11Y-09", "A11Y-10"], RuleBook.In("A11Y").Select(r => r.Id));
        Assert.Equal("CODE-15", RuleBook.In("CODE").Last().Id);
        Assert.Equal("TABLE-05", RuleBook.In("TABLE").Last().Id);
        Assert.Equal("FORM-25", RuleBook.In("FORM").Last().Id); // FORM-25 came after the review (the select from the server)
        // FORM-23, SHELL-20 and LIST-18 were given to other changes that were made at the same time (the search, the components pages): they exist now, and the review did not write them
        Assert.NotNull(RuleBook.Get("FORM-23"));
        Assert.NotNull(RuleBook.Get("SHELL-20"));
        Assert.NotNull(RuleBook.Get("LIST-18"));
    }

    // ------------------------------------------------------------------------------------------------------ the rules that were changed

    [Fact]
    public void The_rules_the_review_corrected_say_what_the_code_does()
    {
        var size = RuleBook.Get("BUTTON-05")!.Statement;
        Assert.Contains("44 px", size);
        Assert.Contains("MOBILE-04", size);
        Assert.DoesNotContain("smaller than 36 px", size);

        var messages = RuleBook.Get("SHELL-12")!.Statement;
        Assert.Contains("a toast (at the bottom right, on a phone at the top under the header bar, gone after a few seconds", messages);
        Assert.Contains("a tap on it goes to what lies under it, only the X takes it", messages);
        Assert.Contains("never the only place for an error", messages);

        Assert.Contains("split into cards or sections", RuleBook.Get("FORM-10")!.Statement);
        Assert.Contains("puts the cursor in the search field", RuleBook.Get("A11Y-02")!.Statement);
        var landmarks = RuleBook.Get("A11Y-03")!.Statement;
        Assert.Contains("`footer`", landmarks);
        Assert.Contains("exactly one `h1`", landmarks);
        Assert.Contains("an `alt` that says it", landmarks);
        Assert.Contains("spinner", RuleBook.Get("A11Y-05")!.Statement);
    }

    [Fact]
    public void The_rules_that_cite_alt_text_and_long_forms_find_them_where_they_cite_them()
    {
        // IDENT-08 and FORM-22 cite A11Y-03 for alt text and a name for an image; the catalogue cites FORM-10 for the split of a long form
        Assert.Contains("A11Y-03", RuleBook.Get("IDENT-08")!.Statement);
        Assert.Contains("A11Y-03", RuleBook.Get("FORM-22")!.Statement);
        var layout = ComponentCatalog.Get("form-layout")!;
        Assert.Contains("FORM-10", layout.Rules);
        Assert.Contains(layout.Behaviour, b => b.Contains("split into cards", StringComparison.Ordinal) && b.Contains("FORM-10", StringComparison.Ordinal));
    }

    [Fact]
    public void The_sizes_of_BUTTON_05_are_the_tokens_and_the_spinner_of_A11Y_05_turns_slowly_under_reduced_motion()
    {
        var tokens = Css("tokens");
        Assert.Contains("--mat-control-h: 40px;", tokens);
        Assert.Contains("--mat-control-h-sm: 32px;", tokens);
        Assert.Contains("--mat-control-h: 44px;", tokens);
        Assert.Contains("--mat-control-h-sm: 36px;", tokens);
        Assert.Matches(@"@media \(prefers-reduced-motion: reduce\) \{ \.mat-spinner \{ animation-duration: 3s", Css("controls"));
        Assert.Matches(@"animation-duration: \.01ms", Css("base"));
    }

    [Fact]
    public void A_toast_is_at_the_bottom_right_and_goes_after_a_few_seconds_with_an_X()
    {
        var host = Regex.Match(Css("controls"), @"\.mat-toasts \{[^}]*\}").Value;
        Assert.Contains("right:", host);
        Assert.Contains("bottom:", host);
        Assert.DoesNotContain("top:", host);
        // on a phone at the top under the header bar, where it covers no action of the thumb
        Assert.Matches(@"@media \(max-width: 600px\) \{\s*\.mat-toasts \{[^}]*top: calc\(var\(--mat-topbar-h\)[^}]*bottom: auto;", Css("controls"));
        // it only tells: a tap goes through it, except on its X
        Assert.Matches(@"\.mat-toast \{\s*pointer-events: none;", Css("controls"));
        Assert.Contains(".mat-toast .mat-alert__close { pointer-events: auto; }", Css("controls"));
        Assert.Contains(".mat-toasts > .mat-toast:not(:last-child) { display: none; }", Css("controls"));
        // it goes after six seconds (the time can be set with `timeout`), but not while it is being read (ScriptTests holds the waiting)
        Assert.Contains("var TOAST_TIME = 6000", Script);
        Assert.Matches(@"left: o\.timeout \|\| TOAST_TIME", Script);
        Assert.Contains("class=\"mat-alert__close\"", Script.Replace("'", "\""));
    }

    // ------------------------------------------------------------------------------------------------------ A11Y-01: the focus ring and the skip link

    /// <summary>The selectors that take the outline away and are held by a ring of another element: each with the test that holds its replacement.</summary>
    private static readonly string[] RingElsewhere = [":focus", ".mat-range:focus-visible", ".mat-upload__browse:focus-visible", ".mat-slider__input:focus-visible"];

    [Fact]
    public void Focus_is_never_removed_without_a_replacement()
    {
        // A11Y-01: a rule that sets the outline of a focused element to none also draws a ring, a border or a tint, or its ring is drawn by another element (listed, with the test of the ring)
        var problems = new List<string>();
        foreach (var part in Parts)
        {
            var css = Comment().Replace(Css(part), string.Empty);
            foreach (Match block in DeclarationBlock().Matches(css))
            {
                var selector = block.Groups[1].Value.Trim();
                var body = block.Groups[2].Value;
                if (!selector.Contains(":focus", StringComparison.Ordinal) || !Regex.IsMatch(body, @"outline:\s*(none|0)\b"))
                {
                    continue;
                }

                var replaced = Regex.IsMatch(body, @"box-shadow:|border-color:|background:");
                if (!replaced && !RingElsewhere.Contains(selector))
                {
                    problems.Add($"matui.{part}.css: {selector} takes the outline away and draws nothing instead");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));

        // the rings that the list above leans on are there
        Assert.Contains(":focus-visible { outline: none; box-shadow: var(--mat-focus-ring);", Css("base"));
        Assert.Contains(".mat-range:focus-visible::-webkit-slider-thumb { box-shadow: var(--mat-focus-ring); }", Css("forms"));
        Assert.Contains(".mat-range:focus-visible::-moz-range-thumb { box-shadow: var(--mat-focus-ring); }", Css("forms"));
        Assert.Contains(".mat-upload__zone:has(.mat-upload__browse:focus-visible)", Css("forms"));
        Assert.Contains(".mat-slider__thumb.is-key::before, .mat-slider__thumb.is-drag::before { box-shadow: var(--mat-focus-ring); }", Css("forms"));
    }

    [Fact]
    public async Task The_skip_link_is_the_first_thing_that_can_take_the_focus_on_every_page_and_leads_to_the_content()
    {
        var problems = new List<string>();
        foreach (var url in SitePages.Append("/Studio/Login"))
        {
            var html = await Html(url);
            var body = html[html.IndexOf("<body", StringComparison.Ordinal)..];
            var first = Focusable().Match(body).Value;
            if (!first.Contains("class=\"mat-skip\"", StringComparison.Ordinal) || !first.Contains("href=\"#main\"", StringComparison.Ordinal))
            {
                problems.Add($"{url}: the first thing that can take the focus is {first}");
            }

            if (!html.Contains("id=\"main\"", StringComparison.Ordinal))
            {
                problems.Add($"{url}: the skip link leads to #main and there is none");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.Matches(@"\.mat-skip:focus \{ top: var\(--mat-space-3\)", Css("base"));
    }

    // ------------------------------------------------------------------------------------------------------ IDENT-07, A11Y-06: the title and the language of a page

    [Fact]
    public async Task The_title_of_every_page_is_the_page_a_dot_and_the_name_of_the_site_with_the_entity_first()
    {
        foreach (var url in SitePages)
        {
            var title = Title().Match(await Html(url)).Groups[1].Value;
            Assert.EndsWith(" · MatDesign", title);
            Assert.True(title.Length > " · MatDesign".Length + 2, $"{url}: the title is only the name of the site");
        }

        Assert.StartsWith("SHELL-01 ", Title().Match(await Html("/Rules/Detail?id=SHELL-01")).Groups[1].Value);
    }

    [Fact]
    public async Task Every_page_names_its_language_and_the_script_chooses_its_texts_and_dates_from_it()
    {
        foreach (var url in SitePages.Append("/Studio/Login"))
        {
            var lang = HtmlLang().Match(await Html(url)).Groups[1].Value;
            Assert.Contains(lang, new[] { "en", "de" });
        }

        // without lang the script writes English; the dates follow lang unless data-locale says otherwise
        Assert.Contains("(root.lang || 'en')", Script);
        Assert.Contains("data-locale", Script);
        Assert.Contains("data-locale", Script[..Script.IndexOf("(function ()", StringComparison.Ordinal)]);
    }

    // ------------------------------------------------------------------------------------------------------ A11Y-07: the state is an attribute

    [Fact]
    public void A_button_that_cannot_go_further_is_aria_disabled_and_never_disabled()
    {
        // the stepper, the carousel and the schedule keep the focus on a button at its limit
        Assert.Matches(@"b\[0\]\.setAttribute\('aria-disabled'", Script);
        Assert.Matches(@"c\.prev\.setAttribute\('aria-disabled'", Script);
        Assert.Matches(@"c\.next\.setAttribute\('aria-disabled'", Script);
        Assert.Matches(@"c\.today\.setAttribute\('aria-disabled'", Script);
        Assert.DoesNotMatch(@"c\.(prev|next|today)\.disabled\s*=", Script);
        Assert.DoesNotMatch(@"b\[[01]\]\.disabled\s*=\s*(?!off)", Script.Replace("b[0].disabled = off;", string.Empty));
    }

    [Fact]
    public void What_opens_and_what_toggles_carries_the_aria_attribute_in_the_examples_of_the_catalogue()
    {
        var problems = new List<string>();
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var html in component.Examples.Select(e => e.Html))
            {
                foreach (Match tag in Regex.Matches(html, @"<button\b[^>]*>"))
                {
                    var text = tag.Value;
                    if ((text.Contains("data-group-toggle") || text.Contains("data-nav-branch") || text.Contains("data-nav-group")) && !text.Contains("aria-expanded"))
                    {
                        problems.Add($"{component.Id}: {text}");
                    }

                    if ((text.Contains("data-view-switch") || text.Contains("data-theme-set") || text.Contains("data-scheme-set") || text.Contains("data-password-toggle")) && !text.Contains("aria-pressed"))
                    {
                        problems.Add($"{component.Id}: {text}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        // the script keeps them true when it changes the state
        foreach (var attribute in new[] { "aria-expanded", "aria-pressed", "aria-selected", "aria-checked", "aria-busy", "aria-current" })
        {
            Assert.Contains($"'{attribute}'", Script);
        }
    }

    // ------------------------------------------------------------------------------------------------------ A11Y-08, A11Y-09, A11Y-10: live regions, the focus, loading

    [Fact]
    public async Task Toasts_and_the_counts_of_a_list_are_polite_live_regions_and_the_script_never_interrupts()
    {
        Assert.Contains("host.setAttribute('role', 'status'); host.setAttribute('aria-live', 'polite')", Script);
        Assert.Contains("class=\"mat-toolbar__info\" aria-live=\"polite\"", await Html("/Rules"));
        Assert.DoesNotContain("assertive", Script);
        Assert.True(Regex.Matches(Script, @"setAttribute\('aria-live', 'polite'\)").Count >= 10, "the live regions of the controls");
        // a live region is never hidden: the upload names its status region and the toolkit's own markup never gives one the hidden attribute
        Assert.Matches(@"u\.status\.setAttribute\('aria-live', 'polite'\)", Script);
        foreach (var html in AllExamples())
        {
            Assert.DoesNotMatch(@"<[^>]*aria-live=""[a-z]+""[^>]*\shidden[\s>]", html);
        }
    }

    [Fact]
    public void The_focus_goes_to_a_place_that_makes_sense_when_what_has_it_goes_away()
    {
        // a dialog gives it back to its opener, a form that is sent with errors puts it on the first of them, removing a file moves it to the next remove button
        Assert.Matches(@"var op = d\._matOpener;[^}]*op\.focus\(\{ preventScroll: true \}\)", Script);
        Assert.Contains("bad[0].focus();", Script);
        Assert.Matches(@"next = u\.entries\[at\] \|\| u\.entries\[at - 1\]", Script);
        Assert.Contains("\"x\": to the next remove button", Script);
    }

    [Fact]
    public void A_spinner_has_a_text_a_skeleton_and_a_filling_region_are_busy_and_a_failure_says_so_in_words()
    {
        foreach (var html in Examples("spinner").Take(1))
        {
            Assert.Equal(3, Regex.Matches(html, "role=\"status\"><span class=\"mat-sr-only\">Loading</span>").Count);
        }

        Assert.Contains("class=\"mat-loading\" role=\"status\"", string.Concat(Examples("spinner")));
        Assert.Contains("is-loading\" aria-busy=\"true\"", string.Concat(Examples("spinner")));
        Assert.Contains("aria-busy=\"true\"", string.Concat(Examples("skeleton")));
        // what failed says so, and the appointment picker and the tree offer to try again, the upload names the file
        Assert.Contains("slotsFailed: 'The appointments could not be loaded.'", Script);
        Assert.Contains("retry: 'Try again'", Script);
        Assert.Contains("failed: '{0} could not be loaded'", Script);
        Assert.Contains("data-slots-retry", Script);
        Assert.Contains("function upSetError", Script);
        // a progress bar that shows a task is a progressbar with a name
        Assert.Contains("role=\"progressbar\"", string.Concat(Examples("meter")));
    }

    // ------------------------------------------------------------------------------------------------------ FORM-24, TABLE-05

    [Fact]
    public void A_form_is_checked_when_it_is_sent_the_first_error_gets_the_focus_and_an_error_goes_when_the_field_is_right()
    {
        var submit = Script[Script.IndexOf("doc.addEventListener('submit'", StringComparison.Ordinal)..];
        submit = submit[..submit.IndexOf("}, true);", StringComparison.Ordinal)];
        Assert.Contains("e.submitter.hasAttribute('formnovalidate')", submit);
        Assert.Contains("summarize(form, bad);", submit);
        Assert.Contains("bad.forEach(function (i) { showError(i, messageFor(i)); });", submit);
        Assert.Contains("bad[0].focus();", submit);
        // no check while typing for the first time: the one listener on input only clears a message of a field that is wrong and has become right
        Assert.Matches(@"doc\.addEventListener\('input', function \(e\) \{ var i = e\.target; if \(i\.closest[^}]*is-invalid'\) && i\.checkValidity\(\)\) \{ showError\(i, ''\); \}", Script);
        // the server draws the same markup: an invalid field, its message line and the summary of the validation example
        var example = string.Concat(Examples("validation"));
        Assert.Contains("data-validation-summary", example);
        Assert.Contains("class=\"mat-field is-invalid\"", example);
        Assert.Contains("class=\"mat-error\"", example);
        Assert.Contains("aria-invalid=\"true\"", example);
    }

    [Fact]
    public void Every_check_of_a_row_in_the_examples_is_named_by_what_it_selects_and_the_example_of_the_table_shows_the_bar_that_counts()
    {
        var checks = AllExamples().SelectMany(h => Regex.Matches(h, @"<input\b[^>]*data-select-row[^>]*>").Select(m => m.Value)).ToList();
        Assert.NotEmpty(checks);
        // named by an aria-label (a table, a card) or by the label that wraps the row (a list of rows: mat-item__check inside the label that is mat-item__main)
        Assert.All(checks, c => Assert.True(Regex.IsMatch(c, @"aria-label=""Select [^""]+""") || c.Contains("class=\"mat-item__check\"", StringComparison.Ordinal), c));

        var table = string.Concat(Examples("table"));
        Assert.Contains("data-select-all", table);
        Assert.Contains("data-bulk-bar", table);
        Assert.Contains("data-bulk-count", table);
        Assert.Contains("selected on this page", ComponentCatalog.Get("table")!.Behaviour.Single(b => b.Contains("this page", StringComparison.Ordinal)));
    }

    // ------------------------------------------------------------------------------------------------------ CODE-11 to CODE-15

    /// <summary>The keys of the sub-object that starts at <paramref name="open"/> (the index of its brace): the names before a colon at its first level, outside of strings.</summary>
    private static List<string> KeysOf(string text, int open)
    {
        var keys = new List<string>();
        var depth = 0;
        var word = string.Empty;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\'' or '"')
            {
                var quote = c;
                var start = ++i;
                while (text[i] != quote)
                {
                    i += text[i] == '\\' ? 2 : 1;
                }

                word = text[start..i];
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                if (--depth == 0)
                {
                    return keys;
                }
            }
            else if (c == ':' && depth == 1 && word.Length > 0)
            {
                keys.Add(word);
                word = string.Empty;
            }
            else if (c == ',')
            {
                word = string.Empty;
            }
            else if (depth == 1 && (char.IsLetterOrDigit(c) || c == '_'))
            {
                word = char.IsLetterOrDigit(text[i - 1]) || text[i - 1] == '_' ? word + c : c.ToString();
            }
        }

        return keys;
    }

    [Fact]
    public void Every_table_of_texts_of_the_script_has_every_key_in_English_and_in_German()
    {
        var tables = Regex.Matches(Script, @"var ([A-Z]+_TEXT) = \{").Select(m => (Name: m.Groups[1].Value, At: m.Index + m.Length - 1)).ToList();
        Assert.True(tables.Count >= 10, "the tables of texts: " + string.Join(", ", tables.Select(t => t.Name)));
        var problems = new List<string>();
        foreach (var (name, at) in tables)
        {
            var en = Regex.Match(Script[at..], @"\ben: \{").Index + at + Regex.Match(Script[at..], @"\ben: \{").Length - 1;
            var de = Regex.Match(Script[at..], @"\bde: \{").Index + at + Regex.Match(Script[at..], @"\bde: \{").Length - 1;
            var english = KeysOf(Script, en).Order().ToList();
            var german = KeysOf(Script, de).Order().ToList();
            if (!english.SequenceEqual(german))
            {
                problems.Add($"{name}: only in English: {string.Join(", ", english.Except(german))}; only in German: {string.Join(", ", german.Except(english))}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void The_script_has_one_way_to_name_an_event_and_every_event_that_an_app_may_use_is_written_down_in_its_header()
    {
        var header = Script[..Script.IndexOf("(function () {", StringComparison.Ordinal)];
        var events = Regex.Matches(Script, @"new CustomEvent\(['""]([^'""]+)['""]").Select(m => m.Groups[1].Value).Distinct().Order().ToList();
        Assert.True(events.Count >= 20, string.Join(", ", events));
        Assert.All(events, e => Assert.Matches(@"^mat:[a-z]+(-[a-z]+)*$", e));
        // every event is written down in the header and in the table of ADOPTING.md, which ScriptEventsTests holds to the code (what is sent, bubbles, cancelable, detail); a new one that is not fails here
        var undocumented = events.Where(e => !header.Contains(e, StringComparison.Ordinal) || !Adopting.Contains($"| `{e}` |", StringComparison.Ordinal)).ToList();
        Assert.Empty(undocumented);
        // the public API is one object per control under window.MatUi
        foreach (var control in new[] { "picker", "calendar", "otp", "rating", "tree", "upload", "schedule", "groups", "theme", "scheme" })
        {
            Assert.Matches($@"\b{control}:", Script[Script.IndexOf("window.MatUi = ", StringComparison.Ordinal)..]);
        }
    }

    [Fact]
    public void A_drawn_control_is_a_wrapper_around_the_native_element_that_posts_in_the_examples()
    {
        var native = new Dictionary<string, string>
        {
            ["slider"] = "type=\"range\"",
            ["number-stepper"] = "type=\"number\"",
            ["rating"] = "type=\"radio\"",
            ["code-input"] = "class=\"mat-otp__input\"",
            ["file-upload"] = "type=\"file\"",
            ["rich-select"] = "class=\"mat-picker__input\"",
            ["date-picker"] = "class=\"mat-picker__input\"",
        };
        foreach (var (id, element) in native)
        {
            var html = string.Concat(Examples(id));
            Assert.Contains(element, html);
            Assert.Matches(@"\sname=""[^""]+""", html);
        }

        // the real input is hidden by opacity or by size, never by display: none (the focus would go with it)
        foreach (var rule in Regex.Matches(Css("forms") + Css("pickers"), @"([^{}]*\.mat-(?:slider|rating|otp|upload|stepper)__input[^{}]*)\{([^{}]*)\}").Cast<Match>())
        {
            if (rule.Groups[1].Value.Contains("is-ready", StringComparison.Ordinal))
            {
                Assert.DoesNotMatch(@"display:\s*none", rule.Groups[2].Value);
            }
        }
    }

    [Fact]
    public void The_sign_in_trusts_a_proxy_only_when_it_is_known_and_a_setting_that_cannot_be_read_never_means_everybody()
    {
        Assert.Equal("MATDESIGN_TRUSTED_PROXIES", TrustedProxies.Variable);
        var byDefault = TrustedProxies.Parse(null);
        Assert.True(byDefault.IsDefault);
        Assert.True(byDefault.Trusts(IPAddress.Loopback));
        Assert.True(byDefault.Trusts(IPAddress.Parse("172.18.0.2")));
        Assert.False(byDefault.Trusts(IPAddress.Parse("8.8.8.8")));
        Assert.True(TrustedProxies.Parse("none").TrustsNone);
        Assert.False(TrustedProxies.Parse("none").Trusts(IPAddress.Loopback));
        Assert.True(TrustedProxies.Parse("172.18.0.2, 10.0.0.0/8").Trusts(IPAddress.Parse("10.1.2.3")));
        Assert.ThrowsAny<Exception>(() => TrustedProxies.Parse("this is not an address"));
        Assert.Contains("one sentence", RuleBook.Get("CODE-14")!.Statement);
    }

    [Fact]
    public void Every_component_names_rules_has_examples_and_a_rule_that_it_names_exists_and_the_changelog_of_the_toolkit_is_the_place_for_what_an_app_can_see()
    {
        Assert.All(ComponentCatalog.All, c => Assert.NotEmpty(c.Rules));
        Assert.All(ComponentCatalog.All, c => Assert.NotEmpty(c.Examples));
        Assert.All(ComponentCatalog.All.SelectMany(c => c.Rules), id => Assert.NotNull(RuleBook.Get(id)));
        // the rulebook and the toolkit have a changelog each, in the same place for a person who changes either
        Assert.Contains("Unreleased", Changelog.LoadText());
        Assert.NotEmpty(RuleChanges.All);
        foreach (var file in new[] { "CHANGELOG.md", "docs/VERSIONING.md", "docs/ADOPTING.md" })
        {
            Assert.True(File.Exists(Path.Combine(TestPaths.RepoRoot, file)), file);
        }
    }
}
