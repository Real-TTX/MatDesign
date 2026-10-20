using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The history block of the page of a rule when the Studio is on: a rule that was written or changed in the Studio keeps its revisions there, and the page links to them for a person who may open the
/// Studio. The changelog of the rulebook is the history of the rules that came with the program.
/// </summary>
public sealed class RuleChangesStudioTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public RuleChangesStudioTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private const string HistoryLink = "href=\"/Studio/History?kind=rules&amp;id=";

    private async Task<string> AsAnonymous(string url) => await (await _studio.NewClient().GetAsync(url)).Content.ReadAsStringAsync();

    [Fact]
    public async Task A_rule_that_came_with_the_program_has_its_lines_and_no_link_into_the_Studio_that_has_nothing_about_it()
    {
        await _studio.SignIn();

        var html = await _studio.Get("/Rules/Detail?id=SHELL-01");

        Assert.Contains("id=\"history-title\"", html);
        Assert.DoesNotContain(HistoryLink, html);
        Assert.DoesNotContain("History in the Studio", html);
        Assert.DoesNotContain("changed in the Studio", html);
    }

    [Fact]
    public async Task A_rule_written_in_the_Studio_says_so_has_no_line_of_the_changelog_and_links_to_its_revisions_for_a_person_who_may_open_the_Studio()
    {
        await _studio.SignIn();
        var rule = new Rule("FORM-90", "FORM", "A rule of the Studio", "A statement that the Studio saved, in more than ten characters.", RuleLevel.Should, RuleBook.Studio);
        _studio.Store.Save("rules", rule.Id, rule, "admin", "Created");
        _studio.Design.Reload();

        var signedIn = await _studio.Get("/Rules/Detail?id=FORM-90");

        Assert.Contains("This rule was written in the Studio", signedIn);
        Assert.Contains(HistoryLink + "FORM-90\"", signedIn);
        Assert.Contains("History in the Studio", signedIn);
        Assert.DoesNotContain("<caption class=\"mat-sr-only\">Changes of FORM-90", signedIn);
        Assert.DoesNotContain("The rule was added.", signedIn);

        // somebody who is not signed in sees the text and not the link (the Studio would send them to the sign-in)
        var visitor = await AsAnonymous("/Rules/Detail?id=FORM-90");
        Assert.Contains("This rule was written in the Studio", visitor);
        Assert.DoesNotContain(HistoryLink, visitor);
        Assert.DoesNotContain("History in the Studio", visitor);

        // a viewer may open the Studio and so sees the link
        _studio.AddUser("vera", "viewer");
        var viewer = await _studio.ClientOf("vera");
        Assert.Contains(HistoryLink + "FORM-90\"", await (await viewer.GetAsync("/Rules/Detail?id=FORM-90")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_rule_of_the_book_that_was_changed_in_the_Studio_keeps_its_lines_and_says_where_the_new_text_comes_from()
    {
        await _studio.SignIn();
        var original = RuleBook.Get("SHELL-01")!;
        _studio.Store.Save("rules", original.Id, original with { Statement = original.Statement + " A sentence from the Studio." }, "admin", "Changed statement");
        _studio.Design.Reload();

        var html = await _studio.Get("/Rules/Detail?id=SHELL-01");

        Assert.Contains("A sentence from the Studio.", html);
        Assert.Contains("The text above was changed in the Studio", html);
        Assert.Contains("<caption class=\"mat-sr-only\">Changes of SHELL-01", html);
        Assert.Contains("2026-10-10", html);
        Assert.Contains(HistoryLink + "SHELL-01\"", html);
        Assert.DoesNotContain(HistoryLink, await AsAnonymous("/Rules/Detail?id=SHELL-01"));
    }

    [Fact]
    public async Task The_page_of_the_changes_shows_a_rule_that_the_Studio_changed_with_its_title_as_it_is_now_and_leaves_the_rules_of_the_Studio_out()
    {
        await _studio.SignIn();
        var original = RuleBook.Get("SHELL-02")!;
        _studio.Store.Save("rules", original.Id, original with { Title = "Menu items, as the Studio words them" }, "admin", "Changed title");
        var added = new Rule("FORM-90", "FORM", "A rule of the Studio", "A statement that the Studio saved, in more than ten characters.", RuleLevel.Should, RuleBook.Studio);
        _studio.Store.Save("rules", added.Id, added, "admin", "Created");
        _studio.Design.Reload();

        var html = await _studio.Get("/Rules/Changes?q=SHELL-02");

        Assert.Contains("Menu items, as the Studio words them", html);
        Assert.DoesNotContain("FORM-90", await _studio.Get("/Rules/Changes"));
        Assert.Equal(RuleChanges.All.Count, JsonSerializerCount(await _studio.Get("/api/rules/changes")));
    }

    private static int JsonSerializerCount(string json) => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json)!.Length;
}
