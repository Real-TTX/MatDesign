using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;

namespace MatDesign.Tests;

/// <summary>The colours of the code of an example: nothing from the code reaches the page unencoded, the text stays the code, and the parts get the right class.</summary>
public sealed class CodeHighlighterTests
{
    /// <summary>The text of highlighted code: the spans taken away, the entities decoded. It must be the code again.</summary>
    private static string Text(string highlighted) => WebUtility.HtmlDecode(Regex.Replace(highlighted, @"</?span[^>]*>", string.Empty));

    [Fact]
    public void A_tag_is_split_into_its_brackets_name_attributes_and_values()
    {
        var html = CodeHighlighter.Html("<a class=\"mat-btn\" href='#x' disabled>Go</a>");

        Assert.Contains("<span class=\"mat-code__pun\">&lt;</span><span class=\"mat-code__tag\">a</span>", html);
        Assert.Contains("<span class=\"mat-code__attr\">class</span><span class=\"mat-code__pun\">=</span><span class=\"mat-code__str\">&quot;mat-btn&quot;</span>", html);
        Assert.Contains("<span class=\"mat-code__attr\">href</span><span class=\"mat-code__pun\">=</span><span class=\"mat-code__str\">&#39;#x&#39;</span>", html);
        Assert.Contains("<span class=\"mat-code__attr\">disabled</span>", html);
        Assert.Contains("&gt;</span>Go<span class=\"mat-code__pun\">&lt;/</span><span class=\"mat-code__tag\">a</span>", html);
    }

    [Fact]
    public void A_comment_and_a_doctype_are_marked_and_a_self_closing_tag_keeps_its_slash()
    {
        var html = CodeHighlighter.Html("<!DOCTYPE html>\n<!-- a <b> comment -->\n<input type=\"text\" />");

        Assert.Contains("<span class=\"mat-code__tag\">!DOCTYPE</span>", html);
        Assert.Contains("<span class=\"mat-code__com\">&lt;!-- a &lt;b&gt; comment --&gt;</span>", html);
        Assert.Contains("<span class=\"mat-code__pun\">/</span><span class=\"mat-code__pun\">&gt;</span>", html);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=\"alert(1)\">")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("1 < 2 and 3 > 2 &amp; done")]
    [InlineData("<div title=\"a > b\">text</div>")]
    [InlineData("<p class=\"unfinished")]
    [InlineData("<")]
    [InlineData("")]
    public void Nothing_of_the_code_reaches_the_page_as_markup_and_the_text_stays_the_code(string code)
    {
        var html = CodeHighlighter.Html(code);

        Assert.Equal(code, Text(html));
        // the only tags are the spans of the highlighter
        Assert.DoesNotContain(Regex.Matches(html, "<[^>]*>").Select(m => m.Value), tag => !Regex.IsMatch(tag, @"^</?span( class=""mat-code__[a-z]+"")?>$"));
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_example_of_the_catalogue_keeps_its_text_and_only_spans_are_added()
    {
        var count = 0;
        foreach (var example in ComponentCatalog.All.SelectMany(c => c.Examples))
        {
            var html = CodeHighlighter.Html(example.Html);
            Assert.Equal(example.Html, Text(html));
            Assert.DoesNotContain(Regex.Matches(html, "<[^>]*>").Select(m => m.Value), tag => !Regex.IsMatch(tag, @"^</?span( class=""mat-code__[a-z]+"")?>$"));
            if (example.Js is not null)
            {
                Assert.Equal(example.Js, Text(CodeHighlighter.Script(example.Js)));
            }

            count++;
        }

        Assert.True(count > 100);
    }

    [Fact]
    public void Script_has_its_comments_strings_numbers_and_keywords_marked()
    {
        var script = CodeHighlighter.Script("// ask first\nconst ok = await MatUi.confirm('Restart?', { ok: \"Restart\", delay: 250 });\n/* done */ if (ok) { return `x\\`y`; }");

        Assert.Contains("<span class=\"mat-code__com\">// ask first</span>", script);
        Assert.Contains("<span class=\"mat-code__kw\">const</span> ok = <span class=\"mat-code__kw\">await</span> MatUi.confirm(<span class=\"mat-code__str\">&#39;Restart?&#39;</span>", script);
        Assert.Contains("<span class=\"mat-code__str\">&quot;Restart&quot;</span>", script);
        Assert.Contains("<span class=\"mat-code__num\">250</span>", script);
        Assert.Contains("<span class=\"mat-code__com\">/* done */</span>", script);
        Assert.Contains("<span class=\"mat-code__str\">`x\\`y`</span>", script);
    }

    [Fact]
    public void A_word_that_only_contains_a_keyword_or_follows_a_dot_is_not_marked()
    {
        var script = CodeHighlighter.Script("constant.new = format1 + a2");

        Assert.DoesNotContain("mat-code__kw", script);
        Assert.DoesNotContain("mat-code__num", script);
    }

    [Fact]
    public void Of_chooses_markup_or_script()
    {
        Assert.Contains("mat-code__tag", CodeHighlighter.Of("<b>x</b>", script: false));
        Assert.DoesNotContain("mat-code__tag", CodeHighlighter.Of("<b>x</b>", script: true));
    }
}
