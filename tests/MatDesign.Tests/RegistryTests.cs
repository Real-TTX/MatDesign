using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Tests;

public partial class RegistryTests
{
    [GeneratedRegex(@"<symbol id=""mat-i-([a-z0-9-]+)""")]
    private static partial Regex SymbolRegex();

    internal static IReadOnlySet<string> Sprite { get; } =
        SymbolRegex().Matches(TestPaths.ReadToolkit("img/matui-icons.svg")).Select(m => m.Groups[1].Value).ToHashSet();

    [Fact]
    public void Ids_and_names_are_unique_and_written_the_same_way()
    {
        Assert.Equal(AppRegistry.All.Count, AppRegistry.All.Select(a => a.Id).Distinct().Count());
        Assert.Equal(AppRegistry.All.Count, AppRegistry.All.Select(a => a.Name.ToLowerInvariant()).Distinct().Count());
        Assert.All(AppRegistry.All, a => Assert.Matches("^[a-z]+$", a.Id));
        Assert.All(AppRegistry.All, a => Assert.Matches("^#[0-9a-f]{6}$", a.Accent));
    }

    [Fact]
    public void Every_category_is_known()
        => Assert.All(AppRegistry.All, a => Assert.Contains(a.Category, AppRegistry.Categories));

    [Fact]
    public void Every_app_reaches_AA_in_light_and_dark()
    {
        // IDENT-02: the text on the accent, the links and the accent in dark mode are readable, for every app.
        foreach (var app in AppRegistry.All)
        {
            var failing = AccentReport.For(app).Checks().Where(c => !c.Advisory && !c.Passes).Select(c => $"{c.Name} {c.Ratio:0.00} < {c.Required}").ToList();
            Assert.True(failing.Count == 0, $"{app.Name} ({app.Accent}): {string.Join("; ", failing)}");
        }
    }

    [Fact]
    public void Every_logo_glyph_is_in_the_sprite()
        => Assert.All(AppRegistry.All, a => Assert.True(Sprite.Contains(a.Glyph), $"{a.Name}: the glyph '{a.Glyph}' is not in matui-icons.svg"));

    [Fact]
    public void Ports_do_not_collide()
    {
        var clashes = AppRegistry.All.Where(a => a.Port is not null).GroupBy(a => a.Port).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(a => a.Name))}").ToList();
        Assert.True(clashes.Count == 0, "Apps share a port: " + string.Join("; ", clashes));
    }

    [Fact]
    public void The_advisor_flags_two_apps_with_the_same_colour()
    {
        var twin = AppRegistry.Get("matmail")! with { Id = "twin", Name = "Twin" };
        var collisions = PaletteAdvisor.Collisions([AppRegistry.Get("matmail")!, twin, AppRegistry.Get("matsplit")!]);
        var pair = Assert.Single(collisions);
        Assert.Equal(0, pair.Distance, 6);
    }

    [Fact]
    public void The_advisor_lists_the_closest_pairs_first()
    {
        var distances = PaletteAdvisor.Collisions(AppRegistry.All).Select(c => c.Distance).ToList();
        Assert.Equal(distances.Order(), distances);
        Assert.All(distances, d => Assert.True(d < PaletteAdvisor.TooClose));
    }

    [Fact]
    public void The_proposals_of_the_advisor_remove_every_collision_and_stay_readable()
    {
        var proposals = PaletteAdvisor.Suggest(AppRegistry.All);
        var changed = AppRegistry.All.Select(a => proposals.FirstOrDefault(p => p.App.Id == a.Id) is { } p ? a with { Accent = p.Accent } : a).ToList();

        Assert.Empty(PaletteAdvisor.Collisions(changed));
        Assert.All(proposals, p => Assert.True(Colors.Contrast(Rgb.White, Rgb.Parse(p.Accent)) >= 4.5, $"{p.App.Name}: {p.Accent} is too light for white text"));
        Assert.All(proposals, p => Assert.True(p.NearestDistance >= PaletteAdvisor.TooClose));
    }

    [Fact]
    public void The_later_app_of_every_close_pair_moves_and_the_first_of_all_keeps_its_colour()
    {
        var collisions = PaletteAdvisor.Collisions(AppRegistry.All);
        var proposed = PaletteAdvisor.Suggest(AppRegistry.All).Select(p => p.App.Id).ToHashSet();

        Assert.All(collisions, c => Assert.Contains(c.B.Id, proposed));
        var first = AppRegistry.All.First(a => collisions.Any(c => c.A == a || c.B == a));
        Assert.DoesNotContain(first.Id, proposed);
    }

    [Fact]
    public void A_dark_accent_is_lightened_and_gets_dark_text()
    {
        var report = AccentReport.For(AppRegistry.Get("matsplit")!);
        Assert.True(Colors.ToOklab(report.DarkAccent).L > Colors.ToOklab(report.Accent).L);
        Assert.Equal(AccentReport.Ink.ToHex(), report.DarkOnAccent.ToHex());
    }

    [Fact]
    public void The_version_has_one_of_the_three_formats()
        => Assert.Matches(@"^(\d+\.\d+\.\d+-\d{8}|nightly-\d+-\d{8}|local-\d{8})$", AppVersion.Current);
}
