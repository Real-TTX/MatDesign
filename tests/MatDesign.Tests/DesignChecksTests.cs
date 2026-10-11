using System.Net;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The checks of the design system: the built-in data passes them, and each fault that is put into the data is found, with the item it is about.</summary>
public sealed class DesignChecksTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public DesignChecksTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private static MatApp App(string id, string name, string accent = "#0f766e", int? port = null, string? onAccent = null)
        => new(id, name, "An app for the checks", AppRegistry.Productivity, accent, "apps", id, OnAccent: onAccent, Port: port);

    private static ComponentDoc Component(string id, string html, string live = "true", string rule = "BUTTON-01")
        => new(id, "Check " + id, "Content", "toggle", "A component for the checks, written by hand.", [rule], [new ComponentExample("Example", html, Live: live == "true")], []);

    private void Seed<T>(string kind, string id, T item)
    {
        _studio.Store.Save(kind, id, item, "test");
        _studio.Design.Reload();
    }

    private IEnumerable<Finding> Found(string check) => _studio.Checks.Report.Results.Single(r => r.Check.Id == check).Findings;

    [Fact]
    public void The_built_in_data_passes_every_check_and_the_palette_has_advice()
    {
        var report = _studio.Checks.Report;

        Assert.True(report.Passed, string.Join("\n", report.Findings.Where(f => f.Severity == Severity.Error).Select(f => f.Check + ": " + f.Message)));
        Assert.True(report.Advice > 0, "the built-in palette has pairs of accents that are close");
        Assert.All(Found("palette"), f => Assert.Equal(Severity.Advice, f.Severity));
        Assert.Equal(DesignChecks.All.Count, report.Results.Count);
    }

    [Fact]
    public void Every_check_has_an_id_an_area_a_name_and_says_what_it_holds_to()
    {
        Assert.Equal(DesignChecks.All.Count, DesignChecks.All.Select(c => c.Id).Distinct().Count());
        Assert.All(DesignChecks.All, c =>
        {
            Assert.Matches("^[a-z-]+$", c.Id);
            Assert.NotEmpty(c.Area);
            Assert.NotEmpty(c.Name);
            Assert.True(c.What.Length > 30, c.Id);
        });
    }

    [Fact]
    public void The_report_is_computed_once_for_a_version_of_the_data_and_again_when_it_changes()
    {
        var first = _studio.Checks.Report;
        Assert.Same(first, _studio.Checks.Report);

        Seed("apps", "qa", App("qa", "QA light", accent: "#ffe066"));

        var second = _studio.Checks.Report;
        Assert.NotSame(first, second);
        Assert.True(second.Version > first.Version);
        Assert.False(second.Passed);
    }

    [Fact]
    public void An_app_that_fails_what_a_save_holds_it_to_is_found_with_a_link_to_it()
    {
        Seed("apps", "qa", App("qa", "QA light", accent: "#ffe066"));
        Seed("apps", "qb", App("qb", "QB twin", port: AppRegistry.Get("matdock")!.Port));

        var findings = Found("apps").ToList();

        Assert.Contains(findings, f => f.Id == "qa" && f.Message.Contains("White text reaches only", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Id == "qb" && f.Message.Contains("MatDock already uses port", StringComparison.Ordinal));
        Assert.All(findings, f => Assert.Equal(Severity.Error, f.Severity));
        Assert.Equal("/Studio/Apps/Edit?id=qa", _studio.Checks.UrlOf(findings.First(f => f.Id == "qa")));
    }

    [Fact]
    public void An_app_with_the_colour_of_another_is_advice_and_not_an_error()
    {
        Seed("apps", "qa", App("qa", "QA twin", accent: AppRegistry.Get("matmail")!.Accent));

        Assert.Contains(Found("palette"), f => f.Id == "qa" || f.Message.Contains("QA twin", StringComparison.Ordinal));
        Assert.True(_studio.Checks.Report.Results.Single(r => r.Check.Id == "apps").Passed);
    }

    [Fact]
    public void A_rule_that_fails_what_a_save_holds_it_to_is_found()
    {
        Seed("rules", "FORM-90", new Rule("FORM-90", "FORM", "ab", "A statement that is long enough to be one.", RuleLevel.Should, RuleBook.Studio));

        Assert.Contains(Found("rules"), f => f.Id == "FORM-90" && f.Message.Contains("3 to 80 characters", StringComparison.Ordinal));
    }

    [Fact]
    public void A_component_that_breaks_the_contract_or_names_a_missing_rule_is_found()
    {
        Seed("components", "qa-class", Component("qa-class", """<span class="nopeclass">x</span>"""));
        Seed("components", "qa-rule", Component("qa-rule", """<span class="mat-pill">x</span>""", rule: "NOPE-99"));

        var findings = Found("components").ToList();

        Assert.Contains(findings, f => f.Id == "qa-class" && f.Message.Contains("no class 'nopeclass'", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Id == "qa-rule" && f.Message.Contains("no rule NOPE-99", StringComparison.Ordinal));
        Assert.Equal("/Studio/Components/Edit?id=qa-class", _studio.Checks.UrlOf(findings.First(f => f.Id == "qa-class")));
    }

    [Fact]
    public void An_id_that_two_live_examples_put_on_a_page_is_found_and_one_that_is_not_live_is_not()
    {
        string Input(string id) => $"""<input class="mat-input" id="{id}" name="a" />""";
        Seed("components", "qa-one", Component("qa-one", Input("dup-id")));
        Seed("components", "qa-two", Component("qa-two", Input("dup-id")));
        Seed("components", "qa-three", Component("qa-three", Input("quiet-id"), live: "false"));
        Seed("components", "qa-four", Component("qa-four", Input("quiet-id"), live: "false"));

        var findings = Found("page-ids").ToList();

        Assert.Contains(findings, f => f.Message.Contains("'dup-id' is used 2 times", StringComparison.Ordinal) && f.Message.Contains("Components", StringComparison.Ordinal));
        Assert.DoesNotContain(findings, f => f.Message.Contains("quiet-id", StringComparison.Ordinal));
    }

    [Fact]
    public void An_item_that_the_site_had_to_leave_out_is_reported_with_its_history()
    {
        _studio.Store.Save("components", "qa-evil", Component("qa-evil", "<script>alert(1)</script>"), "hand");
        _studio.Store.Save("apps", "qa-wrong", new { id = "other", name = "Other", tagline = "xyz", category = "Platform", accent = "#112233", glyph = "apps", repo = "x" }, "hand");
        _studio.Design.Reload();

        var findings = Found("volume").ToList();

        Assert.Contains(findings, f => f.Id == "qa-evil" && f.Message.Contains("breaks a check that keeps the page safe", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Id == "qa-wrong" && f.Message.Contains("not the name of its folder", StringComparison.Ordinal));
        Assert.Equal("/Studio/History?kind=components&id=qa-evil", _studio.Checks.UrlOf(findings.First(f => f.Id == "qa-evil")));
        Assert.Null(_studio.Checks.UrlOf(new Finding("x", Severity.Error, "about nothing")));
        Assert.Null(_studio.Checks.UrlOf(new Finding("x", Severity.Error, "gone", "apps", "never-saved")));
    }

    [Fact]
    public async Task The_page_is_for_the_administrator_and_lists_the_checks_and_the_findings()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/Checks")).StatusCode);
        await _studio.SignIn();

        var html = await _studio.Get("/Studio/Checks");
        Assert.Contains("All checks pass", html);
        foreach (var check in DesignChecks.All)
        {
            Assert.Contains(check.Name, html);
        }

        Assert.Contains("class=\"mat-toolbar\"", html);
        foreach (var dialog in new[] { "findings-search", "findings-filter", "findings-sort" })
        {
            Assert.Contains($"id=\"{dialog}\"", html);
        }

        Assert.Contains("mistaken for one another", await _studio.Get("/Studio/Checks?q=mistaken"));
        Assert.Contains("No finding matches", await _studio.Get("/Studio/Checks?q=zzzzz"));
        Assert.Contains("all checks pass", html);
        Assert.Contains("Checks", await _studio.Get("/Studio"));
    }

    [Fact]
    public async Task A_fault_shows_as_an_error_with_a_link_in_the_page_the_menu_and_the_overview()
    {
        await _studio.SignIn();
        Seed("apps", "qa", App("qa", "QA port", port: 4455));

        var html = await _studio.Get("/Studio/Checks");
        Assert.DoesNotContain("All checks pass", html);
        // a port that two apps want is a fault of both
        Assert.Contains("2 errors", html);
        Assert.Contains("href=\"/Studio/Apps/Edit?id=qa\"", html);
        Assert.Contains("checks find errors", html);

        var errors = await _studio.Get("/Studio/Checks?severity=Error");
        Assert.Contains("QA port", errors);
        Assert.DoesNotContain("mistaken for one another", errors);
        var advice = await _studio.Get("/Studio/Checks?severity=Advice");
        Assert.Contains("mistaken for one another", advice);
        Assert.DoesNotContain("QA port", advice);
        Assert.Contains("2 errors", await _studio.Get("/Studio"));
    }
}
