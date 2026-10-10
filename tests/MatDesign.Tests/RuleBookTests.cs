using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Tests;

public partial class RuleBookTests
{
    [GeneratedRegex(@"\b([A-Z][A-Z0-9]{2,5}-\d{2})\b")]
    private static partial Regex RuleIdRegex();

    [Fact]
    public void Ids_are_unique_and_have_the_shape_AREA_NN()
    {
        Assert.Equal(RuleBook.All.Count, RuleBook.All.Select(r => r.Id).Distinct().Count());
        Assert.All(RuleBook.All, r => Assert.Matches(@"^[A-Z][A-Z0-9]{2,5}-\d{2}$", r.Id));
        Assert.All(RuleBook.All, r => Assert.StartsWith(r.Area + "-", r.Id));
    }

    [Fact]
    public void Every_rule_is_in_a_known_area_and_every_area_has_rules()
    {
        var areas = RuleBook.Areas.Select(a => a.Id).ToHashSet();
        Assert.Equal(RuleBook.Areas.Count, areas.Count);
        Assert.All(RuleBook.All, r => Assert.Contains(r.Area, areas));
        Assert.All(RuleBook.Areas, a => Assert.NotEmpty(RuleBook.In(a.Id)));
    }

    [Fact]
    public void Every_area_icon_is_in_the_sprite()
        => Assert.All(RuleBook.Areas, a => Assert.True(RegistryTests.Sprite.Contains(a.Icon), $"{a.Id}: icon '{a.Icon}'"));

    [Fact]
    public void A_rule_says_something_and_names_its_source()
    {
        Assert.All(RuleBook.All, r => Assert.True(r.Title.Length > 3 && r.Statement.Length > 20, r.Id));
        Assert.All(RuleBook.All, r => Assert.False(string.IsNullOrWhiteSpace(r.Source), r.Id));
        Assert.All(RuleBook.All, r => Assert.False(string.IsNullOrWhiteSpace(r.Check), r.Id));
    }

    [Fact]
    public void A_rule_that_cites_another_rule_cites_one_that_exists()
    {
        foreach (var rule in RuleBook.All)
        {
            foreach (var text in new[] { rule.Statement, rule.Why, rule.Do, rule.Dont })
            {
                foreach (Match cited in RuleIdRegex().Matches(text ?? string.Empty))
                {
                    Assert.True(RuleBook.Get(cited.Value) is not null, $"{rule.Id} cites {cited.Value}, which does not exist");
                }
            }
        }
    }

    [Fact]
    public void The_rules_the_owner_stated_are_never_merely_optional()
    {
        // What the owner said in their own words is a must, or a should where they said "where it helps"; never a may.
        var owner = RuleBook.All.Where(r => r.Source == "Owner").ToList();
        Assert.True(owner.Count >= 15, $"only {owner.Count} rules are marked as the owner's");
        Assert.All(owner, r => Assert.True(r.Level != RuleLevel.May, $"{r.Id} is from the owner but only '{r.Level}'"));
    }

    [Theory]
    [InlineData("SHELL-01")]
    [InlineData("SHELL-03")]
    [InlineData("SHELL-05")]
    [InlineData("SHELL-07")]
    [InlineData("SHELL-08")]
    [InlineData("LIST-02")]
    [InlineData("LIST-03")]
    [InlineData("FORM-01")]
    [InlineData("DIALOG-01")]
    [InlineData("DIALOG-03")]
    public void The_rules_the_owner_named_first_exist_and_are_musts(string id)
    {
        var rule = RuleBook.Get(id);
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Must, rule.Level);
    }

    [Fact]
    public void The_lookup_ignores_case()
        => Assert.Same(RuleBook.Get("shell-01"), RuleBook.Get("SHELL-01"));

    [Fact]
    public void The_menu_stays_on_the_left_the_theme_button_has_three_states_and_dialogs_fill_the_phone()
    {
        // The owner's words, as the rules state them: if a rewrite drops one of these ideas the test says so.
        Assert.Contains("left", RuleBook.Get("SHELL-01")!.Statement);
        Assert.Contains("logo", RuleBook.Get("SHELL-01")!.Statement);
        Assert.Contains("System, Light, Dark", RuleBook.Get("SHELL-04")!.Statement);
        Assert.Contains("breadcrumb", RuleBook.Get("SHELL-08")!.Statement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("three buttons", RuleBook.Get("LIST-03")!.Title);
        Assert.Contains("full screen", RuleBook.Get("DIALOG-03")!.Title);
        Assert.Contains("under the content", RuleBook.Get("FORM-01")!.Title);
    }
}
