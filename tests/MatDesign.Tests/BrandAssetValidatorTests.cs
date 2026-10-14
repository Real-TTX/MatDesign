using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>IDENT-08, the files of a brand: decided by content, within limits, an SVG against an allowlist, the item against its rules. A test for every refusal, and a corpus of hostile SVGs.</summary>
public sealed class BrandAssetValidatorTests
{
    private static AssetInspection Inspect(byte[] bytes, AssetSlot slot) => BrandAssetValidator.Inspect(bytes, slot);

    private static AssetInspection Svg(string text, AssetSlot slot = AssetSlot.Logo) => Inspect(AssetFixtures.Bytes(text), slot);

    private static string Refusal(AssetInspection inspection)
    {
        Assert.False(inspection.Ok, "expected a refusal");
        return string.Join(" ", inspection.Problems);
    }

    // ------------------------------------------------------------------------------------------------ the type comes from the content

    [Fact]
    public void A_png_an_svg_an_ico_a_woff_and_a_woff2_are_known_by_their_content()
    {
        Assert.Equal(".png", Inspect(AssetFixtures.Png(64, 32), AssetSlot.Logo).Ext);
        Assert.Equal(".svg", Svg(AssetFixtures.GoodSvg()).Ext);
        Assert.Equal(".ico", Inspect(AssetFixtures.Ico(), AssetSlot.Favicon).Ext);
        Assert.Equal(".woff", Inspect(AssetFixtures.Woff(), AssetSlot.Font).Ext);
        Assert.Equal(".woff2", Inspect(AssetFixtures.Woff2(), AssetSlot.Font).Ext);
        Assert.All(new[]
        {
            Inspect(AssetFixtures.Png(64, 32), AssetSlot.Logo), Svg(AssetFixtures.GoodSvg()), Inspect(AssetFixtures.Ico(), AssetSlot.Favicon),
            Inspect(AssetFixtures.Woff(), AssetSlot.Font), Inspect(AssetFixtures.Woff2(), AssetSlot.Font),
        }, i => Assert.True(i.Ok, string.Join(" ", i.Problems)));
    }

    [Fact]
    public void A_windows_program_with_a_picture_name_is_refused_because_of_what_is_in_it()
    {
        var exe = new byte[2048];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        var problem = Refusal(Inspect(exe, AssetSlot.Logo));
        Assert.Contains("not a file a brand may use", problem);
        Assert.Contains("name of the file", problem);
    }

    [Theory]
    [InlineData("%PDF-1.7 hello")]
    [InlineData("GIF89a....")]
    [InlineData("plain text, not xml")]
    [InlineData("#!/bin/sh\nrm -rf /")]
    public void Anything_else_is_refused(string text) => Assert.Contains("not a file a brand may use", Refusal(Inspect(AssetFixtures.Bytes(text), AssetSlot.Favicon)));

    [Fact]
    public void An_empty_file_is_refused() => Assert.Contains("empty", Refusal(Inspect([], AssetSlot.Logo)));

    [Fact]
    public void A_file_that_is_not_for_the_slot_is_refused_with_what_it_is()
    {
        Assert.Contains("a logo must be an SVG or a PNG", Refusal(Inspect(AssetFixtures.Woff2(), AssetSlot.Logo)));
        Assert.Contains("an ICO", Refusal(Inspect(AssetFixtures.Ico(), AssetSlot.Logo)));
        Assert.Contains("an app icon must be a PNG", Refusal(Svg(AssetFixtures.GoodSvg(), AssetSlot.AppIcon)));
        Assert.Contains("a font must be", Refusal(Inspect(AssetFixtures.Png(16, 16), AssetSlot.Font)));
        Assert.Contains("a favicon must be", Refusal(Inspect(AssetFixtures.Woff(), AssetSlot.Favicon)));
    }

    // ------------------------------------------------------------------------------------------------ sizes

    [Fact]
    public void The_size_limits_are_those_of_the_rule()
    {
        Assert.Equal(100 * 1024, BrandAssetValidator.MaxBytes(".svg"));
        Assert.Equal(500 * 1024, BrandAssetValidator.MaxBytes(".png"));
        Assert.Equal(200 * 1024, BrandAssetValidator.MaxBytes(".ico"));
        Assert.Equal(400 * 1024, BrandAssetValidator.MaxBytes(".woff2"));
        Assert.Equal(400 * 1024, BrandAssetValidator.MaxBytes(".woff"));
        Assert.Equal(12, BrandAssetValidator.MaxFaces);
        Assert.Equal(3, BrandAssetValidator.MaxFamilies);
        Assert.Equal(2048, BrandAssetValidator.MaxPixels);
    }

    [Fact]
    public void A_file_over_its_limit_is_refused_before_it_is_read()
    {
        var bigSvg = AssetFixtures.GoodSvg(inner: "<!-- " + new string('x', 101 * 1024) + " -->");
        Assert.Contains("100 KB at most", Refusal(Svg(bigSvg)));
        Assert.Contains("400 KB at most", Refusal(Inspect(AssetFixtures.Woff2(extra: 401 * 1024), AssetSlot.Font)));
        Assert.Contains("400 KB at most", Refusal(Inspect(AssetFixtures.Woff(extra: 401 * 1024), AssetSlot.Font)));
        var bigPng = AssetFixtures.Png(256, 256).Concat(new byte[501 * 1024]).ToArray();
        Assert.Contains("500 KB at most", Refusal(Inspect(bigPng, AssetSlot.Logo)));
        var bigIco = AssetFixtures.Ico().Concat(new byte[201 * 1024]).ToArray();
        Assert.Contains("200 KB at most", Refusal(Inspect(bigIco, AssetSlot.Favicon)));
    }

    // ------------------------------------------------------------------------------------------------ PNG

    [Fact]
    public void A_png_reports_its_size_and_may_be_wide()
    {
        var wide = Inspect(AssetFixtures.Png(480, 60), AssetSlot.Logo);
        Assert.True(wide.Ok, string.Join(" ", wide.Problems));
        Assert.Equal((480, 60), (wide.Width, wide.Height));
        Assert.True(Inspect(AssetFixtures.Png(2048, 256), AssetSlot.Logo).Ok);
    }

    [Fact]
    public void A_png_that_is_too_big_or_too_thin_is_refused()
    {
        Assert.Contains("2048 px each way", Refusal(Inspect(AssetFixtures.Png(2049, 64), AssetSlot.Logo)));
        Assert.Contains("2048 px each way", Refusal(Inspect(AssetFixtures.Png(64, 4000), AssetSlot.Logo)));
        Assert.Contains("8 times wider", Refusal(Inspect(AssetFixtures.Png(800, 64), AssetSlot.Logo)));
        Assert.Contains("at least 16 px", Refusal(Inspect(AssetFixtures.Png(8, 8), AssetSlot.Wordmark)));
    }

    [Fact]
    public void A_png_that_is_damaged_cut_off_or_followed_by_something_is_refused()
    {
        var good = AssetFixtures.Png(64, 64);
        Assert.Contains("cut off", Refusal(Inspect(good[..^12], AssetSlot.Logo)));
        Assert.Contains("data after the end", Refusal(Inspect([.. good, .. "<script>alert(1)</script>"u8.ToArray()], AssetSlot.Logo)));
        var damaged = (byte[])good.Clone();
        damaged[45] ^= 0xFF;
        Assert.Contains("checksum", Refusal(Inspect(damaged, AssetSlot.Logo)));
        var noHeaderFirst = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(AssetFixtures.Chunk("IDAT", [1, 2, 3])).Concat(AssetFixtures.Chunk("IEND", [])).ToArray();
        Assert.Contains("header", Refusal(Inspect(noHeaderFirst, AssetSlot.Logo)));
    }

    [Fact]
    public void A_favicon_png_is_square_and_an_app_icon_is_512()
    {
        Assert.True(Inspect(AssetFixtures.Png(32, 32), AssetSlot.Favicon).Ok);
        Assert.Contains("square", Refusal(Inspect(AssetFixtures.Png(32, 48), AssetSlot.Favicon)));
        Assert.Contains("16 to 512", Refusal(Inspect(AssetFixtures.Png(1024, 1024), AssetSlot.Favicon)));
        Assert.True(Inspect(AssetFixtures.Png(512, 512), AssetSlot.AppIcon).Ok);
        Assert.Contains("512 x 512", Refusal(Inspect(AssetFixtures.Png(256, 256), AssetSlot.AppIcon)));
        Assert.Contains("512 x 512", Refusal(Inspect(AssetFixtures.Png(512, 513), AssetSlot.AppIcon)));
    }

    // ------------------------------------------------------------------------------------------------ ICO and fonts

    [Fact]
    public void An_ico_is_read_by_its_directory_and_a_broken_one_is_refused()
    {
        var ico = Inspect(AssetFixtures.Ico(), AssetSlot.Favicon);
        Assert.True(ico.Ok, string.Join(" ", ico.Problems));
        Assert.Equal((16, 16), (ico.Width, ico.Height));
        var outside = AssetFixtures.Ico();
        outside[14] = 0xFF;
        outside[15] = 0xFF;
        Assert.Contains("outside the file", Refusal(Inspect(outside, AssetSlot.Favicon)));
        Assert.Contains("too short", Refusal(Inspect([0, 0, 1, 0, 1, 0], AssetSlot.Favicon)));
        var notImage = AssetFixtures.Ico();
        notImage[22] = 1;
        Assert.Contains("neither a PNG nor a bitmap", Refusal(Inspect(notImage, AssetSlot.Favicon)));
    }

    [Fact]
    public void A_font_has_the_length_in_its_header_and_a_known_flavour()
    {
        var woff = AssetFixtures.Woff();
        Assert.Contains("length in its header", Refusal(Inspect(woff[..^4], AssetSlot.Font)));
        var flavour = AssetFixtures.Woff2();
        flavour[4] = 0x11;
        Assert.Contains("TrueType or OpenType", Refusal(Inspect(flavour, AssetSlot.Font)));
        Assert.Contains("shorter than its header", Refusal(Inspect("wOF2short"u8.ToArray(), AssetSlot.Font)));
        var table = AssetFixtures.Woff();
        table[52] = 0xFF;
        Assert.Contains("lies outside the file", Refusal(Inspect(table, AssetSlot.Font)));
    }

    // ------------------------------------------------------------------------------------------------ SVG, the good ones

    [Fact]
    public void A_plain_svg_with_gradients_references_inside_the_file_and_a_style_attribute_passes()
    {
        var svg = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <svg xmlns="{AssetFixtures.Svg}" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 100 50" width="200" height="100" role="img" aria-label="Acme">
              <title>Acme</title>
              <defs>
                <linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#0f766e"/><stop offset="1" stop-color="#115e59" style="stop-opacity:0.9"/></linearGradient>
                <clipPath id="c"><rect width="100" height="50"/></clipPath>
                <path id="p" d="M0 0L10 10"/>
              </defs>
              <g clip-path="url(#c)"><rect width="100" height="50" fill="url(#g)" style="stroke:none;fill-opacity:1"/><use xlink:href="#p"/><use href="#p" transform="translate(5 5)"/></g>
              <text x="10" y="30" font-family="Arial" font-size="12" fill="#ffffff">Acme</text>
            </svg>
            """;
        var result = Svg(svg);
        Assert.True(result.Ok, string.Join(" ", result.Problems));
        Assert.Equal((200, 100), (result.Width, result.Height));
    }

    [Fact]
    public void An_svg_that_has_only_a_view_box_is_sized_by_it_and_a_wide_one_is_a_logo()
    {
        var square = Svg(AssetFixtures.GoodSvg());
        Assert.Equal((64, 64), (square.Width, square.Height));
        var wide = Svg(AssetFixtures.WideSvg());
        Assert.True(wide.Ok, string.Join(" ", wide.Problems));
        Assert.Equal((240, 40), (wide.Width, wide.Height));
        Assert.Contains("8 times wider", Refusal(Svg($"<svg xmlns=\"{AssetFixtures.Svg}\" viewBox=\"0 0 900 100\"><rect width=\"900\" height=\"100\"/></svg>")));
    }

    [Fact]
    public void A_favicon_svg_may_not_be_wide()
    {
        Assert.True(Svg(AssetFixtures.GoodSvg(), AssetSlot.Favicon).Ok);
        Assert.Contains("square", Refusal(Svg(AssetFixtures.WideSvg(), AssetSlot.Favicon)));
    }

    // ------------------------------------------------------------------------------------------------ SVG, the hostile corpus

    public static TheoryData<string, string, string> Hostile()
    {
        var ns = $"xmlns=\"{AssetFixtures.Svg}\"";
        var data = new TheoryData<string, string, string>
        {
            { "a script element", $"<svg {ns} viewBox=\"0 0 10 10\"><script>alert(document.cookie)</script></svg>", "<script>" },
            { "a script element with CDATA", $"<svg {ns} viewBox=\"0 0 10 10\"><script><![CDATA[alert(1)]]></script></svg>", "<script>" },
            { "onload on the root", $"<svg {ns} viewBox=\"0 0 10 10\" onload=\"alert(1)\"><rect width=\"1\" height=\"1\"/></svg>", "event handler" },
            { "onclick on a shape", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" onclick=\"alert(1)\"/></svg>", "event handler" },
            { "onmouseover in capitals", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" OnMouseOver=\"alert(1)\"/></svg>", "event handler" },
            { "foreignObject with html", $"<svg {ns} viewBox=\"0 0 10 10\"><foreignObject width=\"10\" height=\"10\"><body xmlns=\"http://www.w3.org/1999/xhtml\"><img src=x onerror=alert(1)></img></body></foreignObject></svg>", "foreignObject" },
            { "a style element", $"<svg {ns} viewBox=\"0 0 10 10\"><style>rect {{ fill: url(https://evil.example/x) }}</style><rect width=\"1\" height=\"1\"/></svg>", "<style>" },
            { "an image from another site", $"<svg {ns} viewBox=\"0 0 10 10\"><image href=\"https://evil.example/track.png\" width=\"10\" height=\"10\"/></svg>", "<image>" },
            { "an image with a data address", $"<svg {ns} viewBox=\"0 0 10 10\"><image href=\"data:image/png;base64,AAAA\" width=\"10\" height=\"10\"/></svg>", "<image>" },
            { "use that points to another file", $"<svg {ns} viewBox=\"0 0 10 10\"><use href=\"https://evil.example/x.svg#a\"/></svg>", "leads out of the document" },
            { "use that points to a relative file", $"<svg {ns} viewBox=\"0 0 10 10\"><use href=\"other.svg#a\"/></svg>", "leads out of the document" },
            { "xlink href with javascript", $"<svg {ns} xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 10 10\"><use xlink:href=\"javascript:alert(1)\"/></svg>", "leads out of the document" },
            { "a link element", $"<svg {ns} viewBox=\"0 0 10 10\"><a href=\"https://evil.example\"><rect width=\"10\" height=\"10\"/></a></svg>", "<a>" },
            { "animate that sets a link", $"<svg {ns} viewBox=\"0 0 10 10\"><a><animate attributeName=\"href\" to=\"javascript:alert(1)\"/></a></svg>", "<a>" },
            { "set that changes an attribute", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"1\" height=\"1\"><set attributeName=\"onmouseover\" to=\"alert(1)\"/></rect></svg>", "<set>" },
            { "a filter with an image", $"<svg {ns} viewBox=\"0 0 10 10\"><filter id=\"f\"><feImage href=\"https://evil.example/x.png\"/></filter></svg>", "<filter>" },
            { "a url that leaves the file", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"url(https://evil.example/x.svg#p)\"/></svg>", "url(" },
            { "a quoted url that leaves the file", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"url('//evil.example/x')\"/></svg>", "url(" },
            { "a url in a style attribute", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" style=\"fill:url(https://evil.example/x)\"/></svg>", "url(" },
            { "a style property that loads", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" style=\"background-image:url(#a)\"/></svg>", "background-image" },
            { "a style with an import", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" style=\"fill:red;@import 'x'\"/></svg>", "@-rule" },
            { "a style with an escape", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" style=\"fill:\\75rl(x)\"/></svg>", "escape" },
            { "javascript in a value", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"javascript:alert(1)\"/></svg>", "javascript:" },
            { "javascript split by a tab", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"java&#9;script:alert(1)\"/></svg>", "javascript:" },
            { "javascript written as character references", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"&#106;avascript:alert(1)\"/></svg>", "javascript:" },
            { "a data address in a value", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"data:text/html,x\"/></svg>", "data:" },
            { "a data address in text", $"<svg {ns} viewBox=\"0 0 10 10\"><title>data:text/html,&lt;script&gt;</title></svg>", "data:" },
            { "a data address in a comment", $"<svg {ns} viewBox=\"0 0 10 10\"><!-- javascript:alert(1) --><rect width=\"1\" height=\"1\"/></svg>", "javascript:" },
            { "an external entity", $"<!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><svg {ns} viewBox=\"0 0 10 10\"><text>&xxe;</text></svg>", "DTD" },
            { "an external DTD", $"<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://evil.example/svg11.dtd\"><svg {ns} viewBox=\"0 0 10 10\"/>", "DTD" },
            { "a billion laughs entity bomb", $"<!DOCTYPE lolz [<!ENTITY lol \"lol\"><!ENTITY lol2 \"&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;\"><!ENTITY lol3 \"&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;\"><!ENTITY lol4 \"&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;\">]><svg {ns} viewBox=\"0 0 10 10\"><text>&lol4;</text></svg>", "DTD" },
            { "a style sheet instruction", $"<?xml version=\"1.0\"?><?xml-stylesheet href=\"https://evil.example/x.css\" type=\"text/css\"?><svg {ns} viewBox=\"0 0 10 10\"/>", "processing instruction" },
            { "an inkscape element", $"<svg {ns} xmlns:sodipodi=\"http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd\" viewBox=\"0 0 10 10\"><sodipodi:namedview id=\"n\"/></svg>", "sodipodi" },
            { "a foreign namespace on an attribute", $"<svg {ns} xmlns:x=\"http://evil.example/ns\" viewBox=\"0 0 10 10\"><rect width=\"1\" height=\"1\" x:y=\"z\"/></svg>", "not allowed" },
            { "a declaration of an unknown namespace", $"<svg {ns} xmlns:evil=\"http://evil.example/ns\" viewBox=\"0 0 10 10\"/>", "namespace declaration" },
            { "a huge declared width", $"<svg {ns} width=\"100000\" height=\"100000\" viewBox=\"0 0 10 10\"/>", "4096 px" },
            { "a huge height in inches", $"<svg {ns} width=\"10\" height=\"500in\" viewBox=\"0 0 10 10\"/>", "4096 px" },
            { "no size at all", $"<svg {ns}><rect width=\"1\" height=\"1\"/></svg>", "no viewBox" },
            { "a view box that is not four numbers", $"<svg {ns} viewBox=\"0 0 10\"/>", "four numbers" },
            { "an unknown attribute", $"<svg {ns} viewBox=\"0 0 10 10\"><rect width=\"1\" height=\"1\" filter=\"url(#x)\"/></svg>", "not on the list of attributes" },
            { "an unknown element", $"<svg {ns} viewBox=\"0 0 10 10\"><marquee/></svg>", "not on the list of elements" },
            { "not an svg at all", "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body onload=\"alert(1)\"></body></html>", "root element is not an SVG" },
            { "an svg without a namespace", "<svg viewBox=\"0 0 10 10\"><rect width=\"1\" height=\"1\"/></svg>", "root element is not an SVG" },
            { "markup that is not well formed", $"<svg {ns} viewBox=\"0 0 10 10\"><rect></svg>", "not a well-formed" },
            { "an unknown entity", $"<svg {ns} viewBox=\"0 0 10 10\"><text>&nbsp;</text></svg>", "not a well-formed" },
        };
        var nested = string.Concat(Enumerable.Repeat("<g>", 60)) + string.Concat(Enumerable.Repeat("</g>", 60));
        data.Add("nesting that is too deep", $"<svg {ns} viewBox=\"0 0 10 10\">{nested}</svg>", "too complex");
        var uses = string.Concat(Enumerable.Repeat("<use href=\"#a\"/>", 60));
        data.Add("a use bomb", $"<svg {ns} viewBox=\"0 0 10 10\"><defs><g id=\"a\"/></defs>{uses}</svg>", "<use>");
        return data;
    }

    [Theory]
    [MemberData(nameof(Hostile))]
    public void A_hostile_svg_is_refused_and_says_why(string name, string svg, string why)
    {
        var problem = Refusal(Svg(svg));
        Assert.True(problem.Contains(why, StringComparison.OrdinalIgnoreCase), $"{name}: the reason was: {problem}");
    }

    [Theory]
    [MemberData(nameof(Hostile))]
    public void A_hostile_svg_is_refused_for_every_slot_that_takes_an_svg(string name, string svg, string why)
    {
        _ = why;
        Assert.False(Svg(svg, AssetSlot.Favicon).Ok, name);
        Assert.False(Svg(svg, AssetSlot.Wordmark).Ok, name);
    }

    [Fact]
    public void A_utf8_byte_order_mark_and_leading_white_space_do_not_hide_an_svg_or_make_a_bad_one_pass()
    {
        var good = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'\n' }.Concat(AssetFixtures.Bytes(AssetFixtures.GoodSvg())).ToArray();
        Assert.True(Inspect(good, AssetSlot.Logo).Ok);
        var bad = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(AssetFixtures.Bytes(AssetFixtures.GoodSvg(inner: "<script>x</script>"))).ToArray();
        Assert.False(Inspect(bad, AssetSlot.Logo).Ok);
    }

    // ------------------------------------------------------------------------------------------------ names and families

    [Fact]
    public void A_stored_name_is_a_hash_and_one_of_five_extensions_never_a_path()
    {
        var hash = new string('a', 64);
        foreach (var ext in new[] { ".svg", ".png", ".ico", ".woff2", ".woff" })
        {
            Assert.True(BrandAssetValidator.IsStoredName(hash + ext), ext);
        }

        foreach (var bad in new[] { "", "logo.svg", hash, hash + ".exe", hash + ".svg/..", "../" + hash + ".svg", hash.ToUpperInvariant() + ".svg", hash[..63] + ".svg", hash + ".svg ", "a/" + hash + ".svg" })
        {
            Assert.False(BrandAssetValidator.IsStoredName(bad), bad);
        }

        Assert.False(BrandAssetValidator.IsStoredName(null));
    }

    [Fact]
    public void A_stored_name_is_the_sha256_of_the_content()
    {
        var content = AssetFixtures.Bytes("hello");
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824.svg", BrandAssetValidator.StoredFileName(content, ".svg"));
    }

    [Theory]
    [InlineData("Acme Sans", true)]
    [InlineData("Acme-Sans 2", true)]
    [InlineData("A", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("1Acme", false)]
    [InlineData("Acme;Sans", false)]
    [InlineData("Acme}Sans", false)]
    [InlineData("Acme\"Sans", false)]
    [InlineData("Acme'Sans", false)]
    [InlineData("Acme, Sans", false)]
    [InlineData("Acme_Sans", false)]
    [InlineData("Acme  Sans", false)]
    [InlineData("Acme Sans ", true)]
    [InlineData("Ünicode", false)]
    [InlineData("url(x)", false)]
    public void A_family_name_is_letters_digits_spaces_and_dashes(string family, bool ok)
        => Assert.Equal(ok, BrandAssetValidator.CheckFamily(family, []) is null);

    [Theory]
    [InlineData("serif")]
    [InlineData("Sans-Serif")]
    [InlineData("system-ui")]
    [InlineData("monospace")]
    [InlineData("ui-rounded")]
    [InlineData("inherit")]
    public void A_generic_family_is_not_a_name(string family) => Assert.Contains("generic family", BrandAssetValidator.CheckFamily(family, []));

    [Fact]
    public void A_family_the_toolkit_already_uses_by_name_is_refused_whatever_the_case()
    {
        var reserved = BrandAssetValidator.FamiliesIn("-apple-system, BlinkMacSystemFont, \"SF Pro Text\", 'Segoe UI', Roboto, sans-serif").ToList();
        Assert.Contains("SF Pro Text", reserved);
        Assert.Contains("Segoe UI", reserved);
        Assert.Contains("already name", BrandAssetValidator.CheckFamily("segoe ui", reserved));
        Assert.Contains("already name", BrandAssetValidator.CheckFamily("Roboto", reserved));
        Assert.Null(BrandAssetValidator.CheckFamily("Acme Sans", reserved));
    }

    [Fact]
    public void A_name_in_a_file_name_is_a_slug() => Assert.Equal("acme-sans-2", BrandAssetValidator.Slug("Acme  Sans-2 "));

    // ------------------------------------------------------------------------------------------------ the item

    private static AssetFile File(string slotExt = ".svg", char c = 'a') => new(new string(c, 64) + slotExt, 64, 64, 100);

    private static FontFace Face(string family = "Acme Sans", int weight = 400, string style = "normal", char c = 'b', string ext = ".woff2") => new(family, weight, style, new string(c, 64) + ext, 100);

    private static char Hex(int i) => "0123456789abcdef"[i % 16];

    private static IReadOnlyList<Problem> Validate(BrandAssets assets) => BrandAssetValidator.Validate(BrandAssetValidator.Normalize(assets), ["Roboto"]);

    [Fact]
    public void Nothing_at_all_is_valid_and_empty()
    {
        var empty = new BrandAssets();
        Assert.True(empty.IsEmpty);
        Assert.Empty(Validate(empty));
        Assert.True(BrandAssetValidator.IsSafe(empty));
        Assert.Empty(empty.References());
    }

    [Fact]
    public void A_logo_needs_alt_text_and_a_word_mark_too()
    {
        Assert.Contains(Validate(new BrandAssets(Logo: File())), p => p.Field == "LogoAlt" && p.Message.Contains("alt text"));
        Assert.Contains(Validate(new BrandAssets(Wordmark: File())), p => p.Field == "WordmarkAlt");
        Assert.Empty(Validate(new BrandAssets(Logo: File(), LogoAlt: "Acme", Wordmark: File(c: 'c'), WordmarkAlt: "Acme Anvils")));
        Assert.Contains(Validate(new BrandAssets(Logo: File(), LogoAlt: new string('x', 121))), p => p.Field == "LogoAlt");
        Assert.Contains(Validate(new BrandAssets(Logo: File(), LogoAlt: "line\u0001break")), p => p.Field == "LogoAlt");
    }

    [Fact]
    public void A_dark_variant_needs_its_light_one_and_an_alt_text_only_belongs_to_an_image()
    {
        // Normalize drops what has nothing to belong to; Validate on the raw item says it
        var onlyDark = new BrandAssets(LogoDark: File(c: 'd'), WordmarkDark: File(c: 'e'));
        Assert.Contains(BrandAssetValidator.Validate(onlyDark, []), p => p.Field == "LogoDark");
        Assert.Contains(BrandAssetValidator.Validate(onlyDark, []), p => p.Field == "WordmarkDark");
        var typed = BrandAssetValidator.Normalize(new BrandAssets(LogoAlt: "left over", WordmarkAlt: "left over"));
        Assert.Equal(string.Empty, typed.LogoAlt);
        Assert.Equal(string.Empty, typed.WordmarkAlt);
    }

    [Fact]
    public void A_file_must_be_a_stored_file_of_a_type_its_slot_takes()
    {
        Assert.Contains(Validate(new BrandAssets(Logo: new AssetFile("logo.svg"), LogoAlt: "x")), p => p.Field == "Logo");
        Assert.Contains(Validate(new BrandAssets(Logo: File(".ico"), LogoAlt: "x")), p => p.Field == "Logo" && p.Message.Contains("An SVG or a PNG", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Validate(new BrandAssets(AppIcon: File(".svg"))), p => p.Field == "AppIcon");
        Assert.Empty(Validate(new BrandAssets(Favicon: File(".ico"), AppIcon: File(".png", 'f'))));
    }

    [Fact]
    public void Fonts_are_checked_face_by_face()
    {
        Assert.Empty(Validate(new BrandAssets(Fonts: [Face(), Face(weight: 700, c: 'c'), Face(style: "italic", c: 'd', ext: ".woff")])));
        Assert.Contains(Validate(new BrandAssets(Fonts: [Face(weight: 450)])), p => p.Field == "Fonts" && p.Message.Contains("weight"));
        Assert.Contains(Validate(new BrandAssets(Fonts: [Face(weight: 1000)])), p => p.Message.Contains("weight"));
        Assert.Contains(Validate(new BrandAssets(Fonts: [Face(style: "oblique")])), p => p.Message.Contains("style"));
        Assert.Contains(Validate(new BrandAssets(Fonts: [Face(family: "serif")])), p => p.Message.Contains("generic"));
        Assert.Contains(Validate(new BrandAssets(Fonts: [Face(family: "Roboto")])), p => p.Message.Contains("already name"));
        Assert.Contains(Validate(new BrandAssets(Fonts: [Face(ext: ".png")])), p => p.Field == "Fonts");
    }

    [Fact]
    public void A_face_twice_is_refused_also_when_the_name_differs_in_case_or_spaces()
    {
        var problems = Validate(new BrandAssets(Fonts: [Face(), Face(family: "Acme  Sans", c: 'c')]));
        Assert.Contains(problems, p => p.Message.Contains("there twice"));
        Assert.Empty(Validate(new BrandAssets(Fonts: [Face(), Face(weight: 700, c: 'c')])));
    }

    [Fact]
    public void At_most_twelve_faces_and_three_families()
    {
        var thirteen = Enumerable.Range(1, 13).Select(i => Face(family: "Acme", weight: ((i - 1) % 9 + 1) * 100, style: i > 9 ? "italic" : "normal", c: Hex(i))).ToList();
        Assert.Contains(Validate(new BrandAssets(Fonts: thirteen)), p => p.Message.Contains("12 font faces"));
        Assert.Empty(Validate(new BrandAssets(Fonts: thirteen.Take(12).ToList())));
        var families = new[] { "One", "Two", "Three", "Four" }.Select((f, i) => Face(family: f, c: (char)('a' + i))).ToList();
        Assert.Contains(Validate(new BrandAssets(Fonts: families)), p => p.Message.Contains("3 font families"));
        Assert.Empty(Validate(new BrandAssets(Fonts: families.Take(3).ToList())));
    }

    [Fact]
    public void Every_file_the_brand_refers_to_has_a_public_name_that_follows_its_content()
    {
        var assets = new BrandAssets(Logo: File(), LogoDark: File(c: 'c'), LogoAlt: "Acme", Favicon: File(".png", 'd'), AppIcon: File(".png", 'e'), Fonts: [Face(c: 'f')]);
        var names = assets.References().Select(r => r.Name).ToList();
        Assert.Equal(["logo-aaaaaaaaaa.svg", "logo-dark-cccccccccc.svg", "favicon-dddddddddd.png", "app-icon-eeeeeeeeee.png", "fonts/acme-sans-400-normal-ffffffffff.woff2"], names);
        Assert.Equal("image/svg+xml", BrandAssets.ContentTypeOf(assets.Logo!.File));
        Assert.Equal("font/woff2", BrandAssets.ContentTypeOf(assets.Fonts![0].File));
        Assert.Equal("font/woff", BrandAssets.ContentTypeOf(new string('a', 64) + ".woff"));
        Assert.Equal("image/x-icon", BrandAssets.ContentTypeOf(new string('a', 64) + ".ico"));
        Assert.Null(BrandAssets.ContentTypeOf("x.exe"));
    }

    [Fact]
    public void An_item_read_from_the_volume_is_safe_only_when_it_is_of_the_shape_it_must_be()
    {
        Assert.True(BrandAssetValidator.IsSafe(new BrandAssets(Logo: File(), LogoAlt: "Acme", Fonts: [Face()])));
        Assert.False(BrandAssetValidator.IsSafe(null));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Logo: new AssetFile("../../etc/passwd"))));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Fonts: [Face(family: "A}body{x:y")])));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Fonts: [Face(family: "Acme\"; src: url(http://evil)")])));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Fonts: [Face(style: "normal; x: y")])));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Fonts: [Face(weight: 4000)])));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Favicon: File(".woff2"))));
        Assert.False(BrandAssetValidator.IsSafe(new BrandAssets(Fonts: Enumerable.Range(0, 13).Select(i => Face(weight: 100 + (i % 9 * 100), c: Hex(i))).ToList())));
    }

    // ------------------------------------------------------------------------------------------------ with the bytes

    private static (BrandAssets Assets, Dictionary<string, byte[]> Volume) Stored()
    {
        var png = AssetFixtures.Png(64, 64);
        var svg = AssetFixtures.Bytes(AssetFixtures.GoodSvg());
        var woff = AssetFixtures.Woff2();
        var volume = new Dictionary<string, byte[]>
        {
            [BrandAssetValidator.StoredFileName(png, ".png")] = png,
            [BrandAssetValidator.StoredFileName(svg, ".svg")] = svg,
            [BrandAssetValidator.StoredFileName(woff, ".woff2")] = woff,
        };
        var assets = new BrandAssets(
            Logo: new AssetFile(BrandAssetValidator.StoredFileName(svg, ".svg"), 64, 64, svg.Length), LogoAlt: "Acme",
            Favicon: new AssetFile(BrandAssetValidator.StoredFileName(png, ".png"), 64, 64, png.Length),
            Fonts: [new FontFace("Acme Sans", 400, "normal", BrandAssetValidator.StoredFileName(woff, ".woff2"), woff.Length)]);
        return (assets, volume);
    }

    [Fact]
    public void An_item_whose_files_are_all_there_and_valid_passes()
    {
        var (assets, volume) = Stored();
        Assert.Empty(BrandAssetValidator.ValidateFiles(assets, name => volume.GetValueOrDefault(name), []));
    }

    [Fact]
    public void A_file_that_is_missing_from_the_volume_is_named()
    {
        var (assets, volume) = Stored();
        volume.Remove(assets.Logo!.File);
        var problems = BrandAssetValidator.ValidateFiles(assets, name => volume.GetValueOrDefault(name), []);
        Assert.Contains(problems, p => p.Field == "Logo" && p.Message.Contains("missing from the volume"));
    }

    [Fact]
    public void A_file_whose_content_was_changed_no_longer_has_its_hash()
    {
        var (assets, volume) = Stored();
        volume[assets.Favicon!.File] = AssetFixtures.Png(32, 32);
        Assert.Contains(BrandAssetValidator.ValidateFiles(assets, name => volume.GetValueOrDefault(name), []), p => p.Field == "Favicon" && p.Message.Contains("hash"));
    }

    [Fact]
    public void A_file_that_is_not_valid_any_more_is_refused_by_the_same_rules_as_an_upload()
    {
        // a hostile SVG written to the volume by hand, under its own hash: the hash is right, the content is not allowed
        var (assets, volume) = Stored();
        var evil = AssetFixtures.Bytes(AssetFixtures.GoodSvg(inner: "<script>alert(1)</script>"));
        var name = BrandAssetValidator.StoredFileName(evil, ".svg");
        volume[name] = evil;
        var broken = assets with { Logo = new AssetFile(name, 64, 64, evil.Length) };
        Assert.Contains(BrandAssetValidator.ValidateFiles(broken, n => volume.GetValueOrDefault(n), []), p => p.Field == "Logo" && p.Message.Contains("<script>"));
    }

    [Fact]
    public void The_files_together_are_held_to_a_budget()
    {
        var volume = new Dictionary<string, byte[]>();
        string Keep(byte[] bytes, string ext)
        {
            var name = BrandAssetValidator.StoredFileName(bytes, ext);
            volume[name] = bytes;
            return name;
        }

        var faces = Enumerable.Range(0, 9).Select(i => new FontFace("Acme", (i + 1) * 100, "normal", Keep(AssetFixtures.Woff2(extra: 399 * 1024 + i), ".woff2"))).ToList();
        AssetFile Picture(int size, int seed) => new(Keep(AssetFixtures.PngWith(size, size, 490 * 1024, seed), ".png"));
        var fonts = new BrandAssets(Fonts: faces);
        // 9 fonts of 399 KB and four pictures of 490 KB are 5.4 MB: within the 6 MB
        var within = fonts with { Logo = Picture(256, 1), LogoDark = Picture(256, 2), LogoAlt = "Acme", Favicon = Picture(256, 3), Wordmark = Picture(256, 4), WordmarkAlt = "Acme" };
        Assert.DoesNotContain(BrandAssetValidator.ValidateFiles(within, n => volume.GetValueOrDefault(n), []), p => p.Field == "Budget");
        // the app icon and a dark word mark make it 6.4 MB
        var over = within with { AppIcon = Picture(512, 5), WordmarkDark = Picture(256, 6) };
        Assert.Contains(BrandAssetValidator.ValidateFiles(over, n => volume.GetValueOrDefault(n), []), p => p.Field == "Budget" && p.Message.Contains("6144 KB"));
        // the same file in two slots weighs once
        var shared = within with { LogoDark = within.Logo, AppIcon = null };
        Assert.DoesNotContain(BrandAssetValidator.ValidateFiles(shared, n => volume.GetValueOrDefault(n), []), p => p.Field == "Budget");
    }
}
