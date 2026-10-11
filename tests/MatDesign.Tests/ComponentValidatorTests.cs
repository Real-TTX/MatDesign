using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Tests;

/// <summary>What the Studio refuses to save as a component of the documentation.</summary>
public class ComponentValidatorTests
{
    private static readonly MarkupFacts Facts = new(
        MarkupChecks.ClassesIn(new[] { "tokens", "base", "shell", "controls", "forms", "pickers", "lists", "dialogs", "layout", "apps" }.Select(p => TestPaths.ReadToolkit($"css/matui.{p}.css"))),
        RegistryTests.Sprite);

    private static ComponentDoc Good() => new(
        "qa-sample", "QA sample", "Content", "toggle", "A component for the tests, with one example.",
        ["BUTTON-01"], [new ComponentExample("Pills", """<span class="mat-pill mat-pill--ok">Online</span>""")], ["One line."]);

    private static IReadOnlyList<Problem> Check(ComponentDoc doc, IEnumerable<ComponentDoc>? others = null)
        => ComponentValidator.Validate(ComponentValidator.Normalize(doc), others ?? ComponentCatalog.All, rule => RuleBook.Get(rule) is not null, Facts);

    [Fact]
    public void A_good_component_has_no_problem()
        => Assert.Empty(Check(Good()));

    [Fact]
    public void Normalizing_trims_the_rules_the_lines_and_drops_the_empty_examples()
    {
        var doc = ComponentValidator.Normalize(Good() with
        {
            Id = " QA-Sample ",
            Name = " QA sample ",
            Icon = " ",
            Rules = [" button-01 ", "BUTTON-01", ""],
            Behaviour = ["  one ", " ", "two"],
            Examples = [.. Good().Examples, new ComponentExample("", ""), new ComponentExample(" Second ", " <b>x</b> ", " ", " ", " ")],
        });

        Assert.Equal("qa-sample", doc.Id);
        Assert.Equal("QA sample", doc.Name);
        Assert.Equal("toggle", doc.Icon);
        Assert.Equal(["BUTTON-01"], doc.Rules);
        Assert.Equal(["one", "two"], doc.Behaviour);
        Assert.Equal(2, doc.Examples.Count);
        Assert.Equal("Second", doc.Examples[1].Title);
        Assert.Equal("<b>x</b>", doc.Examples[1].Html);
        Assert.Null(doc.Examples[1].Note);
        Assert.Null(doc.Examples[1].Js);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("1abc")]
    [InlineData("Has Space")]
    [InlineData("under_score")]
    public void An_id_is_lower_case_letters_digits_and_dashes_and_starts_with_a_letter(string id)
        => Assert.Contains(Check(Good() with { Id = id }), p => p.Field == "Id");

    [Theory]
    [InlineData("main")]
    [InlineData("h-pills")]
    [InlineData("src-pills-1")]
    [InlineData("group-forms")]
    [InlineData("toc-dialog")]
    [InlineData("nav-menu")]
    public void An_id_the_page_makes_itself_is_refused(string id)
        => Assert.Contains(Check(Good() with { Id = id }), p => p.Field == "Id" && p.Message.Contains("page uses this id", StringComparison.Ordinal));

    [Fact]
    public void An_id_or_a_name_that_another_component_has_is_refused()
    {
        Assert.Contains(Check(Good() with { Id = "buttons" }), p => p is { Field: "Id", Message: "A component with this id exists." });
        Assert.Contains(Check(Good() with { Name = ComponentCatalog.Get("buttons")!.Name.ToUpperInvariant() }), p => p is { Field: "Name", Message: "Another component has this name." });
    }

    [Fact]
    public void A_component_may_keep_its_own_id_and_name_when_the_others_are_the_rest()
    {
        var buttons = ComponentCatalog.Get("buttons")!;

        Assert.DoesNotContain(Check(buttons, ComponentCatalog.All.Where(c => c.Id != "buttons")), p => p.Field is "Id" or "Name");
    }

    [Fact]
    public void The_group_the_summary_the_rules_and_the_examples_must_be_sensible()
    {
        Assert.Contains(Check(Good() with { Group = "Toys" }), p => p.Field == "Group");
        Assert.Contains(Check(Good() with { Icon = "nope" }), p => p.Field == "Icon");
        Assert.Contains(Check(Good() with { Summary = "short" }), p => p.Field == "Summary");
        Assert.Contains(Check(Good() with { Rules = [] }), p => p.Field == "Rules" && p.Message.Contains("at least one", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Rules = ["NOPE-99"] }), p => p.Field == "Rules" && p.Message.Contains("NOPE-99", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Behaviour = [new string('x', 301)] }), p => p.Field == "Behaviour");
        Assert.Contains(Check(Good() with { Examples = [] }), p => p.Field == "Examples");
    }

    [Fact]
    public void An_example_needs_a_title_and_markup_and_says_which_example_it_is()
    {
        var problems = Check(Good() with { Examples = [.. Good().Examples, new ComponentExample("x", "<span>no title</span>"), new ComponentExample("Third", string.Empty)] });

        Assert.Contains(problems, p => p.Field == "Examples[1].Title" && p.Message.StartsWith("Example 2:", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Field == "Examples[2].Html" && p.Message.StartsWith("Example 3:", StringComparison.Ordinal));
    }

    [Fact]
    public void The_markup_is_held_to_the_checks_of_the_toolkit_with_the_field_of_the_example()
    {
        var doc = Good() with
        {
            Examples =
            [
                new ComponentExample("Fine", """<span class="mat-pill">x</span>"""),
                new ComponentExample("Bad", """<div class="nope" style="color:red"><button>x</button><img src="https://example.com/a.png" alt="" onerror="x()" /></div>"""),
            ],
        };

        var problems = Check(doc).Where(p => p.Field == "Examples[1].Html").Select(p => p.Message).ToList();

        Assert.Contains(problems, m => m.Contains("no class 'nope'", StringComparison.Ordinal));
        Assert.Contains(problems, m => m.Contains("inline style", StringComparison.Ordinal));
        Assert.Contains(problems, m => m.Contains("inline event handler", StringComparison.Ordinal));
        Assert.Contains(problems, m => m.Contains("without a type", StringComparison.Ordinal));
        Assert.Contains(problems, m => m.Contains("another site", StringComparison.Ordinal));
        Assert.All(problems, m => Assert.StartsWith("Example 2:", m));
        Assert.DoesNotContain(Check(doc), p => p.Field == "Examples[0].Html");
    }

    [Fact]
    public void An_id_inside_an_example_must_not_be_one_of_another_component_or_of_the_page()
    {
        var existing = ComponentCatalog.All.SelectMany(c => c.Examples.SelectMany(e => MarkupChecks.IdsIn(e.Html))).First();
        string Html(string id) => $"""<input class="mat-input" id="{id}" name="a" />""";

        Assert.Contains(Check(Good() with { Examples = [new ComponentExample("Ab", Html(existing))] }), p => p.Message.Contains("already used by", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Examples = [new ComponentExample("Ab", Html("buttons"))] }), p => p.Message.Contains("already used by", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Examples = [new ComponentExample("Ab", Html("main"))] }), p => p.Message.Contains("page uses itself", StringComparison.Ordinal));
        Assert.Contains(Check(Good() with { Examples = [new ComponentExample("Ab", Html("qa-sample"))] }), p => p.Message.Contains("section of this component", StringComparison.Ordinal));
        Assert.Empty(Check(Good() with { Examples = [new ComponentExample("Ab", Html("qa-sample-field"))] }));
    }

    [Fact]
    public void Every_component_that_is_built_in_passes_when_it_is_the_one_being_edited()
    {
        foreach (var doc in ComponentCatalog.All)
        {
            var problems = ComponentValidator.Validate(doc, ComponentCatalog.All.Where(c => c.Id != doc.Id), rule => RuleBook.Get(rule) is not null, Facts)
                .ToList();
            Assert.True(problems.Count == 0, $"{doc.Id}: {string.Join("; ", problems.Select(p => p.Field + ": " + p.Message))}");
            Assert.True(ComponentValidator.IsSafe(doc), doc.Id);
        }
    }

    [Fact]
    public void A_component_read_from_the_disk_is_safe_only_when_nothing_in_it_must_never_be_drawn()
    {
        Assert.True(ComponentValidator.IsSafe(Good()));
        Assert.False(ComponentValidator.IsSafe(Good() with { Id = "../x" }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Group = "Toys" }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Examples = [] }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Examples = [new ComponentExample("A", "<script>alert(1)</script>")] }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Examples = [new ComponentExample("A", """<img src="/a.png" alt="" onerror="x()" />""")] }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Examples = [new ComponentExample("A", """<iframe src="/x"></iframe>""")] }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Examples = [new ComponentExample("A", """<a href="https://example.com">x</a>""")] }));
        Assert.False(ComponentValidator.IsSafe(Good() with { Icon = "a b" }));
    }
}
