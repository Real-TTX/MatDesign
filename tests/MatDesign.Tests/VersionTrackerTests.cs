using System.Net;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>Which release of MatUi each app uses, written down by hand, and where that stands against the releases.</summary>
public sealed class VersionTrackerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public VersionTrackerTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private VersionTracker Tracker => _studio.Factory.Services.GetRequiredService<VersionTracker>();

    private void Release(params string[] versions)
    {
        foreach (var version in versions)
        {
            Assert.True(_studio.Releases.Execute(version, string.Empty, "admin").Succeeded, version);
        }
    }

    private Task<HttpResponseMessage> Save(string app, string version, string note = "", string handler = "")
        => _studio.Post($"/Studio/Versions/Edit?id={app}", "/Studio/Versions/Edit" + handler, new Dictionary<string, string> { ["Input.AppId"] = app, ["Input.Version"] = version, ["Input.Note"] = note });

    [Fact]
    public async Task A_visitor_is_led_to_the_sign_in()
    {
        foreach (var url in new[] { "/Studio/Versions", "/Studio/Versions/Edit?id=matmail" })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync(url)).StatusCode);
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("0.1.0", true)]
    [InlineData(" 1.20.3 ", true)]
    [InlineData("1.0", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("1.0.0.0", false)]
    [InlineData("latest", false)]
    public void A_version_is_three_numbers_or_empty(string? version, bool fine)
        => Assert.Equal(fine, VersionTracker.Check(version) is null);

    [Fact]
    public void Nothing_is_written_down_at_first_and_without_a_release_a_version_has_nothing_to_be_compared_with()
    {
        Assert.All(Tracker.All(), row => Assert.Equal(VersionState.Unknown, row.State));
        Assert.Equal(_studio.Design.Apps.Count, Tracker.All().Count);

        Tracker.Set("matmail", Ver.First, "a note", "admin");

        var row = Tracker.Of("matmail")!;
        Assert.Equal(VersionState.NoRelease, row.State);
        Assert.Equal(Ver.First, row.Version);
        Assert.Equal("a note", row.Note);
        Assert.NotNull(row.RecordedAt);
        Assert.Equal(0, Tracker.BehindCount);
        Assert.Null(Tracker.LatestVersion);
    }

    [Fact]
    public void An_app_is_current_behind_or_on_a_version_that_is_no_release()
    {
        Release(Ver.Next(0), Ver.Next(1), Ver.Next(2));

        Tracker.Set("matmail", Ver.Next(2), null, "admin");
        Tracker.Set("matdock", Ver.First, null, "admin");
        Tracker.Set("matcad", Ver.Next(1), null, "admin");
        Tracker.Set("matpaper", Ver.Below, null, "admin");
        Tracker.Set("matsplit", Ver.Far, null, "admin");

        Assert.Equal((VersionState.Current, 0), (Tracker.Of("matmail")!.State, Tracker.Of("matmail")!.Behind));
        Assert.Equal((VersionState.Behind, 2), (Tracker.Of("matdock")!.State, Tracker.Of("matdock")!.Behind));
        Assert.Equal((VersionState.Behind, 1), (Tracker.Of("matcad")!.State, Tracker.Of("matcad")!.Behind));
        Assert.Equal(VersionState.Foreign, Tracker.Of("matpaper")!.State);
        Assert.Equal(VersionState.Foreign, Tracker.Of("matsplit")!.State);
        Assert.Equal(VersionState.Unknown, Tracker.Of("matfile")!.State);
        Assert.Equal(2, Tracker.BehindCount);
        Assert.Equal(Ver.Next(2), Tracker.LatestVersion);
        Assert.Null(Tracker.Of("nobody"));
    }

    [Fact]
    public void A_new_release_makes_the_apps_that_were_current_fall_behind()
    {
        Release(Ver.First);
        Tracker.Set("matmail", Ver.First, null, "admin");
        Assert.Equal(VersionState.Current, Tracker.Of("matmail")!.State);

        Release(Ver.Next(1));

        Assert.Equal((VersionState.Behind, 1), (Tracker.Of("matmail")!.State, Tracker.Of("matmail")!.Behind));
    }

    [Fact]
    public void A_bad_version_an_unknown_app_and_forgetting_are_handled()
    {
        Assert.Throws<ArgumentException>(() => Tracker.Set("matmail", "1.0", null, "admin"));
        Assert.Throws<KeyNotFoundException>(() => Tracker.Set("nobody", Ver.First, null, "admin"));

        Tracker.Set("matmail", Ver.First, "x", "admin");
        Tracker.Set("matmail", "", "only a note", "admin");
        Assert.Equal((VersionState.Unknown, "only a note"), (Tracker.Of("matmail")!.State, Tracker.Of("matmail")!.Note));

        Tracker.Set("matmail", " ", " ", "admin");
        Assert.Null(Tracker.Of("matmail")!.Note);
        Assert.Null(Tracker.Of("matmail")!.RecordedAt);
        Tracker.Forget("matmail", "admin");
        Assert.Equal(["Forgotten", "Version forgotten, note kept", $"Uses MatUi {Ver.First}"], _studio.Store.History("usage", "matmail").Select(r => r.Note));
    }

    [Fact]
    public async Task The_list_shows_every_app_where_it_stands_and_can_be_narrowed()
    {
        await _studio.SignIn();
        Release(Ver.Next(0), Ver.Next(1));
        Tracker.Set("matmail", Ver.Next(1), null, "admin");
        Tracker.Set("matdock", Ver.First, "Branch main", "admin");

        var html = await _studio.Get("/Studio/Versions");
        Assert.Contains($"The latest release is {Ver.Next(1)}; 1 app is behind it.", html);
        Assert.Contains("1 release behind", html);
        Assert.Contains("Branch main", html);
        Assert.Contains("mat-pill--ok", html);
        Assert.Contains("Not written down", html);
        Assert.Contains("class=\"mat-toolbar\"", html);
        foreach (var dialog in new[] { "versions-search", "versions-filter", "versions-sort" })
        {
            Assert.Contains($"id=\"{dialog}\"", html);
        }

        var behind = await _studio.Get("/Studio/Versions?state=Behind");
        Assert.Contains("MatDock", behind);
        Assert.DoesNotContain("MatMail", behind);
        Assert.Contains("No app matches", await _studio.Get("/Studio/Versions?q=zzzz"));
        Assert.Contains("MatMail", await _studio.Get($"/Studio/Versions?q={Ver.Next(1)}"));
    }

    [Fact]
    public async Task Writing_down_a_version_takes_the_latest_over_and_forgets_it_again()
    {
        await _studio.SignIn();
        Release(Ver.First);

        var edit = await _studio.Get("/Studio/Versions/Edit?id=matmail");
        Assert.Contains("Version of MatMail", edit);
        Assert.Contains($"The latest release is {Ver.First}.", edit);
        Assert.Contains($"Use the latest ({Ver.First})", edit);
        Assert.DoesNotContain("Forget", edit);

        var saved = await Save("matmail", Ver.Below, "an old copy");
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/Studio/Versions", saved.Headers.Location!.OriginalString);
        Assert.Contains($"data-toast=\"MatMail uses {Ver.Below}.\"", await _studio.Get("/Studio/Versions"));
        Assert.Equal(VersionState.Foreign, Tracker.Of("matmail")!.State);
        Assert.Contains("Forget", await _studio.Get("/Studio/Versions/Edit?id=matmail"));

        // the latest, whatever is typed
        var latest = await Save("matmail", "nonsense", "now current", "?handler=Latest");
        Assert.Equal(HttpStatusCode.Redirect, latest.StatusCode);
        Assert.Equal((VersionState.Current, Ver.First, "now current"), (Tracker.Of("matmail")!.State, Tracker.Of("matmail")!.Version, Tracker.Of("matmail")!.Note));

        var forgotten = await Save("matmail", Ver.First, string.Empty, "?handler=Forget");
        Assert.Equal(HttpStatusCode.Redirect, forgotten.StatusCode);
        Assert.Equal(VersionState.Unknown, Tracker.Of("matmail")!.State);
        Assert.Contains("Version", await _studio.Get("/Studio/Activity"));
    }

    [Fact]
    public async Task A_bad_version_or_note_stays_on_the_page_with_the_reason_and_nothing_is_saved()
    {
        await _studio.SignIn();

        var response = await Save("matmail", "1.0", "x");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("A version is three numbers with dots", html);
        Assert.Contains("value=\"1.0\"", html);
        Assert.Empty(_studio.Store.List("usage"));

        Assert.Contains("Keep the note to 200 characters.", await (await Save("matmail", Ver.First, new string('x', 201))).Content.ReadAsStringAsync());
        Assert.Empty(_studio.Store.List("usage"));
        await _studio.Get("/Studio/Versions/Edit?id=nobody", HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Post("/Studio/Versions/Edit?id=matmail", "/Studio/Versions/Edit", new Dictionary<string, string> { ["Input.AppId"] = "nobody", ["Input.Version"] = Ver.First })).StatusCode);
    }

    [Fact]
    public async Task An_app_behind_is_advice_with_a_link_and_never_an_error_and_the_overview_counts_it()
    {
        await _studio.SignIn();
        Release(Ver.Next(0), Ver.Next(1));
        Assert.True(_studio.Checks.Report.Results.Single(r => r.Check.Id == "versions").Passed);
        Assert.Empty(_studio.Checks.Report.Results.Single(r => r.Check.Id == "versions").Findings);
        Assert.Contains("None", await _studio.Get("/Studio"));

        Tracker.Set("matdock", Ver.First, null, "admin");

        var found = Assert.Single(_studio.Checks.Report.Results.Single(r => r.Check.Id == "versions").Findings);
        Assert.Equal(Severity.Advice, found.Severity);
        Assert.Contains($"MatDock uses MatUi {Ver.First}, 1 release behind the latest ({Ver.Next(1)}).", found.Message);
        Assert.Equal("/Studio/Versions/Edit?id=matdock", _studio.Checks.UrlOf(found));
        Assert.True(_studio.Checks.Report.Passed);
        Assert.Contains("1 of 25", await _studio.Get("/Studio"));
    }
}
