using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-23: the search with suggestions, its documentation, its texts, its markup contract, its script and its CSS (the behaviour is held by tests/e2e/run.js in a browser).</summary>
public partial class SearchControlTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.pickers.css");
    private static readonly string ShellCss = TestPaths.ReadToolkit("css/matui.shell.css");

    [GeneratedRegex(@"(?s)var SRCH_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"srText\('([A-Za-z]+)'")]
    private static partial Regex TextCall();

    [GeneratedRegex(@"<div class=""(mat-search[^""]*)""([^>]*)>\s*<input class=""mat-search__input""([^>]*)>")]
    private static partial Regex Wrapper();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    private static ComponentDoc Component => ComponentCatalog.All.Single(c => c.Id == "search");

    /// <summary>The code of the section of the script that is the search.</summary>
    private static string Section
    {
        get
        {
            var from = Js.IndexOf("search with suggestions (FORM-23, SHELL-20, matui.pickers.css)", StringComparison.Ordinal);
            var to = Js.IndexOf("// a meter is filled by data", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, "the section of the search was not found");
            return Js[from..to];
        }
    }

    /// <summary>The CSS of the search: from its heading to the heading of the tags.</summary>
    private static string Style
    {
        get
        {
            var from = Css.IndexOf("Search with suggestions (FORM-23, SHELL-20)", StringComparison.Ordinal);
            var to = Css.IndexOf("Tags: a field of chips", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, "the CSS of the search was not found");
            return Css[from..to];
        }
    }

    private static string Rule(string selector)
    {
        var start = Style.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{selector} is not in the stylesheet");
        return Style[start..Style.IndexOf('}', start)];
    }

    private static IEnumerable<(string Classes, string Attributes, string Input)> Searches() =>
        Component.Examples.SelectMany(e => Wrapper().Matches(e.Html).Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)));

    // ---------------------------------------------------------------------------------------------------- the rule and the documentation

    [Fact]
    public void The_search_follows_its_rule_and_the_rule_is_complete()
    {
        var rule = RuleBook.Get("FORM-23");
        Assert.NotNull(rule);
        Assert.Equal("FORM", rule.Area);
        Assert.Equal("Search with suggestions", rule.Title);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.InRange(rule.Statement.Length, 10, 1200);
        foreach (var word in new[] { "mat-search", "data-autocomplete", "type=\"search\"", "combobox", "aria-activedescendant", "Escape", "Tab", "data-search-url", "late answer", "http or https", "data-recent", "full-screen", "44 px" })
        {
            Assert.Contains(word, rule.Statement);
        }

        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.Contains("mat-search", rule.Do);
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
        Assert.StartsWith("Test", rule.Check);
        foreach (Match cited in Regex.Matches(rule.Statement, @"\b([A-Z0-9]+-\d\d)\b"))
        {
            Assert.NotNull(RuleBook.Get(cited.Groups[1].Value));
        }
    }

    [Fact]
    public void The_search_is_documented_among_the_forms_with_the_rules_it_follows_and_the_limits_of_a_component()
    {
        Assert.Equal("Forms", Component.Group);
        Assert.Equal("Search with suggestions", Component.Name);
        foreach (var rule in new[] { "FORM-23", "SHELL-20", "FORM-12", "A11Y-02", "A11Y-03", "COLOR-08", "MOBILE-04" })
        {
            Assert.Contains(rule, Component.Rules);
            Assert.NotNull(RuleBook.Get(rule));
        }

        Assert.True(Component.Examples.Count >= 5, $"only {Component.Examples.Count} examples");
        Assert.InRange(Component.Behaviour.Count, 8, 12);
        Assert.All(Component.Behaviour, b => Assert.True(b.Length <= 300, b.Length + ": " + b[..40]));
        Assert.Contains(ComponentCatalog.All.Select(c => c.Id), id => id == "search");
        // it stands after the tags in the forms, where the other fields that suggest are
        var forms = ComponentCatalog.All.Where(c => c.Group == "Forms").Select(c => c.Id).ToList();
        Assert.True(forms.IndexOf("search") > forms.IndexOf("tag-input"));
    }

    [Fact]
    public void Every_search_of_an_example_is_a_wrapper_around_a_real_search_input_that_a_label_points_to()
    {
        var searches = Searches().ToList();
        Assert.True(searches.Count >= 5, $"only {searches.Count} searches in the examples");
        Assert.All(searches, s =>
        {
            Assert.Contains("data-autocomplete", s.Attributes);
            Assert.Contains("type=\"search\"", s.Input);
            Assert.Matches(@"\bid=""[^""]+""", s.Input);
            Assert.Matches(@"\bname=""[^""]+""", s.Input);
        });

        foreach (var example in Component.Examples)
        {
            foreach (Match input in Regex.Matches(example.Html, @"<input class=""mat-search__input"" type=""search"" id=""([^""]+)"""))
            {
                Assert.Contains($"for=\"{input.Groups[1].Value}\"", example.Html);
            }
        }
    }

    [Fact]
    public void The_examples_show_local_items_a_server_recent_choices_a_form_and_a_disabled_field()
    {
        var searches = Searches().ToList();
        Assert.Contains(searches, s => s.Attributes.Contains("data-items='", StringComparison.Ordinal));
        Assert.Contains(searches, s => s.Attributes.Contains("data-search-url=\"/api/demo/search\"", StringComparison.Ordinal) && s.Attributes.Contains("data-min=\"2\"", StringComparison.Ordinal));
        Assert.Contains(searches, s => s.Attributes.Contains("data-recent=\"", StringComparison.Ordinal));
        Assert.Contains(searches, s => s.Attributes.Contains("data-results-url=\"/Search\"", StringComparison.Ordinal) && s.Attributes.Contains("data-search-url=\"/api/search\"", StringComparison.Ordinal));
        Assert.Contains(searches, s => s.Input.Contains(" disabled", StringComparison.Ordinal));
        Assert.Contains(Component.Examples, e => e.Html.Contains("<form role=\"search\" action=\"/Search\" method=\"get\">", StringComparison.Ordinal));
        // an example that wants a key on a page that has the same key in its header bar would fight it for the key: only the header bar has data-key
        Assert.DoesNotContain(Component.Examples, e => e.Html.Contains("data-key=", StringComparison.Ordinal));
    }

    [Fact]
    public void The_local_items_of_the_examples_are_json_with_titles_addresses_and_icons_of_the_sprite()
    {
        var sprite = RegistryTests.Sprite;
        foreach (var example in Component.Examples)
        {
            foreach (Match items in Regex.Matches(example.Html, @"data-items='([^']*)'"))
            {
                var groups = JsonDocument.Parse(items.Groups[1].Value).RootElement;
                foreach (var group in groups.EnumerateArray())
                {
                    Assert.False(string.IsNullOrEmpty(group.GetProperty("name").GetString()));
                    foreach (var item in group.GetProperty("items").EnumerateArray())
                    {
                        Assert.False(string.IsNullOrEmpty(item.GetProperty("title").GetString()));
                        Assert.StartsWith("#", item.GetProperty("href").GetString());
                        Assert.True(sprite.Contains(item.GetProperty("icon").GetString()!), item.GetProperty("icon").GetString());
                    }
                }
            }
        }
    }

    [Fact]
    public void The_header_bar_is_documented_with_the_search_in_it_and_the_rule_that_says_why()
    {
        var topbar = ComponentCatalog.All.Single(c => c.Id == "topbar");
        var example = topbar.Examples.Single(e => e.Title == "With a search field");

        Assert.Equal("bar", example.Frame);
        Assert.Contains("<div class=\"mat-topbar__search\">", example.Html);
        Assert.Contains("mat-topbar__actions", example.Html);
        Assert.True(example.Html.IndexOf("mat-topbar__search", StringComparison.Ordinal) < example.Html.IndexOf("mat-topbar__actions", StringComparison.Ordinal));
        Assert.Contains(topbar.Behaviour, b => b.Contains("SHELL-20", StringComparison.Ordinal));
        // the header bar keeps its other examples
        Assert.True(topbar.Examples.Count >= 4);
    }

    // ---------------------------------------------------------------------------------------------------- the texts

    [Fact]
    public void The_search_has_every_text_in_both_languages_with_the_places_for_what_is_put_in()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the search were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 16, $"only {en.Count} texts");
        Assert.Equal(en, de);
        Assert.Equal(en.Count, en.Distinct().Count());
        foreach (var language in new[] { match.Groups[1].Value, match.Groups[2].Value })
        {
            Assert.Matches(@"none: '[^']*\{0\}[^']*'", language);
            Assert.Matches(@"all: '[^']*\{0\}[^']*'", language);
            Assert.Matches(@"count: '\{0\}[^']*'", language);
            Assert.Matches(@"hintKey: '[^']*\{0\}[^']*'", language);
        }

        Assert.Contains("Search is not available", match.Groups[1].Value);
        Assert.Contains("Keine Treffer für", match.Groups[2].Value);
        // the name is not one of another table (SL_TEXT is the slider)
        Assert.Single(Regex.Matches(Js, @"var SRCH_TEXT = "));
        Assert.DoesNotContain("var SR_TEXT", Js);
    }

    [Fact]
    public void Every_text_the_script_asks_for_is_in_the_table_and_an_app_can_change_one_by_an_attribute_of_html()
    {
        var keys = TextKey().Matches(Texts().Match(Js).Groups[1].Value).Select(m => m.Groups[1].Value).ToHashSet();
        var asked = TextCall().Matches(Section).Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.True(asked.Count >= 12, "the script asks for too few texts: the check no longer looks at anything");
        Assert.All(asked, key => Assert.Contains(key, keys));
        Assert.Contains("root.getAttribute('data-sr-' + key.toLowerCase())", Section);
        // what a person typed goes into a text through a function, so that a dollar sign survives
        Assert.Contains("s.replace('{0}', function () { return a === undefined ? '' : a; })", Section);
        // no text of the control is written in the code of the control except through the table
        Assert.DoesNotContain("'Search is not available'", Section.Replace(Texts().Match(Js).Value, string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("'No results", Section.Replace(Texts().Match(Js).Value, string.Empty, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------------- the script

    [Fact]
    public void The_script_is_documented_in_its_header_and_found_by_the_init_the_refresh_and_the_api()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        foreach (var attribute in new[] { "data-autocomplete", "data-search-url", "data-items", "data-min", "data-delay", "data-limit", "data-results-url", "data-recent", "data-key", "data-sr-<name>", "mat:search-select", "MatUi.search" })
        {
            Assert.Contains(attribute, header);
        }

        var init = Js[Js.IndexOf("function init() {", StringComparison.Ordinal)..];
        Assert.Contains("initSearches();", init[..init.IndexOf("applyTheme", StringComparison.Ordinal)]);
        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains("search: search,", api);
        Assert.Contains("initSearches(scope);", api);
        Assert.Contains("var search = { init: initSearches, setItems: srSetItems, open: srApiOpen, close: srApiClose, clear: srApiClear, focus:", Section);
    }

    [Fact]
    public void The_section_has_no_comment_opener_in_a_string_or_a_comment_so_that_the_header_check_can_read_it()
    {
        // ScriptTests strips block comments with a regex: a slash and a star in the section would swallow the code after it, and the section is inside the one of the upload
        Assert.DoesNotContain("/*", Section);
        Assert.Contains("data-autocomplete", BlockComment().Replace(Js, string.Empty));
    }

    [Fact]
    public void The_input_is_a_combobox_of_the_aria_pattern_with_a_listbox_options_groups_and_a_live_region()
    {
        foreach (var line in new[]
        {
            "input.setAttribute('role', 'combobox');", "input.setAttribute('aria-autocomplete', 'list');", "input.setAttribute('aria-haspopup', 'listbox');", "input.setAttribute('aria-expanded', 'false');",
            "input.setAttribute('aria-controls', s.list.id);", "s.list.setAttribute('role', 'listbox');", "a.setAttribute('role', 'option');", "a.setAttribute('aria-selected', 'false');",
            "box.setAttribute('role', 'group');", "box.setAttribute('aria-labelledby', head.id);", "s.live.setAttribute('role', 'status');", "s.live.setAttribute('aria-live', 'polite');",
            "s.input.setAttribute('aria-activedescendant', now.id);", "s.input.removeAttribute('aria-activedescendant');", "s.input.setAttribute('aria-expanded', 'true');",
        })
        {
            Assert.Contains(line, Section);
        }

        // an option is not in the order of the Tab key: the field is the one stop, the active option is named by aria-activedescendant
        Assert.Contains("a.tabIndex = -1;", Section);
        // a label of its own or the text of the control names the field
        Assert.Contains("input.setAttribute('aria-label', srText('label'))", Section);
        // the group is named by its heading and the list by what it holds
        Assert.Contains("s.list.setAttribute('aria-label', srText('suggestions'))", Section);
        Assert.Contains("s.list.setAttribute('aria-label', srText('recentList'))", Section);
        Assert.Contains("aria-busy", Section);
    }

    [Fact]
    public void The_keys_are_the_ones_of_the_rule_and_the_typed_text_is_never_replaced()
    {
        var keys = Section[Section.IndexOf("doc.addEventListener('keydown'", StringComparison.Ordinal)..];
        keys = keys[..keys.IndexOf("}, true);", StringComparison.Ordinal)];

        foreach (var key in new[] { "'ArrowDown'", "'ArrowUp'", "'Home'", "'End'", "'Enter'", "'Escape'", "'Tab'" })
        {
            Assert.Contains(key, keys);
        }

        // the arrow keys wrap, Home and End only count while an option is active
        Assert.Contains("(s.active + step + n) % n", Section);
        Assert.Contains("open && s.active >= 0", keys);
        // Enter follows the active option and otherwise leaves the form to send itself
        Assert.Contains("if (open && s.active >= 0) { e.preventDefault(); srChoose(s, s.opts[s.active], e); }", keys);
        // Escape closes first and clears second; Tab closes
        Assert.Contains("else if (open) { e.preventDefault(); e.stopPropagation(); srClose(s); }", keys);
        Assert.Contains("else if (s.input.value !== '') { e.preventDefault(); e.stopPropagation(); s.input.value = '';", keys);
        // the field keeps what was typed: nothing in the section writes the text of an option into the input except the choice of a recent query
        Assert.DoesNotContain("s.input.value = item.title", Section);
        Assert.DoesNotContain("s.input.value = opt", Section);
        Assert.Contains("s.input.value = model.query;", Section);
        // composition (an IME) is left alone
        Assert.Contains("e.isComposing", keys);
        // a dialog around the field closes after the popup
        Assert.Contains("doc.addEventListener('cancel'", Section);
    }

    [Fact]
    public void An_option_is_chosen_by_a_click_that_the_browser_follows_and_the_keys_make_the_same_click()
    {
        // one path: Enter dispatches a click (with the modifier keys) on the option, and the one handler of clicks does the rest
        Assert.Contains("opt.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, ctrlKey: e.ctrlKey, metaKey: e.metaKey, shiftKey: e.shiftKey, altKey: e.altKey }))", Section);
        Assert.Contains("new CustomEvent('mat:search-select', { bubbles: true, cancelable: true", Section);
        Assert.Contains("if (picked.defaultPrevented) { e.preventDefault(); srClose(s); return; }", Section);
        // a press on the popup does not take the focus from the field, or the click would find nothing
        Assert.Contains("closest('.mat-search__panel')) { e.preventDefault(); }", Section);
        // a link is an a with an href, a choice with a modifier key leaves the popup open
        Assert.Contains("srEl(item.href ? 'a' : 'button', model.cls || 'mat-search__option')", Section);
        Assert.Contains("e.ctrlKey || e.metaKey || e.shiftKey", Section);
    }

    [Fact]
    public void A_late_answer_is_dropped_a_failure_is_a_row_and_a_slow_answer_is_a_loading_row_after_200_ms()
    {
        var run = Section[Section.IndexOf("function srRun(", StringComparison.Ordinal)..];
        run = run[..run.IndexOf("// the choices that were made before", StringComparison.Ordinal)];

        Assert.Contains("typeof AbortController === 'function' ? new AbortController() : null", run);
        Assert.Contains("var mine = (s.seq += 1);", run);
        Assert.Contains("if (mine === s.seq)", run);
        Assert.Contains("}, 200);", run);
        Assert.Contains("srLoading(s)", run);
        Assert.Contains("srFailed(s, q)", run);
        Assert.Contains("err.name === 'AbortError'", run);
        Assert.Contains("'q=' + encodeURIComponent(q) + '&limit=' + srLimit(s)", run);
        Assert.Contains("credentials: 'same-origin'", run);
        // whatever stops the asking invalidates the answer that is on its way
        Assert.Contains("s.seq += 1;", Section[Section.IndexOf("function srStop(", StringComparison.Ordinal)..][..300]);
        // the failure keeps the way to the page of the results and says what can still be done
        Assert.Contains("srNote(s, 'error', srText('error')", Section);
        Assert.Contains("srText('errorHint')", Section);
    }

    [Fact]
    public void A_title_is_made_of_text_nodes_a_link_must_be_http_or_https_and_an_icon_name_is_checked()
    {
        // no title, line, heading, kind or note is ever assigned as markup: every one is textContent, or a text node inside a mark
        Assert.Contains("el.appendChild(doc.createTextNode(text.slice(at, r[0])))", Section);
        Assert.Contains("var mark = srEl('mark', 'mat-search__hit');", Section);
        Assert.Contains("mark.textContent = text.slice(r[0], r[1]);", Section);
        Assert.Contains("desc.textContent = item.text;", Section);
        Assert.Contains("head.textContent = g.name;", Section);
        Assert.Contains("kind.textContent = item.kind.charAt(0).toUpperCase() + item.kind.slice(1);", Section);
        Assert.Contains("line.textContent = text;", Section);
        // innerHTML only gets the markup of an icon, whose name was checked
        var assigned = Regex.Matches(Section, @"\.innerHTML = ([^;]+);").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(assigned);
        Assert.All(assigned, a => Assert.StartsWith("icon(", a));
        Assert.Contains("/^[a-z0-9-]+$/.test(it.icon)", Section);
        Assert.DoesNotContain("insertAdjacentHTML", Section);
        Assert.DoesNotContain("outerHTML", Section);
        // an address that is not http or https is no link
        Assert.Contains("/^https?:$/.test(new URL(h, location.href).protocol) ? h : ''", Section);
        Assert.Contains("if (item.href) { a.href = item.href; } else { a.type = 'button'; }", Section);
    }

    [Fact]
    public void The_recent_choices_are_five_kept_per_app_and_key_in_the_local_storage_and_can_be_forgotten()
    {
        Assert.Contains("var SR_RECENT = 'mat-search-recent';", Section);
        Assert.Contains("var SR_RECENT_MAX = 5;", Section);
        Assert.Contains("(root.getAttribute('data-app') || '') + ':' + (s.wrap.getAttribute('data-recent') || '')", Section);
        Assert.Contains("srRecentWrite(s, []);", Section);
        Assert.Contains("srText('recentCleared')", Section);
        // what is read back is checked again like data from a server (an address, an icon name)
        Assert.Contains("srItem(r)", Section);
        // a form that holds the search remembers the text when it is sent
        Assert.Contains("doc.addEventListener('submit'", Section);
    }

    [Fact]
    public void The_keys_that_focus_the_field_leave_the_keys_of_the_page_alone_and_the_hint_names_the_one_that_works()
    {
        Assert.Contains("function srKeyTaken(s, key)", Section);
        Assert.Contains("el.getAttribute('data-shortcut') === key && !s.wrap.contains(el)", Section);
        Assert.Contains("s.hint.textContent = keys.length", Section);
        Assert.Contains("aria-keyshortcuts", Section);
        Assert.Contains("'Control+' + k.key.toUpperCase() + ' Meta+' + k.key.toUpperCase()", Section);
        // not while typing in a field
        var shortcut = Section[Section.IndexOf("function srShortcut(", StringComparison.Ordinal)..];
        shortcut = shortcut[..shortcut.IndexOf("// ---- state of the control", StringComparison.Ordinal)];
        Assert.Contains("tag === 'input' || tag === 'textarea' || tag === 'select'", shortcut);
        Assert.Contains("isContentEditable", shortcut);
        Assert.Contains("(e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey", shortcut);
        // the field of the header bar and the examples never take the generic data-shortcut: they have their own attribute
        Assert.DoesNotContain("data-shortcut=\"/\"", Section);
    }

    [Fact]
    public void On_a_phone_the_focused_field_is_a_full_screen_panel_that_locks_the_page_and_the_header_bar_has_an_icon_button()
    {
        Assert.Contains("if (isPhone() && !s.noFull) { srEnterFull(s); }", Section);
        Assert.Contains("s.wrap.classList.add('is-full');", Section);
        Assert.Contains("|| !!searchFull); }", Js[Js.IndexOf("function syncLock()", StringComparison.Ordinal)..][..400]);
        Assert.Contains("hdr: !!wrap.closest('.mat-topbar__search')", Section);
        Assert.Contains("srEl('button', 'mat-btn mat-btn--ghost mat-btn--icon mat-search__open')", Section);
        Assert.Contains("srEl('button', 'mat-btn mat-btn--ghost mat-btn--icon mat-search__close')", Section);
        // the X closes it, Escape closes it, Tab stays inside, and a finger does not get the keyboard back
        Assert.Contains("srLeaveFull(x, true)", Section);
        Assert.Contains("matchMedia('(pointer: coarse)')", Section);
        Assert.Contains("window.addEventListener('pageshow'", Section);
        // a panel that is moved by the browser's back button comes back closed
        Assert.Contains("e.persisted", Section);
    }

    [Fact]
    public void The_script_builds_no_name_from_parts_and_the_white_label_build_renames_what_the_search_adds()
    {
        var acme = new WhiteLabelSetting("acme", "AcmeUi");
        var script = WhiteLabel.Rename(Js, acme);
        var css = WhiteLabel.Rename(Css, acme);
        var shell = WhiteLabel.Rename(ShellCss, acme);

        Assert.Contains("var SR_RECENT = 'acme-search-recent';", script);
        Assert.Contains("'mat:search-select'".Replace("mat:", "acme:"), script);
        Assert.Contains("srEl('mark', 'acme-search__hit')", script);
        Assert.Contains("search: search,", script);
        Assert.Contains("window.AcmeUi = {", script);
        Assert.Contains(".acme-search__option.is-active", css);
        Assert.Contains(".acme-topbar > .acme-topbar__search { order: 1;", shell);
        Assert.Contains("--acme-search-width", shell);
        Assert.Empty(WhiteLabel.Leftovers(script, acme));
        Assert.Empty(WhiteLabel.Leftovers(css, acme));
        Assert.Empty(WhiteLabel.Leftovers(shell, acme));
        // the examples of the catalogue are renamed as markup, and what they say still passes the contract of an example
        var examples = Component.Examples.Select(e => WhiteLabel.Rename(e.Html, acme)).ToList();
        Assert.All(examples, e => Assert.Contains("acme-search", e));
        Assert.DoesNotContain("mat-search", string.Join('\n', examples));
        Assert.Contains("data-autocomplete", examples[0]);
    }

    // ---------------------------------------------------------------------------------------------------- the CSS

    [Fact]
    public void The_css_sits_before_the_tags_in_the_pickers_part_and_the_shell_part_places_the_field_in_the_header_bar()
    {
        Assert.True(Css.IndexOf("Search with suggestions (FORM-23, SHELL-20)", StringComparison.Ordinal) < Css.IndexOf("Tags: a field of chips", StringComparison.Ordinal));
        // the tests of the tag input read the last phone block of the file: it is theirs
        Assert.True(Css.LastIndexOf("@media (max-width: 640px)", StringComparison.Ordinal) > Css.IndexOf("Tags: a field of chips", StringComparison.Ordinal));
        Assert.Contains(".mat-topbar > .mat-topbar__search { order: 1;", ShellCss);
        Assert.Contains(".mat-topbar > .mat-topbar__actions { order: 2; }", ShellCss);
        Assert.Contains("flex: 0 1 var(--mat-search-width, 18rem);", ShellCss);
        Assert.Contains("@prop search | --mat-search-width | length | 18rem |", ShellCss);
    }

    [Fact]
    public void The_field_takes_its_shape_from_the_documented_properties_and_the_popup_is_fixed_like_the_panels_of_the_pickers()
    {
        Assert.Contains("min-height: var(--mat-search-height, var(--mat-control-h));", Rule(".mat-search__input"));
        Assert.Contains("border-radius: var(--mat-search-radius, var(--mat-radius));", Rule(".mat-search__input"));
        Assert.Contains("@prop search | --mat-search-radius | length | var(--mat-radius) |", Style);
        Assert.Contains("@prop search | --mat-search-height | length | var(--mat-control-h) |", Style);
        var panel = Rule(".mat-search__panel");
        Assert.Contains("position: fixed; z-index: var(--mat-z-menu);", panel);
        Assert.Contains("max-height: min(28rem, calc(100dvh - 16px));", panel);
        Assert.Contains("overflow: auto;", panel);
        Assert.Contains("border-radius: var(--mat-radius-lg);", panel);
        Assert.Contains("box-shadow: var(--mat-shadow-3);", panel);
        Assert.Contains("border: var(--mat-input-border-width, 1px) solid var(--mat-border-strong);", Rule(".mat-search__input"));
    }

    [Fact]
    public void The_active_option_is_a_tint_behind_the_whole_row_and_the_hit_is_bold_not_only_coloured()
    {
        // COLOR-08: no bar at one side; COLOR-04: the part that matches is bold as well as in the colour of a link
        Assert.Contains("background: var(--mat-accent-soft-strong);", Style[Style.IndexOf(".mat-search__option.is-active", StringComparison.Ordinal)..][..200]);
        Assert.DoesNotMatch(@"border-(left|right)\s*:", Style);
        Assert.DoesNotMatch(@"inset-inline-start|box-shadow: inset", Style);
        var hit = Rule(".mat-search__hit");
        Assert.Contains("font-weight: 750;", hit);
        Assert.Contains("color: var(--mat-accent-text);", hit);
        Assert.Contains("background: transparent;", hit);
        Assert.Empty(OneSidedAccentTests.Violations("matui.pickers.css", Css));
    }

    [Fact]
    public void The_search_is_flat_and_made_of_tokens_only()
    {
        var style = BlockComment().Replace(Style, string.Empty);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", style);
        Assert.DoesNotMatch(@"\brgba?\(|\bhsla?\(|\boklch\(", style);
        Assert.DoesNotContain("!important", style);
        Assert.DoesNotContain("gradient(", style);
        Assert.DoesNotContain("blur(", style);
        Assert.DoesNotContain("filter:", style);
        Assert.DoesNotContain("backdrop-filter", style);
        Assert.DoesNotMatch(@"box-shadow: (?!var\(--mat-(focus-ring|shadow-3)\)|none)", style);
        // the colours that mean something: danger for a failure, the muted text for the line, the accent for what matches
        Assert.Contains("var(--mat-danger-text)", Rule(".mat-search__note.is-error"));
        Assert.Contains("color: var(--mat-text-muted);", Rule(".mat-search__desc"));
        // sizes of type are the scales' (or the 16 px of a phone, and the size of an icon)
        Assert.DoesNotMatch(@"\bfont-size: \d", Regex.Replace(style, @"font-size: 1(\.1|\.15)?rem", string.Empty));
    }

    [Fact]
    public void A_phone_gets_a_full_screen_panel_and_a_touch_screen_gets_rows_and_a_clear_button_of_44_px()
    {
        var phone = Style[Style.IndexOf("@media (max-width: 640px) {", StringComparison.Ordinal)..];
        phone = phone[..phone.IndexOf("/* ---- Touch targets", StringComparison.Ordinal)];
        Assert.Contains("position: fixed; inset: 0; z-index: var(--mat-z-menu);", phone);
        Assert.Contains(".mat-search.is-full .mat-search__close { display: inline-flex; flex: none; }", phone);
        Assert.Contains("position: static; flex: 1 1 auto;", phone);
        Assert.Contains("padding-top: var(--mat-safe-top); padding-bottom: var(--mat-safe-bottom);", phone);
        // type of 16 px: a phone zooms into smaller type
        Assert.Contains(".mat-search__input { font-size: 1rem; }", phone);
        // the close X and the open button exist on a phone only (the shell part shows the open button below 640 px)
        Assert.Contains(".mat-search__close, .mat-search__open { display: none; }", Style);
        Assert.Contains(".mat-topbar__search .mat-search:not(.is-full) .mat-search__open { display: inline-flex; }", ShellCss);
        Assert.Contains(".mat-topbar__search .mat-search:not(.is-full) .mat-search__bar { display: none; }", ShellCss);
        var touch = Style[Style.IndexOf("@media (pointer: coarse) {", StringComparison.Ordinal)..];
        Assert.Contains(".mat-search__option { min-height: 44px; }", touch);
        Assert.Contains(".mat-search__clear::before { content: \"\"; position: absolute; left: 50%; top: 50%; width: 44px; height: 44px; transform: translate(-50%, -50%); }", touch);
    }

    [Fact]
    public void Hidden_parts_have_their_own_rule_the_key_hint_goes_on_a_small_screen_and_the_native_clear_button_is_not_drawn()
    {
        foreach (var selector in new[] { ".mat-search__panel[hidden]", ".mat-search__clear[hidden]", ".mat-search__key[hidden]", ".mat-search__note[hidden]" })
        {
            Assert.Contains(selector + " { display: none; }", Style);
        }

        Assert.Contains("@media (max-width: 900px) { .mat-search__key { display: none; } }", Style);
        Assert.Contains(".mat-search.is-filled .mat-search__key, .mat-search:focus-within .mat-search__key { display: none; }", Style);
        Assert.Contains("::-webkit-search-cancel-button", Style);
        Assert.Contains("@media (forced-colors: active)", Style);
        Assert.Contains("outline: 2px solid Highlight;", Style);
        Assert.Contains("overflow-wrap: anywhere;", Rule(".mat-search__title"));
        Assert.Contains("text-overflow: ellipsis;", Rule(".mat-search__desc"));
    }

    // ---------------------------------------------------------------------------------------------------- the component properties

    [Fact]
    public void The_search_has_three_documented_properties_a_group_a_preview_and_the_floor_of_a_pressed_control()
    {
        var properties = ComponentProperties.Parse(ToolkitFiles.CssParts.Select(p => TestPaths.ReadToolkit("css/" + p)));
        var search = properties.Where(p => p.Component == "search").ToList();

        Assert.Equal(["--mat-search-height", "--mat-search-radius", "--mat-search-width"], search.Select(p => p.Name).Order());
        Assert.Equal("var(--mat-control-h)", search.Single(p => p.Name == "--mat-search-height").Fallback);
        Assert.Equal("18rem", search.Single(p => p.Name == "--mat-search-width").Fallback);
        Assert.All(search, p => Assert.Equal("length", p.Type));
        var group = ComponentProperties.Components.Single(c => c.Id == "search");
        Assert.Equal(["search", "topbar"], group.Docs);
        Assert.All(group.Docs, d => Assert.Contains(ComponentCatalog.All, c => c.Id == d));
        Assert.Contains("--mat-search-height", ComponentTokens.TouchProperties);
        Assert.NotNull(PropertyPreviews.Of("search"));
        Assert.Equal("search", PropertyPreviews.Of("search")!.Doc);
        // nothing sets one of them anywhere in the toolkit: the fallback is the default
        Assert.DoesNotMatch(@"--mat-search-(radius|height|width)\s*:", string.Join('\n', ToolkitFiles.CssParts.Select(p => BlockComment().Replace(TestPaths.ReadToolkit("css/" + p), string.Empty))));
    }

    [Fact]
    public void The_width_of_the_field_in_the_header_bar_is_held_to_limits_and_the_height_to_the_touch_floor()
    {
        var tokens = CssTokens.Tokens(TestPaths.ReadToolkit("css/matui.tokens.css"));
        var props = ComponentTokens.Editable(ComponentProperties.Parse(ToolkitFiles.CssParts.Select(p => TestPaths.ReadToolkit("css/" + p))), tokens);
        PropertyToken Prop(string name) => props.Single(p => p.Name == name);
        string? Check(string name, string value) => BrandValidator.CheckValue(Prop(name).Token, BrandValidator.NormalizeValue(Prop(name).Token, value));

        Assert.Null(Check("--mat-search-width", "10rem"));
        Assert.Null(Check("--mat-search-width", "560px"));
        Assert.Contains("below the least", Check("--mat-search-width", "159px"), StringComparison.Ordinal);
        Assert.Contains("above the most", Check("--mat-search-width", "561px"), StringComparison.Ordinal);
        Assert.Null(Check("--mat-search-radius", "0"));
        Assert.Contains("above the most", Check("--mat-search-radius", "25px"), StringComparison.Ordinal);
        Assert.Null(Check("--mat-search-height", "36px"));
        Assert.Contains("MOBILE-04", Check("--mat-search-height", "31px"), StringComparison.Ordinal);
        Assert.True(Prop("--mat-search-height").Touch);
        Assert.True(ComponentTokens.AppliesToFinePointerOnly(Prop("--mat-search-height"), "36px"));
        Assert.False(ComponentTokens.AppliesToFinePointerOnly(Prop("--mat-search-width"), "200px"));
    }

    // ---------------------------------------------------------------------------------------------------- the documents

    [Fact]
    public void The_documents_say_what_an_app_does_and_what_the_decisions_were()
    {
        var adopting = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md"));
        var architecture = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ARCHITECTURE.md"));
        var changelog = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "CHANGELOG.md"));

        foreach (var word in new[] { "FORM-23", "mat-search", "data-search-url", "data-sr-<name>", "MatUi.search", "SHELL-20", "mat-topbar__search" })
        {
            Assert.Contains(word, adopting);
        }

        Assert.Contains("### Search", architecture);
        Assert.Contains("SearchIndex", architecture);
        // the entry stays in the changelog when a version is made from it (no test names a version of the toolkit)
        Assert.Contains("`mat-search`", changelog);
        Assert.Contains("`SHELL-20`", changelog);
    }
}
