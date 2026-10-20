using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The changelog of the rulebook: the data against the rules (a rule cannot change without a line that says so), the way a line is checked, the page that shows it, the page of a rule with its
/// history, and the API. The rules are the product, so what happens to them is written down like what happens to the toolkit.
/// </summary>
public sealed partial class RuleChangesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static readonly DateOnly Today = new(2026, 10, 20);

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"<tr class=""mat-group__item"">")]
    private static partial Regex ItemRow();

    [GeneratedRegex(@"<span class=""doc-rule-id"">([A-Z0-9]+-\d+)</span>")]
    private static partial Regex RuleIdCell();

    [GeneratedRegex(@"<td data-label=""Date"" class=""mat-mono mat-text-nowrap"">(\d{4}-\d{2}-\d{2})</td>")]
    private static partial Regex DateCell();

    [GeneratedRegex(@"<span class=""mat-pill mat-pill--[a-z]+"">(Added|Changed|Removed|Moved)</span>")]
    private static partial Regex KindPill();

    private async Task<string> Html(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    // ------------------------------------------------------------------------------------------------------ the data against the rulebook

    [Fact]
    public void The_changelog_of_the_rulebook_is_as_it_should_be_for_the_rules_of_the_book()
    {
        // The message of each problem says what to add: a rule that changed without a line fails here.
        var problems = RuleChanges.Problems();
        Assert.True(problems.Count == 0, "The changelog of the rulebook (src/MatDesign.Core/RuleChangeEntries.cs) does not match RuleBook.cs:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Every_rule_has_a_line_that_says_when_it_was_added_and_the_newest_line_is_for_its_text()
    {
        foreach (var rule in RuleBook.All)
        {
            var lines = RuleChanges.For(rule.Id);
            Assert.True(lines.Count > 0, $"{rule.Id} has no line");
            Assert.Equal(RuleChangeKind.Added, lines[0].Kind);
            Assert.Single(lines, l => l.Kind == RuleChangeKind.Added);
            Assert.Equal(RuleChanges.HashOf(rule), lines[^1].Hash);
        }
    }

    [Fact]
    public void No_line_is_about_a_rule_that_does_not_exist_unless_it_says_that_it_is_gone()
    {
        var ids = RuleBook.All.Select(r => r.Id).ToHashSet();
        foreach (var group in RuleChanges.All.GroupBy(c => c.RuleId))
        {
            Assert.True(ids.Contains(group.Key) || group.Last().EndsTheId, $"{group.Key} is not a rule and its last line does not say it is gone");
        }
    }

    [Fact]
    public void The_baseline_has_a_line_for_every_rule_on_the_day_it_first_stood_in_the_book()
    {
        // 2026-10-10 is the day the rulebook was created (the first commit); nothing can be older, and nothing is later than today
        var added = RuleChanges.All.Where(c => c.Kind == RuleChangeKind.Added).ToList();
        Assert.All(added, c => Assert.InRange(c.Date, new DateOnly(2026, 10, 10), Today.AddYears(1)));
        Assert.Contains(added, c => c.RuleId == "IDENT-01" && c.Date == new DateOnly(2026, 10, 10));
        // a rule of a later stage was not added on the first day (checked against git: tools/rule-history.js --first)
        Assert.Equal(new DateOnly(2026, 10, 15), added.Single(c => c.RuleId == "CODE-10").Date);
        Assert.Equal(new DateOnly(2026, 10, 14), added.Single(c => c.RuleId == "LIST-17").Date);
        Assert.Equal(new DateOnly(2026, 10, 13), added.Single(c => c.RuleId == "COLOR-10").Date);
    }

    [Fact]
    public void The_lines_are_oldest_first_dated_and_have_notes_that_are_short_sentences()
    {
        Assert.Equal(RuleChanges.All.OrderBy(c => c.Date).Select(c => c.DateText), RuleChanges.All.Select(c => c.DateText));
        foreach (var change in RuleChanges.All.Where(c => c.Note.Length > 0))
        {
            Assert.True(change.Note.Length <= RuleChanges.MaxNote, $"{change.RuleId}: {change.Note.Length} characters");
            Assert.EndsWith(".", change.Note);
            Assert.DoesNotContain('\n', change.Note);
        }

        Assert.All(RuleChanges.All.Where(c => c.Kind != RuleChangeKind.Added), c => Assert.NotEmpty(c.Note));
    }

    // ------------------------------------------------------------------------------------------------------ the hash

    [Fact]
    public void The_hash_is_eight_hex_digits_of_the_statement_the_level_and_the_source_and_nothing_else()
    {
        // a fixed vector: the first eight digits of SHA-256 of "a\nMust\nOwner" (so that the formula is not changed by accident; tools/rule-hash.js uses the same)
        Assert.Equal("fddda9d1", RuleChanges.HashOf("a", RuleLevel.Must, "Owner"));
        Assert.Equal("8da03703", RuleChanges.HashOf("Ä € `x`", RuleLevel.Should, "MatDesign"));

        var rule = new Rule("FORM-90", "FORM", "Title", "A statement that says something.", RuleLevel.Should, "MatDesign", Why: "why", Do: "do", Dont: "dont", Check: "Test");
        var hash = RuleChanges.HashOf(rule);
        Assert.Matches("^[0-9a-f]{8}$", hash);
        Assert.Equal(hash, RuleChanges.HashOf(rule with { Title = "Another title", Why = null, Do = null, Dont = null, Check = "Review" }));
        Assert.NotEqual(hash, RuleChanges.HashOf(rule with { Statement = "A statement that says something else." }));
        Assert.NotEqual(hash, RuleChanges.HashOf(rule with { Level = RuleLevel.Must }));
        Assert.NotEqual(hash, RuleChanges.HashOf(rule with { Source = "Owner" }));
    }

    private static (int Code, string Output)? Node(params string[] args)
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(true);
                throw new TimeoutException("node did not finish: " + string.Join(' ', args));
            }

            return (process.ExitCode, output.Result + error.Result);
        }
        catch (Win32Exception) when (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
        {
            // no node on this machine: the tool has nothing to run here (the build of the repository has node, so there it always runs)
            return null;
        }
    }

    [Fact]
    public void The_tool_that_prints_the_line_to_add_computes_the_hash_the_book_computes_for_every_rule()
    {
        // node tools/rule-hash.js reads RuleBook.cs as text; if it cannot read a rule, or computes another hash, a line it prints would not satisfy the test above
        var result = Node(Path.Combine(TestPaths.RepoRoot, "tools", "rule-hash.js"), "--all");
        if (result is null)
        {
            return;
        }

        Assert.True(result.Value.Code == 0, result.Value.Output);
        var fromNode = result.Value.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim().Split(' ')).ToDictionary(p => p[0], p => p[1]);
        Assert.Equal(RuleBook.All.Count, fromNode.Count);
        foreach (var rule in RuleBook.All)
        {
            Assert.True(fromNode.TryGetValue(rule.Id, out var hash), $"{rule.Id}: the tool did not find the rule");
            Assert.Equal(RuleChanges.HashOf(rule), hash);
        }
    }

    [Fact]
    public void The_tool_prints_the_line_to_paste_with_today_and_the_note()
    {
        var result = Node(Path.Combine(TestPaths.RepoRoot, "tools", "rule-hash.js"), "FORM-15", "The statement says more.");
        if (result is null)
        {
            return;
        }

        Assert.True(result.Value.Code == 0, result.Value.Output);
        var rule = RuleBook.Get("FORM-15")!;
        Assert.Contains($"Chg(\"{DateTime.UtcNow:yyyy-MM-dd}\", \"FORM-15\", \"{RuleChanges.HashOf(rule)}\", \"The statement says more.\"),", result.Value.Output);
        Assert.Equal(1, Node(Path.Combine(TestPaths.RepoRoot, "tools", "rule-hash.js"), "NOPE-99")!.Value.Code);
    }

    // ------------------------------------------------------------------------------------------------------ what is wrong, and what the message says

    private static Rule R(string id, string statement = "The statement of the rule says something.", RuleLevel level = RuleLevel.Should, string source = "MatDesign")
        => new(id, id[..id.IndexOf('-')], "A title of the rule", statement, level, source);

    private static RuleChange Line(string date, string id, RuleChangeKind kind, string hash, string note = "")
        => new(DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), id, kind, note, hash);

    private static RuleChange Added(Rule rule, string date = "2026-10-10") => Line(date, rule.Id, RuleChangeKind.Added, RuleChanges.HashOf(rule));

    private static IReadOnlyList<string> Check(IReadOnlyList<Rule> rules, params RuleChange[] lines) => RuleChanges.Problems(rules, lines, Today);

    [Fact]
    public void A_rule_with_a_line_for_its_text_is_fine()
    {
        var rule = R("FORM-90");
        Assert.Empty(Check([rule], Added(rule)));
        var changed = rule with { Statement = "A statement that says something else." };
        Assert.Empty(Check([changed], Added(rule), Line("2026-10-12", "FORM-90", RuleChangeKind.Changed, RuleChanges.HashOf(changed), "The statement says something else.")));
    }

    [Fact]
    public void A_rule_without_a_line_is_found_and_the_message_gives_the_line_to_add()
    {
        var rule = R("FORM-90");
        var problem = Assert.Single(Check([rule]));
        Assert.Contains("FORM-90", problem);
        Assert.Contains("no line", problem);
        Assert.Contains($"Add(\"2026-10-20\", \"FORM-90\", \"{RuleChanges.HashOf(rule)}\",", problem);
    }

    [Fact]
    public void A_rule_that_changed_without_a_line_is_found_and_the_message_says_what_to_add()
    {
        var before = R("FORM-90");
        var after = before with { Statement = "A statement that was rewritten." };
        var problem = Assert.Single(Check([after], Added(before)));
        Assert.Contains("FORM-90", problem);
        Assert.Contains(RuleChanges.HashOf(before), problem);
        Assert.Contains(RuleChanges.HashOf(after), problem);
        Assert.Contains("RuleChangeEntries.cs", problem);
        Assert.Contains($"Chg(\"2026-10-20\", \"FORM-90\", \"{RuleChanges.HashOf(after)}\",", problem);
        Assert.Contains("tools/rule-hash.js FORM-90", problem);

        // the level and the source count as well
        Assert.Single(Check([before with { Level = RuleLevel.Must }], Added(before)));
        Assert.Single(Check([before with { Source = "Owner" }], Added(before)));
        // the title does not
        Assert.Empty(Check([before with { Title = "Another title for it" }], Added(before)));
    }

    [Fact]
    public void A_line_about_a_rule_that_does_not_exist_is_found_unless_the_rule_was_removed_or_moved()
    {
        var gone = R("FORM-91");
        var problem = Assert.Single(Check([], Added(gone)));
        Assert.Contains("no such rule", problem);
        Assert.Contains("Rem(", problem);

        Assert.Empty(Check([], Added(gone), Line("2026-10-12", "FORM-91", RuleChangeKind.Removed, RuleChanges.HashOf(gone), "The rule is part of FORM-12 now.")));
        var home = R("FORM-12");
        Assert.Empty(Check([home], Added(home), Added(gone), Line("2026-10-12", "FORM-91", RuleChangeKind.Moved, RuleChanges.HashOf(gone), "Now FORM-12.")));
    }

    [Fact]
    public void A_removed_rule_stays_removed_and_a_rule_that_is_in_the_book_is_not_removed()
    {
        var rule = R("FORM-90");
        var removed = Line("2026-10-12", "FORM-90", RuleChangeKind.Removed, RuleChanges.HashOf(rule), "Gone for good.");
        Assert.Contains(Check([rule], Added(rule), removed), p => p.Contains("the newest line says Removed, but the rule is in the rulebook"));
        Assert.Contains(Check([], Added(rule), removed, Line("2026-10-13", "FORM-90", RuleChangeKind.Changed, RuleChanges.HashOf(rule), "Back again.")), p => p.Contains("An id that is gone stays gone"));
    }

    [Fact]
    public void The_first_line_is_Added_and_there_is_one_Added_line_for_an_id()
    {
        var rule = R("FORM-90");
        Assert.Contains(Check([rule], Line("2026-10-10", "FORM-90", RuleChangeKind.Changed, RuleChanges.HashOf(rule), "Changed.")), p => p.Contains("must be Added"));
        Assert.Contains(Check([rule], Added(rule), Added(rule, "2026-10-11")), p => p.Contains("Added twice"));
    }

    [Fact]
    public void The_lines_must_be_oldest_first_and_dated_days_that_are_not_in_the_future()
    {
        var one = R("FORM-90");
        var two = R("FORM-91");
        Assert.Contains(Check([one, two], Added(one, "2026-10-12"), Added(two, "2026-10-11")), p => p.Contains("oldest first"));
        Assert.Contains(Check([one], Added(one, "2026-10-25")), p => p.Contains("not a day between"));
        Assert.Contains(Check([one], Added(one, "2025-12-31")), p => p.Contains("not a day between"));
        Assert.Empty(Check([one], Added(one, "2026-10-21")));
    }

    [Theory]
    [InlineData("The statement says it.", false)]
    [InlineData("", true)]
    [InlineData("No full stop at the end", true)]
    [InlineData("a small letter first.", true)]
    [InlineData("Two\nlines.", true)]
    [InlineData(" Spaces at the start.", true)]
    [InlineData("`mat-btn` is named in the statement.", false)]
    [InlineData("The statement now says that the touch target is 44 px.", false)]
    public void A_note_is_one_short_sentence(string note, bool problem)
    {
        var rule = R("FORM-90");
        var changed = rule with { Statement = "A statement that was rewritten." };
        var problems = Check([changed], Added(rule), Line("2026-10-12", "FORM-90", RuleChangeKind.Changed, RuleChanges.HashOf(changed), note));
        Assert.Equal(problem, problems.Count > 0);
    }

    [Fact]
    public void A_note_is_not_longer_than_a_short_sentence_and_a_moved_line_names_the_new_id()
    {
        var rule = R("FORM-90");
        var changed = rule with { Statement = "A statement that was rewritten." };
        var long_ = "The statement " + new string('x', RuleChanges.MaxNote) + ".";
        Assert.Contains(Check([changed], Added(rule), Line("2026-10-12", "FORM-90", RuleChangeKind.Changed, RuleChanges.HashOf(changed), long_)), p => p.Contains("keep it to"));

        var gone = R("FORM-91");
        Assert.Contains(Check([], Added(gone), Line("2026-10-12", "FORM-91", RuleChangeKind.Moved, RuleChanges.HashOf(gone), "It moved somewhere.")), p => p.Contains("names the new id"));
        Assert.Contains(Check([], Added(gone), Line("2026-10-12", "FORM-91", RuleChangeKind.Moved, RuleChanges.HashOf(gone), "Now FORM-77.")), p => p.Contains("names the new id"));
    }

    [Fact]
    public void An_id_of_no_area_and_a_hash_that_is_no_hash_are_found()
    {
        var odd = R("FORM-90") with { Id = "NOAREA-90", Area = "NOAREA" };
        Assert.Contains(Check([odd], Added(odd)), p => p.Contains("known area"));
        var rule = R("FORM-90");
        Assert.Contains(Check([rule], Line("2026-10-10", "FORM-90", RuleChangeKind.Added, "XYZ")), p => p.Contains("eight lower-case hex digits"));
    }

    // ------------------------------------------------------------------------------------------------------ the page

    [Fact]
    public async Task The_changes_page_is_a_list_with_its_toolbar_above_one_title_and_no_inline_style()
    {
        var html = await Html("/Rules/Changes");

        Assert.Single(Heading().Matches(html));
        Assert.Contains("<h1>What changed in the rulebook</h1>", html);
        Assert.False(InlineStyle().IsMatch(html), "an inline style (CODE-02)");
        Assert.Contains("class=\"mat-crumbs\"", html);
        Assert.Contains("class=\"mat-toolbar\"", html);
        Assert.True(html.IndexOf("class=\"mat-toolbar\"", StringComparison.Ordinal) < html.IndexOf("<table class=\"mat-table\"", StringComparison.Ordinal), "the toolbar is not above the table (LIST-02)");
        // the search is a field in the bar and Filter and Sort stand at its right end; the dialogs are Filter and Sort (LIST-03)
        Assert.Contains("class=\"mat-input-icon mat-toolbar__search\"", html);
        Assert.Contains("class=\"mat-toolbar__end\"", html);
        Assert.Contains("id=\"rulechanges-filter\"", html);
        Assert.Contains("id=\"rulechanges-sort\"", html);
        Assert.DoesNotContain("id=\"rulechanges-search\"", html);
        Assert.Contains($"{RuleChanges.All.Count} changes", html);
        // the way back and the data
        Assert.Contains("href=\"/api/rules/changes\"", html);
        Assert.Contains("Back to the rulebook", html);
        // the Studio keeps the history of what it writes itself, and the page says so
        Assert.Contains("keep the history of their revisions", html);
    }

    [Fact]
    public async Task The_changes_are_grouped_by_month_in_a_table_that_the_toolkit_knows_and_every_cell_has_a_label()
    {
        var html = await Html("/Rules/Changes");

        Assert.Contains("data-groups=\"rule-changes\"", html);
        Assert.Contains("<tbody class=\"mat-group\" data-group=\"2026-10\">", html);
        Assert.Contains("October 2026", html);
        Assert.Contains("data-group-toggle aria-expanded=\"true\"", html);
        var table = html[html.IndexOf("<table class=\"mat-table\"", StringComparison.Ordinal)..html.IndexOf("</table>", StringComparison.Ordinal)];
        var cells = Regex.Matches(table, @"<td\b([^>]*)>").Select(m => m.Groups[1].Value).ToList();
        Assert.All(cells, c => Assert.True(c.Contains("data-label=") || c.Contains("mat-group__cell"), "a cell without a label: " + c));
        Assert.Contains("<th scope=\"col\">What changed</th>", table);
    }

    [Fact]
    public async Task The_newest_line_is_first_and_a_page_has_thirty_lines_with_a_pager_under_the_table()
    {
        var html = await Html("/Rules/Changes");
        var dates = DateCell().Matches(html).Select(m => m.Groups[1].Value).ToList();

        Assert.Equal(Math.Min(30, RuleChanges.All.Count), ItemRow().Matches(html).Count);
        Assert.Equal(dates.OrderByDescending(d => d, StringComparer.Ordinal), dates);
        Assert.Equal(RuleChanges.All.Max(c => c.DateText), dates[0]);
        if (RuleChanges.All.Count > 30)
        {
            Assert.Contains("class=\"mat-pager\"", html);
            Assert.True(html.IndexOf("</table>", StringComparison.Ordinal) < html.IndexOf("class=\"mat-pager\"", StringComparison.Ordinal));
            var second = await Html("/Rules/Changes?page=2");
            Assert.NotEqual(RuleIdCell().Matches(html).Select(m => m.Groups[1].Value), RuleIdCell().Matches(second).Select(m => m.Groups[1].Value));
            Assert.Contains("aria-current=\"page\">2</span>", second);
        }
    }

    [Fact]
    public async Task Every_line_names_its_rule_as_a_link_and_the_kind_as_a_word_in_a_pill()
    {
        var html = await Html("/Rules/Changes?sort=id");
        var first = RuleIdCell().Match(html).Groups[1].Value;

        Assert.Equal(RuleBook.All[0].Id, first);
        Assert.Contains($"<a href=\"/Rules/Detail?id={first}\">{RuleBook.All[0].Title}</a>", html);
        Assert.Contains("<span class=\"mat-pill mat-pill--ok\">Added</span>", html);
        Assert.Equal(ItemRow().Matches(html).Count, KindPill().Matches(html).Count);
        // a line that was added has no note of its own: the page says what it is
        Assert.Contains("The rule was added.", html);
    }

    [Fact]
    public async Task The_kind_the_area_and_the_search_narrow_the_list_and_what_is_not_understood_is_ignored()
    {
        var byKind = await Html("/Rules/Changes?kind=Changed");
        var changed = RuleChanges.All.Count(c => c.Kind == RuleChangeKind.Changed);
        if (changed == 0)
        {
            Assert.Contains("No change matches", byKind);
        }
        else
        {
            Assert.Equal(Math.Min(30, changed), ItemRow().Matches(byKind).Count);
            Assert.All(KindPill().Matches(byKind), m => Assert.Equal("Changed", m.Groups[1].Value));
            Assert.Contains("Kind of change: Changed", byKind);
        }

        var byArea = await Html("/Rules/Changes?area=SHELL");
        Assert.Equal(Math.Min(30, RuleChanges.All.Count(c => c.RuleId.StartsWith("SHELL-"))), ItemRow().Matches(byArea).Count);
        Assert.All(RuleIdCell().Matches(byArea), m => Assert.StartsWith("SHELL-", m.Groups[1].Value));
        Assert.Contains("Area: Shell", byArea);

        var bySearch = await Html("/Rules/Changes?q=form-15");
        Assert.Contains("FORM-15", bySearch);
        Assert.DoesNotContain("LIST-01", bySearch);
        Assert.Contains("value=\"form-15\"", bySearch);

        // both at once, and a value that is not one is dropped instead of breaking the page
        var both = await Html("/Rules/Changes?area=FORM&kind=Added");
        Assert.All(RuleIdCell().Matches(both), m => Assert.StartsWith("FORM-", m.Groups[1].Value));
        var nonsense = await Html("/Rules/Changes?kind=Nonsense&area=NOPE&sort=nothing&page=999");
        Assert.True(ItemRow().Matches(nonsense).Count > 0);
        Assert.DoesNotContain("Kind of change: Nonsense", nonsense);
    }

    [Fact]
    public async Task A_search_that_finds_nothing_is_an_empty_state_with_the_way_out()
    {
        var html = await Html("/Rules/Changes?q=zzzzzz");

        Assert.Contains("No change matches", html);
        Assert.Contains("<a class=\"mat-btn mat-btn--primary\" href=\"/Rules/Changes\">", html);
        Assert.DoesNotContain("<table class=\"mat-table\"", html);
        Assert.Contains("0 of", html);
    }

    [Fact]
    public async Task The_sort_can_follow_the_book_or_the_kind_and_be_turned_around()
    {
        var byId = RuleIdCell().Matches(await Html("/Rules/Changes?sort=id")).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(byId.OrderBy(i => RuleBook.All.Select(r => r.Id).ToList().IndexOf(i)), byId);

        var reversed = RuleIdCell().Matches(await Html("/Rules/Changes?sort=id&dir=desc")).Select(m => m.Groups[1].Value).First();
        Assert.Equal(RuleBook.All[^1].Id, reversed);

        var oldest = DateCell().Matches(await Html("/Rules/Changes?dir=desc")).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(RuleChanges.All.Min(c => c.DateText), oldest[0]);
        var byKind = KindPill().Matches(await Html("/Rules/Changes?sort=kind")).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal("Added", byKind[0]);
        Assert.Contains("Date (the newest first)", await Html("/Rules/Changes"));
    }

    [Fact]
    public async Task The_rulebook_the_changelog_and_the_download_page_lead_to_the_changes()
    {
        var rulebook = await Html("/Rules");
        Assert.Contains("href=\"/Rules/Changes\"", rulebook);
        Assert.Contains("what changed in them", rulebook);
        Assert.Contains("What changed</span>", rulebook);
        Assert.Contains($"{RuleBook.All.Count} rules in {RuleBook.Areas.Count} areas", rulebook);
        Assert.Single(Heading().Matches(rulebook));

        Assert.Contains("href=\"/Rules/Changes\"", await Html("/Changelog"));
        var download = await Html("/Download");
        Assert.Contains("href=\"/Rules/Changes\"", download);
        Assert.Contains("/api/rules/changes", download);
    }

    // ------------------------------------------------------------------------------------------------------ the page of a rule

    [Fact]
    public async Task The_page_of_a_rule_has_its_history_with_the_lines_the_changelog_has_about_it()
    {
        var html = await Html("/Rules/Detail?id=SHELL-01");

        Assert.Single(Heading().Matches(html));
        Assert.Contains("<h2 id=\"history-title\">History</h2>", html);
        var section = html[html.IndexOf("id=\"history-title\"", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("aria-labelledby=\"area-title\"", StringComparison.Ordinal)];
        Assert.Contains("2026-10-10", section);
        Assert.Contains("<span class=\"mat-pill mat-pill--ok\">Added</span>", section);
        Assert.Contains("data-label=\"What changed\"", section);
        Assert.Contains("href=\"/Rules/Changes\"", section);
        Assert.DoesNotContain("History in the Studio", section);
    }

    [Fact]
    public async Task The_history_of_a_rule_that_changed_lists_every_line_the_newest_first_with_its_note()
    {
        foreach (var group in RuleChanges.All.GroupBy(c => c.RuleId).Where(g => g.Count() > 1 && RuleBook.Get(g.Key) is not null).Take(6))
        {
            var html = await Html("/Rules/Detail?id=" + group.Key);
            var section = html[html.IndexOf("id=\"history-title\"", StringComparison.Ordinal)..];
            var positions = group.Reverse().Select(c => section.IndexOf(System.Net.WebUtility.HtmlEncode(MatDesign.Web.Pages.Rules.ChangesModel.NoteOf(c).Replace("`", string.Empty)), StringComparison.Ordinal)).ToList();
            Assert.All(positions, p => Assert.True(p >= 0, $"{group.Key}: a note is not on the page"));
            Assert.Equal(positions.Order().ToList(), positions);
        }
    }

    [Fact]
    public async Task Every_rule_page_has_a_history_block_with_the_lines_of_its_rule()
    {
        // every rule has at least its Added line; the page of every rule shows the block (the smoke test of the whole book answers 200 too)
        var problems = new List<string>();
        foreach (var rule in RuleBook.All)
        {
            var html = await Html("/Rules/Detail?id=" + rule.Id);
            if (!html.Contains("id=\"history-title\"", StringComparison.Ordinal))
            {
                problems.Add($"{rule.Id}: no history block");
                continue;
            }

            var section = html[html.IndexOf("id=\"history-title\"", StringComparison.Ordinal)..];
            if (!section.Contains(RuleChanges.For(rule.Id)[0].DateText, StringComparison.Ordinal))
            {
                problems.Add($"{rule.Id}: its Added date is not in its history");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ------------------------------------------------------------------------------------------------------ the API

    [Fact]
    public async Task The_api_gives_every_line_the_newest_first()
    {
        var lines = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes"))!;

        Assert.Equal(RuleChanges.All.Count, lines.Length);
        var first = lines[0];
        Assert.Equal(RuleChanges.All.Max(c => c.DateText), first.GetProperty("date").GetString());
        foreach (var field in new[] { "date", "rule", "area", "title", "kind", "note", "hash" })
        {
            Assert.True(first.TryGetProperty(field, out _), field);
        }

        var dates = lines.Select(l => l.GetProperty("date").GetString()!).ToList();
        Assert.Equal(dates.OrderByDescending(d => d, StringComparer.Ordinal), dates);
        Assert.All(lines, l => Assert.Matches("^[0-9a-f]{8}$", l.GetProperty("hash").GetString()!));
        var added = lines.First(l => l.GetProperty("rule").GetString() == "SHELL-01" && l.GetProperty("kind").GetString() == "Added");
        Assert.Equal("Added", added.GetProperty("kind").GetString());
        Assert.Equal("SHELL", added.GetProperty("area").GetString());
        Assert.Equal(RuleBook.Get("SHELL-01")!.Title, added.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_api_and_the_page_of_a_rule_list_its_lines_in_the_same_order()
    {
        foreach (var group in RuleChanges.All.GroupBy(c => c.RuleId).Where(g => g.Count() > 1 && RuleBook.Get(g.Key) is not null).Take(8))
        {
            var fromApi = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes?rule=" + group.Key))!.Select(l => l.GetProperty("kind").GetString()!).ToList();
            var page = await Html("/Rules/Detail?id=" + group.Key);
            var fromPage = KindPill().Matches(page[page.IndexOf("id=\"history-title\"", StringComparison.Ordinal)..]).Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(fromApi, fromPage);
            Assert.Equal(group.Reverse().Select(c => c.Kind.ToString()), fromApi);
        }
    }

    [Fact]
    public async Task The_api_can_be_narrowed_by_rule_kind_area_and_day()
    {
        var one = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes?rule=form-15"))!;
        Assert.Equal(RuleChanges.For("FORM-15").Count, one.Length);
        Assert.All(one, l => Assert.Equal("FORM-15", l.GetProperty("rule").GetString()));

        var changed = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes?kind=changed"))!;
        Assert.Equal(RuleChanges.All.Count(c => c.Kind == RuleChangeKind.Changed), changed.Length);

        var area = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes?area=icon"))!;
        Assert.Equal(RuleChanges.All.Count(c => c.RuleId.StartsWith("ICON-")), area.Length);

        var since = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes?since=2026-10-15"))!;
        Assert.Equal(RuleChanges.All.Count(c => c.Date >= new DateOnly(2026, 10, 15)), since.Length);

        Assert.Empty(JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules/changes?rule=NOPE-99"))!);
    }

    [Theory]
    [InlineData("kind=Renamed")]
    [InlineData("since=yesterday")]
    [InlineData("since=2026-13-40")]
    [InlineData("area=NOPE")]
    public async Task A_value_the_api_does_not_understand_is_a_400_with_the_reason_and_never_an_empty_list(string query)
    {
        var html = await Html("/api/rules/changes?" + query, HttpStatusCode.BadRequest);

        Assert.Contains("\"error\"", html);
    }

    [Fact]
    public async Task The_rules_api_is_as_it_was()
    {
        var rules = JsonSerializer.Deserialize<JsonElement[]>(await Html("/api/rules"))!;

        Assert.Equal(RuleBook.All.Count, rules.Length);
        Assert.False(rules[0].TryGetProperty("changes", out _));
    }
}
