using System.Net;
using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The history of an item: its revisions, what changed between two, and a restore that goes through only when the old revision would be accepted today.</summary>
public sealed class StudioHistoryTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioHistoryTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private const string History = "/Studio/History?kind=apps&id=matx";

    private async Task SaveApp(Dictionary<string, string> form)
        => Assert.Equal(HttpStatusCode.Redirect, (await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", form)).StatusCode);

    private static Dictionary<string, string> Edit(string tagline, string accent = "#0f766e", string port = "")
    {
        var form = StudioHarness.AppForm(original: "matx");
        form["Input.Tagline"] = tagline;
        form["Input.Accent"] = accent;
        form["Input.Port"] = port;
        return form;
    }

    /// <summary>An app with three revisions: created, the tagline changed, the tagline and the accent changed.</summary>
    private async Task ThreeRevisions()
    {
        await _studio.SignIn();
        await SaveApp(StudioHarness.AppForm());
        await SaveApp(Edit("A second tagline"));
        await SaveApp(Edit("A third tagline", accent: "#7c3aed"));
    }

    private Task<HttpResponseMessage> Restore(string kind, string id, int revision)
        => _studio.Post("/Studio/Apps/Edit", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = kind, ["id"] = id, ["revision"] = revision.ToString() });

    [Fact]
    public async Task A_visitor_is_led_to_the_sign_in()
        => Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync(History)).StatusCode);

    [Fact]
    public async Task There_is_no_history_for_an_unknown_kind_an_id_that_is_a_path_or_an_item_that_was_never_saved()
    {
        await _studio.SignIn();

        await _studio.Get("/Studio/History?kind=nope&id=matx", HttpStatusCode.NotFound);
        await _studio.Get("/Studio/History?kind=apps&id=..%2Fx", HttpStatusCode.NotFound);
        await _studio.Get("/Studio/History?kind=apps&id=matmail", HttpStatusCode.NotFound);
        await _studio.Get("/Studio/History", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_history_lists_every_revision_marks_the_current_one_and_shows_what_changed_in_the_last()
    {
        await ThreeRevisions();

        var html = await _studio.Get(History);

        Assert.Contains("History of MatX", html);
        foreach (var number in new[] { 1, 2, 3 })
        {
            Assert.Contains($"<span class=\"doc-rule-id\">{number}</span>", html);
        }

        Assert.Equal(1, html.Split("mat-pill--ok mat-pill--plain\">Current").Length - 1);
        Assert.Contains("Created", html);
        Assert.Contains("Changed tagline", html);
        Assert.Contains("Changed tagline, accent", html);
        Assert.Contains("From revision 2 to revision 3.", html);
        Assert.Contains("A second tagline", html);
        Assert.Contains("A third tagline", html);
        Assert.Contains("doc-diff__line--del", html);
        Assert.Contains("doc-diff__line--add", html);
        Assert.Contains("<code>accent</code>", html);
        Assert.Contains("#7c3aed", html);
        Assert.DoesNotContain("<code>name</code>", html);
        Assert.Contains("id=\"compare-from\"", html);
        Assert.Contains("data-confirm=\"Restore revision 1 of MatX?", html);
        Assert.DoesNotContain("data-confirm=\"Restore revision 3 of MatX?", html);
    }

    [Fact]
    public async Task Any_two_revisions_can_be_compared_and_the_first_one_is_all_new()
    {
        await ThreeRevisions();

        var wide = await _studio.Get(History + "&a=1&b=3");
        Assert.Contains("From revision 1 to revision 3.", wide);
        Assert.Contains("<code>tagline</code>", wide);
        Assert.Contains("<code>accent</code>", wide);

        var first = await _studio.Get(History + "&b=1");
        Assert.Contains("Revision 1 is the first: everything in it is new.", first);
        Assert.Contains("<code>name</code>", first);
        Assert.DoesNotContain("doc-diff__line--del", first);

        // an older side that is not older than the newer one is replaced by the one before it
        Assert.Contains("From revision 1 to revision 2.", await _studio.Get(History + "&a=5&b=2"));
    }

    [Fact]
    public async Task Restoring_a_revision_appends_a_copy_of_it_and_brings_the_old_values_back()
    {
        await ThreeRevisions();

        var response = await Restore("apps", "matx", 1);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Studio/History", response.Headers.Location!.OriginalString);
        Assert.Equal("An app for the tests", _studio.Design.App("matx")!.Tagline);
        Assert.Equal("#0f766e", _studio.Design.App("matx")!.Accent);
        var revisions = _studio.Store.History("apps", "matx");
        Assert.Equal(4, revisions.Count);
        Assert.Equal("Restored revision 1", revisions[0].Note);
        Assert.Equal(["Restored revision 1", "Changed tagline, accent", "Changed tagline", "Created"], revisions.Select(r => r.Note));

        var page = await _studio.Get(History);
        Assert.Contains("data-toast=\"Revision 1 of MatX was restored as revision 4.\"", page);
        Assert.Contains("Restored", await _studio.Get("/Studio/Activity"));
        Assert.Contains("#0f766e", await _studio.Get("/ui/css/matui.apps.css"));
    }

    [Fact]
    public async Task A_revision_that_would_not_be_accepted_today_is_refused_with_the_reason_and_nothing_is_written()
    {
        await _studio.SignIn();
        var first = StudioHarness.AppForm();
        first["Input.Port"] = "18080";
        await SaveApp(first);
        await SaveApp(Edit("Second", port: "18081"));
        // another app takes the port that revision 1 had
        var other = StudioHarness.AppForm(id: "maty");
        other["Input.Name"] = "MatY";
        other["Input.Port"] = "18080";
        other["Input.Accent"] = "#7c3aed";
        await SaveApp(other);

        var response = await Restore("apps", "matx", 1);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Revision 1 cannot be restored as it is today.", html);
        Assert.Contains("MatY already uses port 18080.", html);
        Assert.Equal(2, _studio.Store.History("apps", "matx").Count);
        Assert.Equal(18081, _studio.Design.App("matx")!.Port);
    }

    [Fact]
    public async Task A_deleted_item_comes_back_with_a_restore_and_a_deletion_can_be_restored_while_the_item_lives()
    {
        await ThreeRevisions();
        var deleted = await _studio.Post("/Studio/Apps/Edit?id=matx", "/Studio/Apps/Edit?handler=Delete", StudioHarness.AppForm(original: "matx"));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Null(_studio.Design.App("matx"));
        Assert.Contains("Deleted", await _studio.Get(History));

        Assert.Equal(HttpStatusCode.Redirect, (await Restore("apps", "matx", 3)).StatusCode);
        Assert.Equal("A third tagline", _studio.Design.App("matx")!.Tagline);
        Assert.Equal(ItemSource.Added, _studio.Design.SourceOf("matx"));

        // the deletion (revision 4) can be applied again now that the item lives; while it is deleted it cannot
        Assert.Equal(HttpStatusCode.Redirect, (await Restore("apps", "matx", 4)).StatusCode);
        Assert.Null(_studio.Design.App("matx"));
        var again = await Restore("apps", "matx", 4);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("The item is deleted already.", await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_restore_needs_the_token_and_a_revision_that_exists()
    {
        await ThreeRevisions();

        var noToken = await _studio.Client.PostAsync("/Studio/History?handler=Restore", new FormUrlEncodedContent(new Dictionary<string, string> { ["kind"] = "apps", ["id"] = "matx", ["revision"] = "1" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Restore("apps", "matx", 99)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Restore("apps", "nobody", 1)).StatusCode);
        Assert.Equal(3, _studio.Store.History("apps", "matx").Count);
    }

    [Fact]
    public async Task Rules_and_components_have_a_history_too()
    {
        await _studio.SignIn();
        var rule = new Rule("FORM-90", "FORM", "A rule for the tests", "Every test rule is deleted again when the test ends.", RuleLevel.Should, RuleBook.Studio);
        _studio.Store.Save("rules", rule.Id, rule, "admin", "Created");
        _studio.Store.Save("rules", rule.Id, rule with { Title = "A changed title" }, "admin", "Changed title");
        _studio.Design.Reload();

        var page = await _studio.Get("/Studio/History?kind=rules&id=FORM-90");
        Assert.Contains("History of A changed title", page);
        Assert.Contains("<code>title</code>", page);
        Assert.Equal(HttpStatusCode.Redirect, (await Restore("rules", "FORM-90", 1)).StatusCode);
        Assert.Equal("A rule for the tests", _studio.Design.Rule("FORM-90")!.Title);

        var docs = new MatDesign.Web.Services.ComponentDoc("qa-hist", "QA history", "Content", "toggle", "A component for the history tests.", ["BUTTON-01"],
            [new MatDesign.Web.Services.ComponentExample("Pills", """<span class="mat-pill">x</span>""")], []);
        _studio.Store.Save("components", docs.Id, docs, "admin", "Created");
        _studio.Store.Save("components", docs.Id, docs with { Summary = "A changed summary for the history tests." }, "admin", "Changed summary");
        _studio.Design.Reload();
        Assert.Contains("<code>summary</code>", await _studio.Get("/Studio/History?kind=components&id=qa-hist"));
        Assert.Equal(HttpStatusCode.Redirect, (await Restore("components", "qa-hist", 1)).StatusCode);
        Assert.Equal("A component for the history tests.", _studio.Design.Component("qa-hist")!.Summary);
    }

    [Fact]
    public async Task The_edit_pages_link_to_the_history_when_there_is_one_and_the_activity_links_every_item()
    {
        await _studio.SignIn();
        Assert.DoesNotContain("/Studio/History", await _studio.Get("/Studio/Apps/Edit?id=matmail"));
        Assert.DoesNotContain("/Studio/History", await _studio.Get("/Studio/Apps/Edit"));

        await SaveApp(StudioHarness.AppForm());

        Assert.Contains("href=\"/Studio/History?kind=apps&amp;id=matx\"", await _studio.Get("/Studio/Apps/Edit?id=matx"));
        var activity = await _studio.Get("/Studio/Activity");
        Assert.Contains("href=\"/Studio/History?kind=apps&amp;id=matx\"", activity);
        Assert.Contains("Back", await _studio.Get(History));
    }

    [Fact]
    public async Task The_activity_can_be_filtered_by_the_person()
    {
        await _studio.SignIn();
        await SaveApp(StudioHarness.AppForm());
        _studio.Store.Save("rules", "FORM-91", new Rule("FORM-91", "FORM", "Hand written", "A rule that somebody else wrote by hand.", RuleLevel.Must, "hand"), "somebody", "by hand");

        var admin = await _studio.Get("/Studio/Activity?by=admin");
        Assert.Contains("matx", admin);
        Assert.DoesNotContain("FORM-91", admin);
        var other = await _studio.Get("/Studio/Activity?by=somebody");
        Assert.Contains("FORM-91", other);
        Assert.DoesNotContain("matx", other);
        Assert.Contains("id=\"activity-filter\"", await _studio.Get("/Studio/Activity"));
    }
}
