using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>What the Studio refuses to save as an app: the facts that the tests hold for the built-in apps, checked on the one being typed.</summary>
public class AppValidatorTests
{
    private static readonly string[] Glyphs = ["apps", "palette", "mail"];

    private static MatApp Good() => new("matx", "MatX", "An app for the tests", AppRegistry.Productivity, "#0f766e", "apps", "MatX");

    private static IReadOnlyList<Problem> Check(MatApp app, IEnumerable<MatApp>? others = null)
        => AppValidator.Validate(AppValidator.Normalize(app), others ?? AppRegistry.All, Glyphs);

    [Fact]
    public void A_good_app_has_no_problem()
        => Assert.Empty(Check(Good()));

    [Fact]
    public void Normalizing_trims_lowercases_the_colours_and_names_the_repository_like_the_app()
    {
        var app = AppValidator.Normalize(Good() with { Id = " MatX ", Name = "  MatX ", Accent = " #0F766E ", OnAccent = " ", AccentText = "#ABCDEF", Repo = "", Port = null });

        Assert.Equal("matx", app.Id);
        Assert.Equal("MatX", app.Name);
        Assert.Equal("#0f766e", app.Accent);
        Assert.Null(app.OnAccent);
        Assert.Equal("#abcdef", app.AccentText);
        Assert.Equal("MatX", app.Repo);
    }

    [Fact]
    public void An_app_with_a_space_in_its_name_gets_a_repository_without_one()
    {
        var app = AppValidator.Normalize(Good() with { Name = "My Mat App", Repo = "" });

        Assert.Equal("My-Mat-App", app.Repo);
        Assert.Empty(AppValidator.Validate(app, AppRegistry.All, Glyphs));
    }

    [Theory]
    [InlineData("")]
    [InlineData("m")]
    [InlineData("Mat X")]
    [InlineData("mat-x")]
    [InlineData("matx2")]
    [InlineData("abcdefghijklmnopqrstuvwxyz")]
    public void An_id_is_two_to_24_lower_case_letters(string id)
        => Assert.Contains(Check(Good() with { Id = id }), p => p.Field == "Id");

    [Fact]
    public void An_id_or_a_name_that_another_app_has_is_refused_in_any_case()
    {
        Assert.Contains(Check(Good() with { Id = "matmail" }), p => p is { Field: "Id", Message: "An app with this id exists." });
        Assert.Contains(Check(Good() with { Name = "MATMAIL" }), p => p is { Field: "Name", Message: "Another app has this name." });
    }

    [Fact]
    public void An_app_may_keep_its_own_id_and_name_when_the_others_are_the_rest_of_the_family()
    {
        var matmail = AppRegistry.Get("matmail")!;
        var rest = AppRegistry.All.Where(a => a.Id != "matmail");

        Assert.DoesNotContain(Check(matmail, rest), p => p.Field is "Id" or "Name" or "Port");
    }

    [Fact]
    public void A_port_that_another_app_uses_is_refused_and_says_which()
    {
        var problems = Check(Good() with { Port = AppRegistry.Get("matdock")!.Port });

        Assert.Contains(problems, p => p.Field == "Port" && p.Message.Contains("MatDock", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Port = 0 }), p => p.Field == "Port");
        Assert.Contains(Check(Good() with { Port = 70000 }), p => p.Field == "Port");
        Assert.DoesNotContain(Check(Good() with { Port = 18080 }), p => p.Field == "Port");
    }

    [Fact]
    public void The_category_the_glyph_the_tagline_and_the_name_must_be_sensible()
    {
        Assert.Contains(Check(Good() with { Category = "Toys" }), p => p.Field == "Category");
        Assert.Contains(Check(Good() with { Glyph = "nope" }), p => p.Field == "Glyph");
        Assert.Contains(Check(Good() with { Tagline = "x" }), p => p.Field == "Tagline");
        Assert.Contains(Check(Good() with { Tagline = new string('x', 91) }), p => p.Field == "Tagline");
        Assert.Contains(Check(Good() with { Name = "M" }), p => p.Field == "Name");
        Assert.Contains(Check(Good() with { Notes = new string('x', 301) }), p => p.Field == "Notes");
        Assert.Contains(Check(Good() with { Repo = "a b" }), p => p.Field == "Repo");
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#12345g")]
    [InlineData("")]
    public void An_accent_that_is_not_a_colour_is_refused_before_the_contrast_is_asked(string accent)
    {
        var problems = Check(Good() with { Accent = accent });

        Assert.Equal(["Accent"], problems.Select(p => p.Field).Distinct());
    }

    [Fact]
    public void White_text_on_a_light_accent_is_refused_and_a_dark_text_colour_is_the_way_out()
    {
        var light = Good() with { Accent = "#ffe066" };

        var problems = Check(light);
        Assert.Contains(problems, p => p.Field == "OnAccent" && p.Message.Contains("White text", StringComparison.Ordinal) && p.Message.Contains("4.5:1", StringComparison.Ordinal));

        // with dark text the accent carries its text; links in it are still too light on white
        var withInk = Check(light with { OnAccent = AccentReport.Ink.ToHex() });
        Assert.DoesNotContain(withInk, p => p.Field == "OnAccent");
        Assert.Contains(withInk, p => p.Field == "AccentText");

        Assert.Empty(Check(light with { OnAccent = AccentReport.Ink.ToHex(), AccentText = "#7a5c00" }));
    }

    [Fact]
    public void Every_app_that_is_built_in_passes()
    {
        foreach (var app in AppRegistry.All)
        {
            var problems = AppValidator.Validate(app, AppRegistry.All.Where(a => a.Id != app.Id), [app.Glyph]);
            Assert.True(problems.Count == 0, $"{app.Name}: {string.Join("; ", problems.Select(p => p.Message))}");
        }
    }

    [Fact]
    public void An_app_read_from_the_disk_is_safe_only_when_it_can_be_written_into_a_stylesheet()
    {
        Assert.True(AppValidator.IsSafe(Good()));
        Assert.False(AppValidator.IsSafe(Good() with { Id = "a\"] { x" }));
        Assert.False(AppValidator.IsSafe(Good() with { Accent = "red; } body { display: none" }));
        Assert.False(AppValidator.IsSafe(Good() with { OnAccent = "#12" }));
        Assert.False(AppValidator.IsSafe(Good() with { AccentText = "url(x)" }));
        Assert.False(AppValidator.IsSafe(Good() with { Glyph = "a b" }));
        Assert.False(AppValidator.IsSafe(Good() with { Name = " " }));
    }
}
