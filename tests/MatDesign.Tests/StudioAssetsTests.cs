using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>IDENT-08 in the Studio: the page, uploads through the real form, serving, the overlay, releases and the archive, the shell with and without assets, history and restore, checks.</summary>
public sealed partial class StudioAssetsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioAssetsTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex Token();

    [GeneratedRegex(@"\?v=[0-9A-Za-z_-]+")]
    private static partial Regex Version();

    [GeneratedRegex(@"(name=""__RequestVerificationToken""[^>]*value="")[^""]+")]
    private static partial Regex AntiForgery();

    /// <summary>A page as it is, without what differs on every request or with every file (the version of a file in its address, the anti-forgery token of the sign-out form).</summary>
    private static string Norm(string html) => AntiForgery().Replace(Version().Replace(html, "?v="), "$1");

    private ToolkitFiles Toolkit => _studio.Factory.Services.GetRequiredService<ToolkitFiles>();

    private AssetFiles Files => _studio.Factory.Services.GetRequiredService<AssetFiles>();

    private string Volume => Path.Combine(_studio.DataDirectory, "assets");

    private static readonly byte[] GoodSvg = AssetFixtures.Bytes(AssetFixtures.GoodSvg());

    private async Task<HttpResponseMessage> Post(string handler, Dictionary<string, string> fields, (byte[] Bytes, string FileName, string Type)? file = null)
    {
        var token = Token().Match(await _studio.Html("/Studio/Brand/Assets")).Groups[1].Value;
        var form = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };
        foreach (var (name, value) in fields)
        {
            form.Add(new StringContent(value), name);
        }

        if (file is { } f)
        {
            var content = new ByteArrayContent(f.Bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(f.Type);
            form.Add(content, "file", f.FileName);
        }

        return await _studio.Client.PostAsync("/Studio/Brand/Assets?handler=" + handler, form);
    }

    private async Task<HttpResponseMessage> Upload(string slot, byte[] bytes, string fileName, string type, string? alt = null)
    {
        var fields = new Dictionary<string, string> { ["slot"] = slot };
        if (alt is not null)
        {
            fields["alt"] = alt;
        }

        return await Post("Save", fields, (bytes, fileName, type));
    }

    private async Task UploadOk(string slot, byte[] bytes, string fileName, string type, string? alt = null)
    {
        var response = await Upload(slot, bytes, fileName, type, alt);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"{slot}: {response.StatusCode} {Regex.Match(await response.Content.ReadAsStringAsync(), "mat-error[^>]*>([^<]*)").Groups[1].Value}");
    }

    private async Task AddFontOk(string family, int weight, string style, byte[] bytes, string fileName = "font.woff2")
    {
        var response = await Post("AddFont", new() { ["family"] = family, ["weight"] = weight.ToString(), ["style"] = style }, (bytes, fileName, "font/woff2"));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"{family}: {response.StatusCode} {Regex.Match(await response.Content.ReadAsStringAsync(), "mat-error[^>]*>([^<]*)").Groups[1].Value}");
    }

    private static async Task<string> Text(HttpResponseMessage response) => System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    // ------------------------------------------------------------------------------------------------ the page

    [Fact]
    public async Task A_visitor_is_led_to_the_sign_in_but_the_files_are_public_when_there_are_any()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/Brand/Assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/History?kind=assets&id=assets")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync("/ui/brand/logo-aaaaaaaaaa.svg")).StatusCode);
    }

    [Fact]
    public async Task The_page_shows_a_card_for_every_slot_and_follows_the_shell_rules()
    {
        await _studio.SignIn();
        var html = await _studio.Get("/Studio/Brand/Assets");
        Assert.Single(Regex.Matches(html, "<h1\\b"));
        foreach (var slot in new[] { "logo", "wordmark", "favicon", "app-icon" })
        {
            Assert.Contains($"id=\"asset-{slot}\"", html);
            Assert.Contains($"id=\"file-{slot}\"", html);
        }

        Assert.Contains("enctype=\"multipart/form-data\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("id=\"alt-logo\"", html);
        Assert.Contains("id=\"font-family\"", html);
        Assert.Contains("data-upload", html);
        Assert.Contains("/js/brand-assets.js", html);
        Assert.DoesNotContain(" style=", html);
        Assert.Contains("href=\"/Studio/Brand\"", html);
        Assert.Contains("<a class=\"mat-nav__item is-active\" href=\"/Studio/Brand/Assets\" aria-current=\"page\">", html);
        // a dark variant is offered once there is a logo to have one
        Assert.DoesNotContain("id=\"asset-logo-dark\"", html);
        Assert.Contains("href=\"/Studio/Brand/Assets\"", await _studio.Get("/Studio/Brand"));
        Assert.Contains("href=\"/Studio/Brand/Assets\"", await _studio.Get("/Studio"));
    }

    [Fact]
    public async Task The_page_sets_its_own_request_size_limit()
    {
        var attributes = typeof(MatDesign.Web.Pages.StudioBrandAssets.AssetsModel).GetCustomAttributes(inherit: true).Select(a => a.GetType().Name).ToList();
        Assert.Contains("RequestSizeLimitAttribute", attributes);
        Assert.Contains("RequestFormLimitsAttribute", attributes);
        Assert.Equal(1024 * 1024, MatDesign.Web.Pages.StudioBrandAssets.AssetsModel.MaxRequestBytes);
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------ upload, refusal, serving

    [Fact]
    public async Task A_logo_is_uploaded_through_the_form_stored_under_its_hash_and_served_with_the_right_headers()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "acme-logo.svg", "image/svg+xml", "Acme Anvils");

        var assets = _studio.Design.Assets!;
        Assert.Equal(BrandAssetValidator.Sha256(GoodSvg) + ".svg", assets.Logo!.File);
        Assert.Equal("Acme Anvils", assets.LogoAlt);
        Assert.Equal((64, 64), (assets.Logo.Width, assets.Logo.Height));
        Assert.True(File.Exists(Path.Combine(Volume, assets.Logo.File)));
        Assert.Equal(GoodSvg, File.ReadAllBytes(Path.Combine(Volume, assets.Logo.File)));
        // the item refers to the file by its name and holds no bytes
        var revision = _studio.Store.Current("assets", "assets")!;
        Assert.Equal(assets.Logo.File, revision.Data.GetProperty("logo").GetProperty("file").GetString());
        Assert.StartsWith("Created", revision.Note);

        var name = "logo-" + assets.Logo.File[..10] + ".svg";
        var served = await _studio.Client.GetAsync("/ui/brand/" + name);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/svg+xml", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal(GoodSvg, await served.Content.ReadAsByteArrayAsync());
        Assert.Equal("default-src 'none'; style-src 'none'", served.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("public, max-age=31536000, immutable", served.Headers.CacheControl!.ToString());
        Assert.Equal("*", served.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal($"\"{BrandAssetValidator.Sha256(GoodSvg)}\"", served.Headers.ETag!.Tag);

        // a second request with the tag is answered without the file
        var again = new HttpRequestMessage(HttpMethod.Get, "/ui/brand/" + name);
        again.Headers.IfNoneMatch.Add(served.Headers.ETag);
        Assert.Equal(HttpStatusCode.NotModified, (await _studio.Client.SendAsync(again)).StatusCode);
    }

    [Fact]
    public async Task A_png_a_font_and_an_icon_are_served_with_their_own_content_types_and_no_sniffing()
    {
        await _studio.SignIn();
        var png = AssetFixtures.Png(32, 32);
        var ico = AssetFixtures.Ico();
        await UploadOk("favicon", png, "favicon.png", "image/png");
        await UploadOk("app-icon", AssetFixtures.Png(512, 512), "icon.png", "image/png");
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());

        var assets = _studio.Design.Assets!;
        var favicon = await _studio.Client.GetAsync("/ui/brand/favicon-" + assets.Favicon!.File[..10] + ".png");
        Assert.Equal("image/png", favicon.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", favicon.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.False(favicon.Headers.Contains("Content-Security-Policy"));
        var font = await _studio.Client.GetAsync("/ui/brand/" + BrandAssets.PublicName(assets.Faces[0]));
        Assert.Equal("font/woff2", font.Content.Headers.ContentType!.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", font.Headers.CacheControl!.ToString());
        await UploadOk("favicon", ico, "favicon.ico", "image/x-icon");
        Assert.Equal("image/x-icon", (await _studio.Client.GetAsync("/ui/brand/favicon-" + _studio.Design.Assets!.Favicon!.File[..10] + ".ico")).Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task A_logo_needs_its_alt_text_and_nothing_is_saved_without_it()
    {
        await _studio.SignIn();
        var response = await Upload("logo", GoodSvg, "logo.svg", "image/svg+xml", alt: " ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Write the alt text", await Text(response));
        Assert.Null(_studio.Store.Current("assets", "assets"));
        Assert.Null(_studio.Design.Assets);
    }

    [Fact]
    public async Task A_program_with_a_picture_name_is_refused_and_nothing_reaches_the_volume()
    {
        await _studio.SignIn();
        var exe = new byte[3000];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        var response = await Upload("logo", exe, "logo.png", "image/png", "Acme");
        var html = await Text(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not a file a brand may use", html);
        Assert.Contains("The file was not saved.", html);
        Assert.Null(_studio.Store.Current("assets", "assets"));
        Assert.Empty(Directory.GetFiles(Volume));
    }

    [Fact]
    public async Task A_hostile_svg_is_refused_with_the_reason_and_nothing_is_saved_or_stored()
    {
        await _studio.SignIn();
        foreach (var (svg, why) in new[]
        {
            ($"<svg xmlns=\"{AssetFixtures.Svg}\" viewBox=\"0 0 10 10\" onload=\"alert(1)\"/>", "event handler"),
            ($"<svg xmlns=\"{AssetFixtures.Svg}\" viewBox=\"0 0 10 10\"><script>alert(1)</script></svg>", "script"),
            ($"<!DOCTYPE svg [<!ENTITY x \"y\">]><svg xmlns=\"{AssetFixtures.Svg}\" viewBox=\"0 0 10 10\"/>", "DTD"),
        })
        {
            var response = await Upload("logo", AssetFixtures.Bytes(svg), "logo.svg", "image/svg+xml", "Acme");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(why, await Text(response));
        }

        Assert.Null(_studio.Store.Current("assets", "assets"));
        Assert.Empty(Directory.GetFiles(Volume));
        Assert.Null(_studio.Design.Assets);
    }

    [Fact]
    public async Task The_name_of_the_file_and_the_type_the_browser_sent_do_not_decide_anything()
    {
        await _studio.SignIn();
        // a good SVG with the name of a script and the type of an executable is an SVG
        await UploadOk("logo", GoodSvg, "payload.exe", "application/x-msdownload", "Acme");
        Assert.EndsWith(".svg", _studio.Design.Assets!.Logo!.File);
        // a font in the slot of a logo is refused whatever it is called
        var response = await Upload("logo", AssetFixtures.Woff2(), "logo.svg", "image/svg+xml", "Acme");
        Assert.Contains("a logo must be an SVG or a PNG", await Text(response));
    }

    [Fact]
    public async Task A_request_over_the_limit_of_the_page_is_not_taken()
    {
        await _studio.SignIn();
        var big = new byte[2 * 1024 * 1024];
        HttpStatusCode? status = null;
        try
        {
            status = (await Upload("favicon", big, "favicon.ico", "image/x-icon")).StatusCode;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException)
        {
            // the in-memory server may stop the body with an exception instead of a status
        }

        // whatever the server says (413, or the page with a refusal), the file is not taken
        Assert.NotEqual(HttpStatusCode.Redirect, status);
        Assert.Null(_studio.Design.Assets?.Favicon);
        Assert.Empty(Directory.GetFiles(Volume));
    }

    [Fact]
    public async Task An_address_that_is_not_the_name_of_a_current_file_is_not_found()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        var stored = _studio.Design.Assets!.Logo!.File;
        foreach (var path in new[] { "/ui/brand/logo.svg", "/ui/brand/" + stored, "/ui/brand/fonts/..%2flogo-" + stored[..10] + ".svg", "/ui/brand/fonts/", "/ui/brand/logo-0000000000.svg", "/ui/brand/logo-" + stored[..10] + ".png" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task A_file_that_was_changed_on_the_volume_is_not_served_any_more()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        var file = _studio.Design.Assets!.Logo!.File;
        var name = "/ui/brand/logo-" + file[..10] + ".svg";
        Assert.Equal(HttpStatusCode.OK, (await _studio.Client.GetAsync(name)).StatusCode);

        File.WriteAllText(Path.Combine(Volume, file), "<svg xmlns=\"" + AssetFixtures.Svg + "\" onload=\"alert(1)\"/>");
        _studio.Design.Reload();
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync(name)).StatusCode);
    }

    // ------------------------------------------------------------------------------------------------ the shell, with and without

    [Fact]
    public async Task Without_assets_the_shell_is_the_one_the_site_always_had()
    {
        await _studio.SignIn();
        var html = await _studio.Get("/Rules");
        Assert.Contains("<link rel=\"icon\" type=\"image/svg+xml\" href=\"/favicon.svg\" />\n", html);
        Assert.DoesNotContain("apple-touch-icon", html);
        Assert.DoesNotContain("mat-brandmark", html);
        Assert.Contains("class=\"mat-logo mat-logo--md\"", html);
        Assert.Contains("<span class=\"mat-nav__name\">MatDesign</span>", html);
        Assert.Contains("aria-label=\"MatDesign, start page\">\n", html);
        Assert.DoesNotContain("/ui/brand/", html);
    }

    [Fact]
    public async Task Fonts_only_change_the_stylesheet_and_leave_every_byte_of_the_page_as_it_was()
    {
        await _studio.SignIn();
        var pages = new[] { "/Rules", "/Foundations/Type", "/Identity", "/Download" };
        var before = new List<string>();
        foreach (var page in pages)
        {
            before.Add(Norm(await _studio.Get(page)));
        }

        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        await _studio.Get("/Studio");
        for (var i = 0; i < pages.Length; i++)
        {
            var after = Norm(await _studio.Get(pages[i]));
            if (pages[i] == "/Download")
            {
                // the page of the downloads lists the file of the font, nothing else changes
                Assert.Contains("brand/fonts/acme-sans-400-normal-", after);
                continue;
            }

            Assert.Equal(before[i], after);
        }
    }

    [Fact]
    public async Task A_logo_and_a_favicon_replace_the_glyph_and_the_favicon_link_and_nothing_else()
    {
        await _studio.SignIn();
        var before = Norm(await _studio.Get("/Rules"));
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme Anvils");
        await UploadOk("favicon", AssetFixtures.Png(32, 32), "favicon.png", "image/png");
        await UploadOk("app-icon", AssetFixtures.Png(512, 512), "icon.png", "image/png");
        await _studio.Get("/Studio");
        var html = await _studio.Get("/Rules");
        var assets = _studio.Design.Assets!;

        Assert.Contains($"<img class=\"mat-brandmark\" src=\"/ui/brand/logo-{assets.Logo!.File[..10]}.svg\" alt=\"Acme Anvils\" width=\"38\" height=\"38\" />", html);
        Assert.DoesNotContain("class=\"mat-logo mat-logo--md\"", html);
        Assert.Contains($"<link rel=\"icon\" type=\"image/png\" href=\"/ui/brand/favicon-{assets.Favicon!.File[..10]}.png\" />", html);
        Assert.Contains($"<link rel=\"apple-touch-icon\" href=\"/ui/brand/app-icon-{assets.AppIcon!.File[..10]}.png\" />", html);
        Assert.DoesNotContain("href=\"/favicon.svg\"", html);
        // the name next to the logo stays, and the rest of the page is what it was
        Assert.Contains("<span class=\"mat-nav__name\">MatDesign</span>", html);
        // take out the three things that differ, and the page is the page it was
        var after = Regex.Replace(Norm(html), "<img class=\"mat-brandmark\"[^>]*>", "{logo}");
        after = Regex.Replace(after, "<link rel=\"icon\" type=\"image/png\"[^>]*>\\n    <link rel=\"apple-touch-icon\"[^>]*>", "{icons}");
        var expected = Regex.Replace(before, "<span class=\"mat-logo mat-logo--md\".*?</svg></span>", "{logo}", RegexOptions.Singleline);
        expected = expected.Replace("<link rel=\"icon\" type=\"image/svg+xml\" href=\"/favicon.svg\" />", "{icons}");
        Assert.Contains("{logo}", after);
        Assert.Contains("{icons}", after);
        Assert.Equal(expected, after);
    }

    [Fact]
    public async Task A_dark_variant_is_a_second_image_and_the_theme_shows_one_without_inline_style()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        Assert.DoesNotContain("mat-brandmark--dark", await _studio.Get("/Rules"));
        var dark = AssetFixtures.Bytes(AssetFixtures.GoodSvg(inner: "<rect width=\"4\" height=\"4\" fill=\"#000000\"/>"));
        await UploadOk("logo-dark", dark, "logo-dark.svg", "image/svg+xml");
        var html = await _studio.Get("/Rules");
        var assets = _studio.Design.Assets!;
        Assert.Contains($"class=\"mat-brandmark mat-brandmark--light\" src=\"/ui/brand/logo-{assets.Logo!.File[..10]}.svg\" alt=\"Acme\"", html);
        Assert.Contains($"class=\"mat-brandmark mat-brandmark--dark\" src=\"/ui/brand/logo-dark-{assets.LogoDark!.File[..10]}.svg\" alt=\"Acme\"", html);
        Assert.DoesNotContain(" style=", html);
        // the stylesheet that shows one of the two: explicit theme, and the system under prefers-color-scheme
        var css = await _studio.Get("/ui/matui.css");
        Assert.Contains(":root[data-theme=\"dark\"] .mat-brandmark--light { display: none; }", css);
        Assert.Contains(":root:not([data-theme=\"light\"]):not([data-theme=\"dark\"]) .mat-brandmark--dark { display: block; }", css);
        // a logo that is removed takes its dark variant with it
        await Post("Remove", new() { ["slot"] = "logo" });
        Assert.Null(_studio.Design.Assets!.LogoDark);
    }

    [Fact]
    public async Task A_wide_logo_carries_the_name_and_a_word_mark_replaces_the_name()
    {
        await _studio.SignIn();
        await UploadOk("logo", AssetFixtures.Bytes(AssetFixtures.WideSvg()), "wide.svg", "image/svg+xml", "Acme Anvils");
        var html = await _studio.Get("/Rules");
        Assert.Contains("class=\"mat-nav__brand-link--logo-only\"", html);
        Assert.Contains("width=\"228\" height=\"38\"", html);
        await UploadOk("wordmark", AssetFixtures.Bytes(AssetFixtures.WideSvg()), "word.svg", "image/svg+xml", "Acme");
        html = await _studio.Get("/Rules");
        Assert.Contains("<span class=\"mat-nav__name\"><img class=\"mat-brandmark mat-brandmark--word\"", html);
        Assert.Contains("alt=\"Acme\"", html);
    }

    [Fact]
    public async Task The_sample_apps_keep_their_own_logo_and_favicon()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        await UploadOk("favicon", AssetFixtures.Png(32, 32), "favicon.png", "image/png");
        var preview = await _studio.Get("/Preview?app=matmail");
        Assert.DoesNotContain("mat-brandmark", preview);
        Assert.Contains("href=\"/favicon.svg\"", preview);
    }

    // ------------------------------------------------------------------------------------------------ fonts and the stylesheet

    [Fact]
    public async Task A_font_is_declared_in_the_bundle_and_in_the_part_relative_to_where_each_is_served()
    {
        await _studio.SignIn();
        var hashBefore = Toolkit.Hash;
        var bundleBefore = await _studio.Get("/ui/matui.css");
        Assert.DoesNotContain("@font-face", bundleBefore);

        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        await AddFontOk("Acme Sans", 700, "italic", AssetFixtures.Woff2(extra: 8), "bold.woff");
        var faces = _studio.Design.Assets!.Faces;
        var regular = BrandAssets.PublicName(faces[0]);
        var css = await _studio.Get("/ui/matui.css");
        Assert.Contains($"@font-face {{ font-family: \"Acme Sans\"; font-style: normal; font-weight: 400; font-display: swap; src: url(\"brand/{regular}\") format(\"woff2\"); }}", css);
        Assert.Contains("font-style: italic; font-weight: 700; font-display: swap; src: url(\"brand/fonts/acme-sans-700-italic-", css);
        Assert.NotEqual(hashBefore, Toolkit.Hash);
        Assert.Equal(10, ToolkitFiles.CssParts.Count);
        // the file the stylesheet points to is there, relative to the stylesheet
        Assert.Equal(HttpStatusCode.OK, (await _studio.Client.GetAsync("/ui/brand/" + regular)).StatusCode);
        // an app that loads the parts one by one has them a level up
        var part = await _studio.Get("/ui/css/matui.apps.css");
        Assert.Contains($"src: url(\"../brand/{regular}\") format(\"woff2\")", part);
        Assert.DoesNotContain("url(\"brand/", part);
        Assert.Equal(part, Toolkit.Css("matui.apps.css"));
    }

    [Fact]
    public async Task A_logo_alone_leaves_the_stylesheet_and_its_hash_as_they_were()
    {
        await _studio.SignIn();
        var hash = Toolkit.Hash;
        var css = await _studio.Get("/ui/matui.css");
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        await UploadOk("favicon", AssetFixtures.Png(32, 32), "favicon.png", "image/png");
        Assert.Equal(hash, Toolkit.Hash);
        Assert.Equal(css, await _studio.Get("/ui/matui.css"));
        Assert.Equal(TestPaths.ReadToolkit("css/matui.apps.css"), Toolkit.Css("matui.apps.css"));
    }

    [Fact]
    public void The_overlay_is_empty_without_fonts_and_for_what_is_not_safe()
    {
        Assert.Equal(string.Empty, ToolkitFiles.AssetsOverlay(null));
        Assert.Equal(string.Empty, ToolkitFiles.AssetsOverlay(new BrandAssets()));
        Assert.Equal(string.Empty, ToolkitFiles.AssetsOverlay(new BrandAssets(Logo: new AssetFile(new string('a', 64) + ".svg"), LogoAlt: "Acme")));
        var hostile = new BrandAssets(Fonts: [new FontFace("Acme\"; } body { x: y", 400, "normal", new string('a', 64) + ".woff2")]);
        Assert.Equal(string.Empty, ToolkitFiles.AssetsOverlay(hostile));
        Assert.Equal(string.Empty, ToolkitFiles.AssetsOverlay(new BrandAssets(Fonts: [new FontFace("Acme", 400, "normal", "../../etc/passwd")])));
    }

    [Fact]
    public void The_overlay_writes_one_rule_for_each_face_with_swap_and_the_format()
    {
        var hash = new string('a', 64);
        var css = ToolkitFiles.AssetsOverlay(new BrandAssets(Fonts: [new FontFace("Acme Sans", 400, "normal", hash + ".woff2"), new FontFace("Acme Sans", 700, "italic", new string('b', 64) + ".woff")]));
        Assert.Contains("@font-face { font-family: \"Acme Sans\"; font-style: normal; font-weight: 400; font-display: swap; src: url(\"brand/fonts/acme-sans-400-normal-aaaaaaaaaa.woff2\") format(\"woff2\"); }", css);
        Assert.Contains("@font-face { font-family: \"Acme Sans\"; font-style: italic; font-weight: 700; font-display: swap; src: url(\"brand/fonts/acme-sans-700-italic-bbbbbbbbbb.woff\") format(\"woff\"); }", css);
        Assert.Equal(2, Regex.Matches(css, "@font-face").Count);
        Assert.DoesNotContain("http", css);
        Assert.DoesNotContain("//", css.Replace("/*", string.Empty).Replace("*/", string.Empty));
    }

    [Fact]
    public async Task A_family_of_the_brand_is_accepted_in_a_font_stack_of_the_brand_editor()
    {
        await _studio.SignIn();
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        var html = await _studio.Get("/Studio/Brand/Assets");
        Assert.Contains("&quot;Acme Sans&quot;, system-ui, sans-serif", html);
        var token = Toolkit.EditableTokens.Single(t => t.Name == "--mat-font");
        Assert.Null(BrandValidator.CheckValue(token, "\"Acme Sans\", system-ui, sans-serif"));
        Assert.Null(BrandValidator.CheckValue(Toolkit.EditableTokens.Single(t => t.Name == "--mat-mono"), "\"Acme Mono\", ui-monospace, monospace"));
        // and a brand that sets it writes it after the @font-face
        var save = await _studio.Post("/Studio/Brand", "/Studio/Brand", new List<KeyValuePair<string, string>>
        {
            new("Input.Company", "Acme"), new("Input.ShortName", "Acme"), new("Input.Tagline", ""), new("light:--mat-font", "\"Acme Sans\", system-ui, sans-serif"),
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        var css = await _studio.Get("/ui/matui.css");
        Assert.Contains("--mat-font: \"Acme Sans\", system-ui, sans-serif;", css);
        Assert.True(css.IndexOf("@font-face", StringComparison.Ordinal) > css.IndexOf("Brand set in the Studio", StringComparison.Ordinal) || css.Contains("@font-face", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Roboto", "already name")]
    [InlineData("Segoe UI", "already name")]
    [InlineData("sans-serif", "generic family")]
    [InlineData("Acme;Sans", "letters, digits, spaces and dashes")]
    [InlineData("Acme{}", "letters, digits, spaces and dashes")]
    [InlineData("", "Name the family")]
    public async Task A_family_name_that_is_not_allowed_is_refused_at_the_field(string family, string why)
    {
        await _studio.SignIn();
        var response = await Post("AddFont", new() { ["family"] = family, ["weight"] = "400", ["style"] = "normal" }, (AssetFixtures.Woff2(), "f.woff2", "font/woff2"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(why, await Text(response));
        Assert.Null(_studio.Store.Current("assets", "assets"));
    }

    [Fact]
    public async Task A_face_twice_a_bad_weight_a_bad_file_and_too_many_faces_or_families_are_refused()
    {
        await _studio.SignIn();
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        var twice = await Post("AddFont", new() { ["family"] = "acme sans", ["weight"] = "400", ["style"] = "normal" }, (AssetFixtures.Woff2(extra: 4), "f.woff2", "font/woff2"));
        Assert.Contains("there twice", await Text(twice));
        var weight = await Post("AddFont", new() { ["family"] = "Acme Sans", ["weight"] = "450", ["style"] = "normal" }, (AssetFixtures.Woff2(extra: 5), "f.woff2", "font/woff2"));
        Assert.Contains("the weight is 100", await Text(weight));
        var notFont = await Post("AddFont", new() { ["family"] = "Acme Sans", ["weight"] = "500", ["style"] = "normal" }, (AssetFixtures.Png(16, 16), "f.woff2", "font/woff2"));
        Assert.Contains("a font must be", await Text(notFont));
        var none = await Post("AddFont", new() { ["family"] = "Acme Sans", ["weight"] = "500", ["style"] = "normal" });
        Assert.Contains("Choose the font file", await Text(none));
        Assert.Single(_studio.Design.Assets!.Faces);

        await AddFontOk("Acme Serif", 400, "normal", AssetFixtures.Woff2(extra: 6));
        await AddFontOk("Acme Mono", 400, "normal", AssetFixtures.Woff2(extra: 7));
        var fourth = await Post("AddFont", new() { ["family"] = "Acme Slab", ["weight"] = "400", ["style"] = "normal" }, (AssetFixtures.Woff2(extra: 8), "f.woff2", "font/woff2"));
        Assert.Contains("3 font families at most", await Text(fourth));
        for (var i = 1; i <= 9; i++)
        {
            if (i == 4)
            {
                continue;
            }

            await AddFontOk("Acme Sans", i * 100, "normal", AssetFixtures.Woff2(extra: 20 + i));
        }

        Assert.Equal(11, _studio.Design.Assets!.Faces.Count);
        await AddFontOk("Acme Sans", 100, "italic", AssetFixtures.Woff2(extra: 40));
        Assert.Equal(12, _studio.Design.Assets.Faces.Count);
        var thirteenth = await Post("AddFont", new() { ["family"] = "Acme Sans", ["weight"] = "200", ["style"] = "italic" }, (AssetFixtures.Woff2(extra: 41), "f.woff2", "font/woff2"));
        Assert.Contains("12 font faces at most", await Text(thirteenth));
    }

    [Fact]
    public async Task A_face_is_removed_and_its_file_stays_on_the_volume()
    {
        await _studio.SignIn();
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        var file = _studio.Design.Assets!.Faces[0].File;
        var response = await Post("RemoveFont", new() { ["family"] = "Acme Sans", ["weight"] = "400", ["style"] = "normal" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(_studio.Design.Assets!.Faces);
        Assert.True(File.Exists(Path.Combine(Volume, file)));
        Assert.DoesNotContain("@font-face", await _studio.Get("/ui/matui.css"));
    }

    // ------------------------------------------------------------------------------------------------ history, restore

    [Fact]
    public async Task Every_change_is_a_revision_and_a_file_that_is_replaced_stays_on_the_volume()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        var second = AssetFixtures.Bytes(AssetFixtures.GoodSvg(inner: "<rect width=\"2\" height=\"2\"/>"));
        await UploadOk("logo", second, "logo2.svg", "image/svg+xml", "Acme");
        await UploadOk("logo", second, "logo2.svg", "image/svg+xml", "Acme Anvils");

        Assert.Equal(3, _studio.Store.History("assets", "assets").Count);
        Assert.Equal(2, Directory.GetFiles(Volume).Length);
        Assert.True(File.Exists(Path.Combine(Volume, BrandAssetValidator.Sha256(GoodSvg) + ".svg")));
        Assert.Equal("Acme Anvils", _studio.Design.Assets!.LogoAlt);
        // saving what is already there is no revision
        await UploadOk("logo", second, "logo2.svg", "image/svg+xml", "Acme Anvils");
        Assert.Equal(3, _studio.Store.History("assets", "assets").Count);
    }

    [Fact]
    public async Task The_history_page_names_the_kind_compares_revisions_and_restores_one_as_a_new_revision()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        await UploadOk("favicon", AssetFixtures.Png(32, 32), "favicon.png", "image/png");
        await Post("Remove", new() { ["slot"] = "logo" });

        var history = await _studio.Get("/Studio/History?kind=assets&id=assets");
        Assert.Contains("History of Brand assets", history);
        Assert.Contains("<code>logo.file</code>", history);
        Assert.Contains("href=\"/Studio/Brand/Assets\"", history);
        var activity = await _studio.Get("/Studio/Activity?kind=assets");
        Assert.Contains("Brand assets", activity);
        Assert.Contains("href=\"/Studio/History?kind=assets&amp;id=assets\"", activity);

        var restored = await _studio.Post("/Studio/Brand/Assets", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "assets", ["id"] = "assets", ["revision"] = "2" });
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        Assert.Equal(4, _studio.Store.History("assets", "assets").Count);
        Assert.NotNull(_studio.Design.Assets!.Logo);
        Assert.NotNull(_studio.Design.Assets.Favicon);
        Assert.StartsWith("Restored", _studio.Store.Current("assets", "assets")!.Note);
        Assert.Equal(HttpStatusCode.OK, (await _studio.Client.GetAsync("/ui/brand/logo-" + _studio.Design.Assets.Logo!.File[..10] + ".svg")).StatusCode);
    }

    [Fact]
    public async Task A_revision_whose_file_is_missing_from_the_volume_is_not_restored()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        await Post("Remove", new() { ["slot"] = "logo" });
        File.Delete(Path.Combine(Volume, BrandAssetValidator.Sha256(GoodSvg) + ".svg"));

        var refused = await _studio.Post("/Studio/Brand/Assets", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "assets", ["id"] = "assets", ["revision"] = "1" });
        var html = await Text(refused);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("cannot be restored as it is today", html);
        Assert.Contains("missing from the volume", html);
        Assert.Equal(2, _studio.Store.History("assets", "assets").Count);
        Assert.Null(_studio.Design.Assets!.Logo);
    }

    [Fact]
    public async Task Everything_can_be_taken_away_and_the_site_is_what_it_was()
    {
        await _studio.SignIn();
        var before = Norm(await _studio.Get("/Rules"));
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        await UploadOk("favicon", AssetFixtures.Png(32, 32), "favicon.png", "image/png");
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        Assert.NotEqual(before, Norm(await _studio.Get("/Rules")));

        Assert.Equal(HttpStatusCode.Redirect, (await Post("Reset", [])).StatusCode);
        await _studio.Get("/Studio");
        Assert.Null(_studio.Design.Assets);
        Assert.Equal(before, Norm(await _studio.Get("/Rules")));
        Assert.DoesNotContain("@font-face", await _studio.Get("/ui/matui.css"));
        Assert.Empty(Files.Files());
        Assert.Equal(3, Directory.GetFiles(Volume).Length);
    }

    // ------------------------------------------------------------------------------------------------ checks and releases

    [Fact]
    public async Task The_check_passes_for_good_assets_and_finds_a_file_that_is_missing_or_changed()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        Assert.True(_studio.Checks.Run().Passed, string.Join("\n", _studio.Checks.Run().Findings.Select(f => f.Message)));
        Assert.Contains(DesignChecks.All, c => c.Id == "assets");

        var font = _studio.Design.Assets!.Faces[0].File;
        File.Delete(Path.Combine(Volume, font));
        var missing = _studio.Checks.Run();
        Assert.False(missing.Passed);
        Assert.Contains(missing.Findings, f => f.Check == "assets" && f.Kind == "assets" && f.Message.Contains("missing from the volume", StringComparison.Ordinal));
        Assert.Contains("href=\"/Studio/Brand/Assets\"", await _studio.Get("/Studio/Checks"));

        File.WriteAllBytes(Path.Combine(Volume, font), AssetFixtures.Woff2(extra: 99));
        Assert.Contains(_studio.Checks.Run().Findings, f => f.Check == "assets" && f.Message.Contains("hash", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_hostile_file_put_on_the_volume_by_hand_is_found_by_the_check_and_never_served()
    {
        await _studio.SignIn();
        var evil = AssetFixtures.Bytes(AssetFixtures.GoodSvg(inner: "<script>alert(1)</script>"));
        var name = BrandAssetValidator.StoredFileName(evil, ".svg");
        File.WriteAllBytes(Path.Combine(Volume, name), evil);
        _studio.Store.Save("assets", "assets", new BrandAssets(Logo: new AssetFile(name, 64, 64, evil.Length), LogoAlt: "Acme"), "admin", "Edited by hand");
        _studio.Design.Reload();

        var report = _studio.Checks.Run();
        Assert.False(report.Passed);
        Assert.Contains(report.Findings, f => f.Check == "assets" && f.Message.Contains("<script>", StringComparison.Ordinal));
        var run = _studio.Releases.Execute("0.1.0", string.Empty, "admin");
        Assert.False(run.Succeeded);
        Assert.Equal(StageStatus.Failed, run.Stages[0].Status);
        Assert.Contains(run.Stages[0].Log, l => l.Contains("files of the brand", StringComparison.Ordinal));
        Assert.Empty(_studio.Releases.Releases());
    }

    [Fact]
    public async Task An_item_on_the_volume_with_a_name_that_is_not_safe_is_left_out_and_the_checks_say_so()
    {
        await _studio.SignIn();
        _studio.Store.Save("assets", "assets", new BrandAssets(Logo: new AssetFile("../../secret.svg"), LogoAlt: "x"), "admin", "Edited by hand");
        _studio.Design.Reload();
        Assert.Null(_studio.Design.Assets);
        Assert.Contains(_studio.Checks.Run().Findings, f => f.Check == "volume" && f.Kind == "assets");
        Assert.True((await _studio.Client.GetAsync("/Studio/Brand/Assets")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_release_carries_the_files_in_brand_with_their_hashes_in_the_manifest_the_archive_and_the_design_data()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme Anvils");
        await UploadOk("favicon", AssetFixtures.Png(32, 32), "favicon.png", "image/png");
        await AddFontOk("Acme Sans", 400, "normal", AssetFixtures.Woff2());
        var run = _studio.Releases.Execute("0.1.0", "With assets.", "admin");
        Assert.True(run.Succeeded, string.Join("\n", run.Stages.SelectMany(s => s.Log)));
        Assert.Contains(run.Stages.Single(s => s.Name == "Build").Log, l => l.StartsWith("Brand assets: 3 file(s)", StringComparison.Ordinal));
        Assert.Contains(run.Stages.Single(s => s.Name == "Check").Log, l => l.Contains("The files of the brand are valid", StringComparison.Ordinal));

        var assets = _studio.Design.Assets!;
        var logo = "brand/" + BrandAssets.PublicName("logo", assets.Logo!);
        var font = "brand/" + BrandAssets.PublicName(assets.Faces[0]);
        var directory = _studio.Releases.DirectoryOf("0.1.0");
        Assert.Equal(GoodSvg, File.ReadAllBytes(Path.Combine(directory, logo)));
        Assert.True(File.Exists(Path.Combine(directory, font)));

        var manifest = _studio.Releases.Get("0.1.0")!;
        foreach (var path in new[] { logo, font, "brand/" + BrandAssets.PublicName("favicon", assets.Favicon!) })
        {
            var entry = manifest.Files.Single(f => f.Path == path);
            Assert.Equal(BrandAssetValidator.Sha256(File.ReadAllBytes(Path.Combine(directory, path))), entry.Sha256);
        }

        // served by the release address with the headers of a brand file
        var served = await _studio.Client.GetAsync("/releases/0.1.0/" + logo);
        Assert.Equal("image/svg+xml", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal("default-src 'none'; style-src 'none'", served.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("font/woff2", (await _studio.Client.GetAsync("/releases/0.1.0/" + font)).Content.Headers.ContentType!.MediaType);
        Assert.Equal("image/png", (await _studio.Client.GetAsync("/releases/0.1.0/brand/" + BrandAssets.PublicName("favicon", assets.Favicon!))).Content.Headers.ContentType!.MediaType);

        // the stylesheets of the release point to the fonts relative to themselves
        Assert.Contains($"url(\"{font}\")", await _studio.Get("/releases/0.1.0/matui.css"));
        Assert.Contains($"url(\"../{font}\")", await _studio.Get("/releases/0.1.0/css/matui.apps.css"));

        // design.json names the files as they lie in the release, with the alt text
        var design = JsonDocument.Parse(await _studio.Get("/releases/0.1.0/design.json")).RootElement.GetProperty("assets");
        Assert.Equal(logo, design.GetProperty("logo").GetProperty("file").GetString());
        Assert.Equal("Acme Anvils", design.GetProperty("logo").GetProperty("alt").GetString());
        Assert.Equal(font, design.GetProperty("fonts")[0].GetProperty("file").GetString());
        Assert.Equal("Acme Sans", design.GetProperty("fonts")[0].GetProperty("family").GetString());

        // the archive has the files under matui/brand/, their hashes in its manifest, and says how to use them
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync("/releases/0.1.0/matui-0.1.0.zip")));
        using (var data = new MemoryStream())
        {
            await zip.GetEntry("matui/" + logo)!.Open().CopyToAsync(data);
            Assert.Equal(GoodSvg, data.ToArray());
        }

        Assert.NotNull(zip.GetEntry("matui/" + font));
        var zipManifest = JsonDocument.Parse(new StreamReader(zip.GetEntry("matui/matui.manifest.json")!.Open()).ReadToEnd()).RootElement.GetProperty("files");
        Assert.Contains(zipManifest.EnumerateArray(), f => f.GetProperty("name").GetString() == logo && f.GetProperty("sha256").GetString() == BrandAssetValidator.Sha256(GoodSvg));
        var readme = new StreamReader(zip.GetEntry("matui/README.txt")!.Open()).ReadToEnd();
        Assert.Contains("brand/", readme);
        Assert.Contains("../brand/fonts/<name>", readme);
        using var apps = new StreamReader(zip.GetEntry("matui/css/matui.apps.css")!.Open());
        Assert.Contains("url(\"../brand/fonts/", await apps.ReadToEndAsync());
    }

    [Fact]
    public async Task The_downloads_and_the_manifest_of_the_site_list_the_files_of_the_brand()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        var manifest = JsonDocument.Parse(await _studio.Get("/ui/manifest.json")).RootElement.GetProperty("files");
        var logo = _studio.Design.Assets!.Logo!;
        var entry = manifest.EnumerateArray().Single(f => f.GetProperty("name").GetString() == "brand/" + BrandAssets.PublicName("logo", logo));
        Assert.Equal("/ui/brand/" + BrandAssets.PublicName("logo", logo), entry.GetProperty("url").GetString());
        Assert.Equal(BrandAssetValidator.Sha256(GoodSvg), entry.GetProperty("sha256").GetString());
        Assert.Contains("brand/logo-", await _studio.Get("/Download"));
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync("/ui/matui.zip")));
        Assert.NotNull(zip.GetEntry("matui/brand/" + BrandAssets.PublicName("logo", logo)));
    }

    [Fact]
    public async Task A_release_without_assets_has_no_brand_folder_no_assets_in_its_design_data_and_the_archive_it_always_had()
    {
        await _studio.SignIn();
        var run = _studio.Releases.Execute("0.1.0", string.Empty, "admin");
        Assert.True(run.Succeeded);
        Assert.Contains(run.Stages.Single(s => s.Name == "Build").Log, l => l.Contains("No brand assets saved", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(_studio.Releases.DirectoryOf("0.1.0"), "brand")));
        Assert.DoesNotContain(_studio.Releases.Get("0.1.0")!.Files, f => f.Path.StartsWith("brand/", StringComparison.Ordinal));
        Assert.False(JsonDocument.Parse(await _studio.Get("/releases/0.1.0/design.json")).RootElement.TryGetProperty("assets", out _));
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync("/releases/0.1.0/matui-0.1.0.zip")));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("matui/brand", StringComparison.Ordinal));
        Assert.DoesNotContain("brand/", new StreamReader(zip.GetEntry("matui/README.txt")!.Open()).ReadToEnd());
        Assert.DoesNotContain("@font-face", await _studio.Get("/releases/0.1.0/matui.css"));
        Assert.Equal((await _studio.Get("/releases/0.1.0/css/matui.apps.css")).Length, TestPaths.ReadToolkit("css/matui.apps.css").Length);
    }

    [Fact]
    public async Task The_changelog_of_a_release_names_the_assets()
    {
        await _studio.SignIn();
        await UploadOk("logo", GoodSvg, "logo.svg", "image/svg+xml", "Acme");
        Assert.Contains(_studio.Releases.PendingChanges(), l => l.StartsWith("Brand assets assets:", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------ the rule

    [Fact]
    public void The_rule_exists_is_a_must_of_its_area_and_fits_the_limit_of_a_rule()
    {
        var rule = RuleBook.Get("IDENT-08")!;
        Assert.Equal("IDENT", rule.Area);
        Assert.Equal(RuleLevel.Must, rule.Level);
        Assert.InRange(rule.Statement.Length, 10, 1200);
        Assert.Empty(RuleValidator.Validate(rule, RuleBook.All.Where(r => r.Id != rule.Id)));
        Assert.Contains("CODE-03", rule.Statement);
        Assert.Contains("alt text", rule.Statement);
        Assert.Contains("what is inside it", rule.Statement);
        // the rule about fonts says where the exception is
        Assert.Contains("IDENT-08", RuleBook.Get("TYPE-01")!.Statement);
    }
}
