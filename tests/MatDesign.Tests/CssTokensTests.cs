using MatDesign.Core;

namespace MatDesign.Tests;

public class CssTokensTests
{
    [Fact]
    public void A_declaration_keeps_its_parentheses_and_its_trailing_comment()
    {
        var declarations = CssTokens.ParseDeclarations("--a: color-mix(in oklab, var(--b) 86%, black); /* Hover. */\n  --c: 12px;\n --d: 1px solid red; /* Border. */");

        Assert.Equal(3, declarations.Count);
        Assert.Equal("--a", declarations[0].Name);
        Assert.Equal("color-mix(in oklab, var(--b) 86%, black)", declarations[0].Value);
        Assert.Equal("Hover.", declarations[0].Comment);
        Assert.Equal("12px", declarations[1].Value);
        Assert.Null(declarations[1].Comment);
        Assert.Equal("1px solid red", declarations[2].Value);
    }

    [Fact]
    public void A_block_knows_the_media_query_it_sits_in()
    {
        var blocks = CssTokens.ParseBlocks("@media (pointer: coarse) { :root { --x: 1px; } }\n:root { --y: 2px; }\n/* a comment { with a brace */\n.a, .b { --z: 3px; }");

        Assert.Equal(3, blocks.Count);
        Assert.Equal("(pointer: coarse)", blocks[0].Media);
        Assert.Null(blocks[1].Media);
        Assert.Equal(".a, .b", blocks[2].Selector);
        Assert.Equal("--z", Assert.Single(blocks[2].Declarations).Name);
    }

    [Fact]
    public void Tokens_take_their_group_from_the_heading_and_their_dark_value_from_the_override()
    {
        const string css = """
            :root {
              /* ---- Surfaces ---- */
              --mat-bg: #ffffff; /* The page. */
              /* ---- Text ---- */
              --mat-text: #111111; /* Body text. */
            }
            :root,
            [data-app] {
              --mat-accent: var(--mat-app-accent); /* Fill. */
            }
            :root[data-theme="dark"] {
              --mat-bg: #000000;
            }
            """;

        var tokens = CssTokens.Tokens(css);

        Assert.Equal(3, tokens.Count);
        var bg = tokens.Single(t => t.Name == "--mat-bg");
        Assert.Equal("Surfaces", bg.Group);
        Assert.Equal("#000000", bg.Dark);
        Assert.Equal("The page.", bg.Description);
        var text = tokens.Single(t => t.Name == "--mat-text");
        Assert.Equal("Text", text.Group);
        Assert.Null(text.Dark);
        Assert.Equal("Accent", tokens.Single(t => t.Name == "--mat-accent").Group);
    }

    [Theory]
    [InlineData("#abc", true)]
    [InlineData("rgba(1, 2, 3, .5)", true)]
    [InlineData("color-mix(in oklab, red 50%, blue)", true)]
    [InlineData("12px", false)]
    [InlineData("120ms", false)]
    public void A_token_knows_whether_it_is_a_colour(string value, bool isColor)
        => Assert.Equal(isColor, new DesignToken("--mat-x", value, null, string.Empty, string.Empty).IsColor);
}
