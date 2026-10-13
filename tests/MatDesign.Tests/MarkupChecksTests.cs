using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>Each fault of the markup of an example, one by one: the checks that hold the built-in examples and the ones typed in the Studio.</summary>
public class MarkupChecksTests
{
    private static readonly MarkupFacts Facts = new(
        new HashSet<string> { "mat-btn", "mat-btn--primary", "mat-field", "mat-label", "mat-input", "mat-table", "mat-table__cell-actions", "mat-dialog", "mat-dialog__title", "mat-dialog__close", "mat-icon", "mat-tag", "mat-dot" },
        new HashSet<string> { "check", "x" });

    private static IReadOnlyList<MarkupProblem> Check(params string[] examples) => MarkupChecks.Check(examples, Facts);

    private static void Flags(string check, string html)
        => Assert.Contains(Check(html), p => p.Check == check);

    private static void Clean(string html)
        => Assert.Empty(Check(html));

    [Fact]
    public void Good_markup_has_no_problem()
    {
        Clean("""<div class="mat-field"><label class="mat-label" for="a">Name</label><input class="mat-input" id="a" name="a" /></div>""");
        Clean("""<button type="button" class="mat-btn mat-btn--primary"><svg class="mat-icon"><use href="#mat-i-check"></use></svg><span>Save</span></button>""");
        Clean("""<table class="mat-table"><tbody><tr><td data-label="Name">A</td></tr></tbody></table>""");
        Clean("""<a class="mat-btn" href="/Rules" aria-label="Rules"><svg class="mat-icon"><use href="#mat-i-check"></use></svg></a>""");
    }

    [Fact]
    public void A_class_the_toolkit_does_not_ship_is_flagged_once_per_class()
    {
        var problems = Check("""<div class="mat-btn nope another"><span class="nope"></span></div>""");

        Assert.Equal(["another", "nope"], problems.Where(p => p.Check == MarkupChecks.Classes).Select(p => p.Message.Split('\'')[1]).Order());
    }

    [Theory]
    [InlineData("""<p style="color:red">x</p>""")]
    [InlineData("""<p STYLE = "x">x</p>""")]
    [InlineData("""<button type="button" onclick="x()">x</button>""")]
    [InlineData("""<img src="/a.png" alt="" onerror="x()" />""")]
    [InlineData("""<div onpointerdown="x()"></div>""")]
    [InlineData("""<script>alert(1)</script>""")]
    [InlineData("""<script src="/x.js"></script>""")]
    [InlineData("""<a href="javascript:alert(1)">x</a>""")]
    [InlineData("""<iframe srcdoc="x"></iframe>""")]
    public void An_inline_style_handler_script_or_script_address_is_flagged(string html)
        => Flags(MarkupChecks.Inline, html);

    [Theory]
    [InlineData("""<iframe src="/x"></iframe>""")]
    [InlineData("""<object data="/x"></object>""")]
    [InlineData("""<embed src="/x" />""")]
    [InlineData("""<link rel="stylesheet" href="/x.css" />""")]
    [InlineData("""<meta http-equiv="refresh" content="0" />""")]
    [InlineData("""<base href="/" />""")]
    [InlineData("""<style>body{display:none}</style>""")]
    public void An_element_that_does_not_belong_in_an_example_is_flagged(string html)
        => Flags(MarkupChecks.Elements, html);

    [Theory]
    [InlineData("""<img src="https://example.com/a.png" alt="" />""")]
    [InlineData("""<img src="//example.com/a.png" alt="" />""")]
    [InlineData("""<a href="http://example.com">x</a>""")]
    [InlineData("""<form action="https://example.com/post"></form>""")]
    [InlineData("""<button type="submit" formaction="//example.com">x</button>""")]
    public void An_address_of_another_site_is_flagged(string html)
        => Flags(MarkupChecks.Outside, html);

    [Fact]
    public void An_address_on_this_site_and_a_data_attribute_are_not_outside()
        => Clean("""<a href="/Rules/Detail?id=FORM-01">x</a><span data-copy="//not-an-address">x</span><a href="#top">x</a>""");

    [Fact]
    public void An_icon_that_is_not_in_the_sprite_is_flagged()
        => Flags(MarkupChecks.Icons, """<svg class="mat-icon"><use href="#mat-i-nope"></use></svg>""");

    [Fact]
    public void A_table_cell_without_a_label_is_flagged_unless_it_holds_the_actions()
    {
        Flags(MarkupChecks.CellLabel, """<table class="mat-table"><tr><td>A</td></tr></table>""");
        Clean("""<table class="mat-table"><tr><td class="mat-table__cell-actions">A</td></tr></table>""");
    }

    [Fact]
    public void A_button_without_a_type_is_flagged()
        => Flags(MarkupChecks.ButtonType, """<button class="mat-btn">x</button>""");

    [Fact]
    public void A_control_without_an_id_is_flagged_unless_it_is_hidden()
    {
        Flags(MarkupChecks.Controls, """<input class="mat-input" name="a" />""");
        Flags(MarkupChecks.Controls, """<select class="mat-input"><option>x</option></select>""");
        Clean("""<input type="hidden" name="a" value="1" />""");
    }

    [Fact]
    public void A_dialog_needs_its_name_its_title_and_its_x()
    {
        Flags(MarkupChecks.Dialog, """<dialog class="mat-dialog"><h2>x</h2></dialog>""");
        Clean("""<dialog class="mat-dialog" aria-labelledby="t"><h2 class="mat-dialog__title" id="t">T</h2><button type="button" class="mat-btn mat-dialog__close" aria-label="Close">x</button></dialog>""");
    }

    [Fact]
    public void A_button_that_is_only_an_icon_needs_a_name()
    {
        Flags(MarkupChecks.IconButton, """<button type="button" class="mat-btn"><svg class="mat-icon"><use href="#mat-i-x"></use></svg></button>""");
        Assert.Equal(1, MarkupChecks.IconOnlyButtons("""<button type="button"><svg><use href="#mat-i-x"></use></svg></button>"""));
    }

    [Fact]
    public void The_colour_of_a_mark_is_a_data_attribute_and_an_inline_style_for_it_is_still_refused()
    {
        // COLOR-10
        Clean("""<span class="mat-tag" data-colour="#d6336c">Invoices</span><span class="mat-tag" data-colour="#ABC">x</span><span class="mat-dot" data-colour="#0d9488" aria-hidden="true"></span>""");
        Clean("""<svg class="mat-icon" data-colour="#7c3aed"><use href="#mat-i-check"></use></svg>""");
        Flags(MarkupChecks.Inline, """<span class="mat-dot" style="background: #d6336c" aria-hidden="true"></span>""");
        Flags(MarkupChecks.Inline, """<span class="mat-tag" data-colour="#d6336c" style="--mat-swatch: #d6336c">x</span>""");
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#abcd")]
    [InlineData("abc")]
    [InlineData("#ggg")]
    [InlineData("rgb(1, 2, 3)")]
    [InlineData("")]
    public void A_data_colour_that_is_not_a_hex_colour_is_flagged_because_the_script_ignores_it(string value)
        => Flags(MarkupChecks.Colour, $"""<span class="mat-tag" data-colour="{value}">x</span>""");

    [Fact]
    public void A_tag_that_is_not_closed_is_flagged()
    {
        Flags(MarkupChecks.Balanced, """<div class="mat-field"><span>x</div>""");
        Flags(MarkupChecks.Balanced, """<ul><li>a</ul>""");
    }

    [Fact]
    public void Ids_that_appear_twice_across_the_examples_are_flagged_and_so_is_a_reference_that_points_at_nothing()
    {
        var problems = Check("""<input class="mat-input" id="same" name="a" />""", """<input class="mat-input" id="same" name="b" /><label class="mat-label" for="missing">x</label>""");

        Assert.Contains(problems, p => p.Check == MarkupChecks.Ids && p.Message.Contains("same", StringComparison.Ordinal) && p.Example == -1);
        Assert.Contains(problems, p => p.Check == MarkupChecks.References && p.Message.Contains("missing", StringComparison.Ordinal));
        Clean("""<label class="mat-label" for="a">x</label><input class="mat-input" id="a" name="a" /><button type="button" class="mat-btn" data-dialog-open="d">x</button><dialog class="mat-dialog" id="d" aria-labelledby="dt"><h2 class="mat-dialog__title" id="dt">T</h2><button type="button" class="mat-btn mat-dialog__close" aria-label="Close">x</button></dialog>""");
    }

    [Fact]
    public void A_problem_knows_which_example_it_is_in()
    {
        var problems = Check("""<div class="mat-btn"></div>""", """<div class="nope"></div>""");

        Assert.Equal(1, Assert.Single(problems).Example);
    }

    [Fact]
    public void The_dangerous_kinds_are_the_ones_that_keep_a_page_safe()
        => Assert.Equal(["elements", "inline", "outside"], MarkupChecks.Dangerous.Order());

    [Fact]
    public void Classes_are_read_from_selectors_and_not_from_attribute_selectors()
    {
        var classes = MarkupChecks.ClassesIn(["""
            .mat-a, .mat-b:hover { x: y; }
            [data-x=".not-a-class"] .mat-c > .mat-d::before { x: y; }
            """]);

        Assert.Equal(["mat-a", "mat-b", "mat-c", "mat-d"], classes.Order());
    }
}
