using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>What the Studio refuses to save as a rule.</summary>
public class RuleValidatorTests
{
    private static Rule Good() => new("FORM-90", "FORM", "A rule for the tests", "Every test rule is deleted again when the test ends.", RuleLevel.Should, RuleBook.Studio);

    private static IReadOnlyList<Problem> Check(Rule rule, IEnumerable<Rule>? others = null)
        => RuleValidator.Validate(RuleValidator.Normalize(rule), others ?? RuleBook.All);

    [Fact]
    public void A_good_rule_has_no_problem()
        => Assert.Empty(Check(Good()));

    [Fact]
    public void Normalizing_trims_puts_the_id_in_capitals_and_leaves_the_check_to_a_reviewer()
    {
        var rule = RuleValidator.Normalize(Good() with { Id = " form-90 ", Area = "form", Title = "  A rule ", Why = " ", Do = "x", Check = "" });

        Assert.Equal("FORM-90", rule.Id);
        Assert.Equal("FORM", rule.Area);
        Assert.Equal("A rule", rule.Title);
        Assert.Null(rule.Why);
        Assert.Equal("x", rule.Do);
        Assert.Equal("Review", rule.Check);
    }

    [Theory]
    [InlineData("FORM")]
    [InlineData("FORM-1")]
    [InlineData("FORM-1000")]
    [InlineData("FORM90")]
    [InlineData("F-90")]
    [InlineData("FORM-9a")]
    [InlineData("")]
    public void An_id_is_the_area_a_dash_and_two_or_three_digits(string id)
        => Assert.Contains(Check(Good() with { Id = id }), p => p.Field == "Id");

    [Fact]
    public void The_id_starts_with_the_area_and_nobody_else_has_it()
    {
        Assert.Contains(Check(Good() with { Id = "LIST-90" }), p => p is { Field: "Id" } && p.Message.Contains("starts with FORM-", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Id = "SHELL-01", Area = "SHELL" }), p => p is { Field: "Id", Message: "A rule with this id exists." });
        Assert.Contains(Check(Good() with { Area = "NOPE" }), p => p.Field == "Area");
    }

    [Fact]
    public void A_rule_may_keep_its_own_id_when_the_others_are_the_rest_of_the_book()
    {
        var shell = RuleBook.Get("SHELL-01")!;

        Assert.DoesNotContain(Check(shell, RuleBook.All.Where(r => r.Id != shell.Id)), p => p.Field == "Id");
    }

    [Fact]
    public void The_title_the_statement_the_level_the_source_and_the_texts_must_be_sensible()
    {
        Assert.Contains(Check(Good() with { Title = "ab" }), p => p.Field == "Title");
        Assert.Contains(Check(Good() with { Title = new string('x', 81) }), p => p.Field == "Title");
        Assert.Contains(Check(Good() with { Statement = "short" }), p => p.Field == "Statement");
        Assert.Contains(Check(Good() with { Statement = new string('x', 1201) }), p => p.Field == "Statement");
        Assert.Contains(Check(Good() with { Level = (RuleLevel)(-1) }), p => p.Field == "Level");
        Assert.Contains(Check(Good() with { Source = "" }), p => p.Field == "Source");
        Assert.Contains(Check(Good() with { Why = new string('x', 401) }), p => p.Field == "Why");
        Assert.Contains(Check(Good() with { Do = new string('x', 401) }), p => p.Field == "Do");
        Assert.Contains(Check(Good() with { Dont = new string('x', 401) }), p => p.Field == "Dont");
    }

    [Fact]
    public void Every_rule_that_is_built_in_passes()
    {
        foreach (var rule in RuleBook.All)
        {
            var problems = RuleValidator.Validate(rule, RuleBook.All.Where(r => r.Id != rule.Id));
            Assert.True(problems.Count == 0, $"{rule.Id}: {string.Join("; ", problems.Select(p => p.Message))}");
            Assert.True(RuleValidator.IsSafe(rule), rule.Id);
        }
    }

    [Fact]
    public void The_next_id_of_an_area_follows_the_highest_one()
    {
        var highest = RuleBook.In("IDENT").Max(r => int.Parse(r.Id[6..]));
        Assert.Equal($"IDENT-{highest + 1:00}", RuleValidator.NextId("IDENT", RuleBook.All));
        Assert.Equal("FORM-01", RuleValidator.NextId("FORM", []));
        Assert.Equal("FORM-91", RuleValidator.NextId("FORM", [.. RuleBook.All, Good()]));
        Assert.Equal("CODE-100", RuleValidator.NextId("CODE", [Good() with { Id = "CODE-99", Area = "CODE" }]));
    }

    [Fact]
    public void A_rule_read_from_the_disk_is_safe_only_when_it_is_a_rule_of_the_book()
    {
        Assert.True(RuleValidator.IsSafe(Good()));
        Assert.False(RuleValidator.IsSafe(Good() with { Area = "NOPE", Id = "NOPE-01" }));
        Assert.False(RuleValidator.IsSafe(Good() with { Id = "LIST-90" }));
        Assert.False(RuleValidator.IsSafe(Good() with { Level = (RuleLevel)7 }));
        Assert.False(RuleValidator.IsSafe(Good() with { Title = " " }));
        Assert.False(RuleValidator.IsSafe(Good() with { Statement = "" }));
        Assert.False(RuleValidator.IsSafe(Good() with { Id = "../x" }));
    }

    [Fact]
    public void The_level_is_stored_by_name()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Good(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"level\":\"Should\"", json);
        Assert.Equal(RuleLevel.Must, System.Text.Json.JsonSerializer.Deserialize<Rule>(json.Replace("\"Should\"", "\"Must\""), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.Level);
    }
}
