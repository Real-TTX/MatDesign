using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>The markup of an example as code: indented and wrapped for reading, never changed in what it says.</summary>
public partial class HtmlFormatterTests
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex White();

    /// <summary>The markup without any white space between and inside: what the formatter must not change.</summary>
    private static string Core(string html) => White().Replace(html, string.Empty);

    [Fact]
    public void Nested_blocks_are_indented_by_two_spaces_and_a_short_element_with_text_is_one_line()
    {
        var code = HtmlFormatter.Pretty("<div class=\"mat-card\"><div class=\"mat-card__body\"><p>Some text</p><p>More</p></div></div>");
        Assert.Equal("<div class=\"mat-card\">\n  <div class=\"mat-card__body\">\n    <p>Some text</p>\n    <p>More</p>\n  </div>\n</div>", code);
    }

    [Fact]
    public void The_indentation_of_the_source_does_not_matter()
    {
        var a = HtmlFormatter.Pretty("            <ul>\n                <li>One</li>\n   <li>Two</li></ul>");
        var b = HtmlFormatter.Pretty("<ul><li>One</li><li>Two</li></ul>");
        Assert.Equal(b, a);
        Assert.Equal("<ul>\n  <li>One</li>\n  <li>Two</li>\n</ul>", a);
    }

    [Fact]
    public void A_long_opening_tag_has_one_attribute_per_line_under_the_first()
    {
        var code = HtmlFormatter.Pretty("<div class=\"mat-picker\" data-picker=\"select\" data-multiple data-search data-clearable data-placeholder=\"Choose apps\"><input class=\"mat-picker__input\" id=\"pk\" name=\"a\" /></div>");
        Assert.Equal(
            "<div class=\"mat-picker\"\n     data-picker=\"select\"\n     data-multiple\n     data-search\n     data-clearable\n     data-placeholder=\"Choose apps\">\n"
            + "  <input class=\"mat-picker__input\" id=\"pk\" name=\"a\" />\n</div>",
            code);

        // an element that is too long for its line is broken the same way at any depth, and a tag that fits is left alone
        var deep = HtmlFormatter.Pretty("<div><div><input class=\"mat-picker__input\" id=\"pk-apps\" name=\"apps\" value=\"mail, files\" /></div></div>");
        Assert.Contains("    <input class=\"mat-picker__input\"\n           id=\"pk-apps\"\n           name=\"apps\"\n           value=\"mail, files\" />", deep);
    }

    [Fact]
    public void Inline_elements_stay_in_the_line_of_their_text_and_an_icon_stays_whole()
    {
        var code = HtmlFormatter.Pretty("<button type=\"button\" class=\"mat-btn\"><svg class=\"mat-icon\"><use href=\"#mat-i-check\"></use></svg><span>Save</span></button>");
        Assert.Equal("<button type=\"button\" class=\"mat-btn\">\n  <svg class=\"mat-icon\"><use href=\"#mat-i-check\"></use></svg><span>Save</span>\n</button>", code);
        // where the whole element fits on a line it is one line
        Assert.Equal("<button><svg class=\"i\"><use href=\"#x\"></use></svg><span>Go</span></button>", HtmlFormatter.Pretty("<button><svg class=\"i\"><use href=\"#x\"></use></svg><span>Go</span></button>"));
        var text = HtmlFormatter.Pretty("<p>Press <code>Enter</code> to <strong>save</strong> it.</p>");
        Assert.Equal("<p>Press <code>Enter</code> to <strong>save</strong> it.</p>", text);
    }

    [Fact]
    public void Text_that_is_too_long_is_wrapped_at_the_words_and_nothing_else_moves()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 40));
        var code = HtmlFormatter.Pretty($"<div><p>{words}</p></div>", 60);
        var lines = code.Split('\n');
        Assert.True(lines.Length > 4);
        Assert.All(lines, l => Assert.True(l.Length <= 60, l));
        Assert.Equal(Core($"<div><p>{words}</p></div>"), Core(code));
    }

    [Fact]
    public void Pre_and_textarea_keep_their_content_exactly_and_comments_are_lines_of_their_own()
    {
        var code = HtmlFormatter.Pretty("<div><!-- a note --><pre class=\"mat-code\">line 1\n   line 2</pre><textarea rows=\"2\">a\n b</textarea></div>");
        Assert.Contains("  <!-- a note -->", code);
        Assert.Contains("  <pre class=\"mat-code\">line 1\n   line 2</pre>", code);
        Assert.Contains("  <textarea rows=\"2\">a\n b</textarea>", code);
    }

    [Fact]
    public void Void_elements_and_self_closing_tags_are_not_closed_again()
    {
        // it fits on a line
        Assert.Equal("<div><input class=\"a\" /><br><img src=\"x.png\" alt=\"\"></div>", HtmlFormatter.Pretty("<div><input class=\"a\" /><br><img src=\"x.png\" alt=\"\"></div>"));
        // it does not: every void element has a line of its own and none gets a closing tag
        var narrow = HtmlFormatter.Pretty("<div><input class=\"a\" /><br><img src=\"x.png\" alt=\"\"></div>", 40);
        Assert.Equal("<div>\n  <input class=\"a\" />\n  <br>\n  <img src=\"x.png\" alt=\"\">\n</div>", narrow);
    }

    [Theory]
    [InlineData("<div><p>never closed</div>")]
    [InlineData("<div></span></div>")]
    [InlineData("<div><!-- never closed </div>")]
    [InlineData("<pre>never closed")]
    public void Markup_that_cannot_be_read_is_returned_as_it_is_only_dedented(string markup)
        => Assert.Equal(markup, HtmlFormatter.Pretty("    " + markup + "\n"));

    [Fact]
    public void A_less_than_sign_in_text_is_text()
        => Assert.Equal("<p>a < b</p>", HtmlFormatter.Pretty("<p>a < b</p>"));

    [Fact]
    public void An_empty_text_and_a_text_without_markup_come_back_sensibly()
    {
        Assert.Equal(string.Empty, HtmlFormatter.Pretty("  \n \n"));
        Assert.Equal("just text", HtmlFormatter.Pretty("   just   text  "));
    }

    [Fact]
    public void Formatting_twice_gives_the_same_as_once()
    {
        var once = HtmlFormatter.Pretty("<table class=\"mat-table\"><thead><tr><th scope=\"col\">Name</th></tr></thead><tbody><tr><td data-label=\"Name\"><a href=\"#\">Alpha</a></td></tr></tbody></table>");
        Assert.Equal(once, HtmlFormatter.Pretty(once));
    }

    [Fact]
    public void Every_example_of_the_catalogue_keeps_its_content_formats_the_same_way_twice_and_fits_the_column()
    {
        var problems = new List<string>();
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var example in component.Examples.Where(e => e.Js is null && e.Html.Length > 0))
            {
                var code = HtmlFormatter.Pretty(example.Html);
                if (Core(code) != Core(example.Html))
                {
                    problems.Add($"{component.Id} / {example.Title}: the formatter changed something besides white space");
                }

                if (HtmlFormatter.Pretty(code) != code)
                {
                    problems.Add($"{component.Id} / {example.Title}: not the same when formatted twice");
                }

                if (code.Contains("\t") || code.Split('\n').Any(l => l.TrimEnd() != l))
                {
                    problems.Add($"{component.Id} / {example.Title}: a tab or a space at the end of a line");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void The_code_block_keeps_its_indentation_and_its_first_line_is_not_pushed_by_the_padding_of_an_inline_code()
    {
        // the indentation is the structure of the code: no wrapping into the place of the next line, and the code element of a block has no padding (it would move the first line only)
        Assert.Matches(@"\.mat-code code \{[^}]*padding: 0;", TestPaths.ReadToolkit("css/matui.controls.css"));
        var site = File.ReadAllText(TestPaths.Web("wwwroot/css/site.css"));
        Assert.Matches(@"\.doc-code \.mat-code \{[^}]*white-space: pre;", site);
    }

    [Fact]
    public void The_code_that_the_pages_show_is_the_formatted_markup()
    {
        var shown = MatDesign.Web.Studio.CodeHighlighter.Of("<div class=\"a\"><p>x</p></div>", script: false);
        Assert.Contains("\n  ", shown);
        Assert.DoesNotContain("&lt;div class=\"a\"&gt;&lt;p&gt;", shown);
        // a script is shown as written
        Assert.Equal(MatDesign.Web.Studio.CodeHighlighter.Script("a();  b();"), MatDesign.Web.Studio.CodeHighlighter.Of("a();  b();", script: true));
    }
}
