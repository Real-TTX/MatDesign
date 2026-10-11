using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The optional icon library as data (src/MatUi/wwwroot/iconlib, built by tools/iconlib.js): the sprite and the catalogue agree, every icon is in categories that exist (and many in
/// several), the smart-home areas are all there and add up to "Home automation", the drawings are shapes that take their colour from the text, and the toolkit does not depend on it.
/// </summary>
public sealed partial class IconLibraryDataTests
{
    private static readonly string[] Areas = ["lighting", "climate", "access", "alarm", "appliances", "energy", "sensors", "rooms", "outdoor", "automation"];

    private static readonly JsonElement Catalogue = JsonDocument.Parse(TestPaths.ReadToolkit("iconlib/iconlib.json")).RootElement;

    private static readonly string Sprite = TestPaths.ReadToolkit("iconlib/matui-iconlib.svg");

    private sealed record Item(string Name, string[] Categories, string[] Tags, string Origin);

    private static readonly List<Item> Items = Catalogue.GetProperty("items").EnumerateArray()
        .Select(i => new Item(
            i.GetProperty("name").GetString()!,
            i.GetProperty("categories").EnumerateArray().Select(c => c.GetString()!).ToArray(),
            i.GetProperty("tags").EnumerateArray().Select(c => c.GetString()!).ToArray(),
            i.GetProperty("origin").GetString()!))
        .ToList();

    private static readonly Dictionary<string, JsonElement> Categories = Catalogue.GetProperty("categories").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);

    private static readonly Dictionary<string, string> Bodies = SymbolRegex().Matches(Sprite).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

    [GeneratedRegex(@"<symbol id=""mat-il-([a-z0-9-]+)"" viewBox=""0 0 24 24"">(.*?)</symbol>")]
    private static partial Regex SymbolRegex();

    [GeneratedRegex(@"<(\w+)((?:\s+[\w-]+='[^']*')*)\s*/>")]
    private static partial Regex Shape();

    [GeneratedRegex(@"([\w-]+)='([^']*)'")]
    private static partial Regex Attribute();

    private static string[] In(string category) => Items.Where(i => i.Categories.Contains(category)).Select(i => i.Name).ToArray();

    [Fact]
    public void The_library_is_big_and_sorted_into_many_categories()
    {
        Assert.True(Items.Count >= 1500, $"only {Items.Count} icons");
        Assert.True(Categories.Count >= 40, $"only {Categories.Count} categories");
        Assert.Equal(Items.Count, Catalogue.GetProperty("icons").GetInt32());
    }

    [Fact]
    public void Every_icon_of_the_catalogue_is_in_the_sprite_and_the_other_way_round()
    {
        Assert.Equal(Items.Select(i => i.Name).Order().ToArray(), Bodies.Keys.Order().ToArray());
        Assert.Equal(Items.Count, Items.Select(i => i.Name).Distinct().Count());
        Assert.Equal(Items.Count, Regex.Matches(Sprite, "<symbol ").Count);
    }

    [Fact]
    public void Every_icon_is_in_a_category_that_exists_and_every_category_counts_its_icons()
    {
        Assert.All(Items, i => Assert.True(i.Categories.Length > 0, $"{i.Name} is in no category"));
        Assert.All(Items, i => Assert.All(i.Categories, c => Assert.True(Categories.ContainsKey(c), $"{i.Name}: unknown category {c}")));
        Assert.All(Items, i => Assert.Equal(i.Categories.Length, i.Categories.Distinct().Count()));
        foreach (var (id, category) in Categories)
        {
            var count = In(id).Length;
            Assert.True(count > 0, $"the category {id} is empty");
            Assert.Equal(count, category.GetProperty("count").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(category.GetProperty("title").GetString()), $"{id} has no title");
            Assert.False(string.IsNullOrWhiteSpace(category.GetProperty("description").GetString()), $"{id} has no description");
            Assert.Contains(category.GetProperty("icon").GetString()!, Bodies.Keys);
        }
    }

    [Fact]
    public void An_icon_can_be_in_several_categories_and_many_are()
    {
        Assert.True(Items.Count(i => i.Categories.Length > 1) >= 500, "too few icons are in more than one category");
        var thermometer = Items.Single(i => i.Name == "thermometer");
        Assert.Contains("climate", thermometer.Categories);
        Assert.Contains("sensors", thermometer.Categories);
        Assert.Contains("weather", thermometer.Categories);
        Assert.Contains("general", thermometer.Categories);
    }

    [Fact]
    public void Home_automation_has_every_area_of_a_smart_home_and_is_made_of_them()
    {
        foreach (var area in Areas)
        {
            Assert.True(Categories.ContainsKey(area), $"the area {area} is missing");
            Assert.True(In(area).Length >= 25, $"the area {area} has only {In(area).Length} icons");
            Assert.All(In(area), name => Assert.Contains("home-automation", Items.Single(i => i.Name == name).Categories));
        }

        Assert.True(In("home-automation").Length >= 250, $"home automation has only {In("home-automation").Length} icons");
        Assert.Equal("Home automation", Categories["home-automation"].GetProperty("title").GetString());
        Assert.All(Areas, area => Assert.Equal("home-automation", Categories[area].GetProperty("group").GetString()));

        // what a smart home is made of, from Lucide and from our own drawings
        foreach (var name in new[] { "lightbulb", "lamp-ceiling", "thermometer", "fan", "heater", "blinds", "door-open", "lock", "cctv", "siren", "alarm-smoke", "washing-machine", "refrigerator",
                                     "robot-vacuum", "solar-panel", "ev-charger", "plug-zap", "router", "house-wifi", "sofa", "bed", "sprout", "radiator", "thermostat", "air-conditioner",
                                     "curtains", "window", "garage-door", "keypad", "motion-sensor", "water-leak", "oven", "outlet", "light-switch", "smart-hub", "sprinkler", "wind-turbine" })
        {
            Assert.Contains(name, In("home-automation"));
        }
    }

    [Fact]
    public void General_has_the_icons_every_interface_needs()
    {
        Assert.True(In("general").Length >= 150);
        foreach (var name in new[] { "house", "search", "settings", "check", "x", "plus", "minus", "trash", "pencil", "user", "bell", "mail", "calendar", "clock", "lock", "download", "upload", "star", "heart", "eye" })
        {
            Assert.Contains(name, In("general"));
        }

        Assert.Equal("start", Categories["general"].GetProperty("group").GetString());
        Assert.Equal("start", Categories["home-automation"].GetProperty("group").GetString());
    }

    [Fact]
    public void Names_are_lower_case_words_with_dashes_and_the_tags_help_to_find_them()
    {
        Assert.All(Items, i => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", i.Name));
        Assert.All(Items, i => Assert.Contains(i.Origin, new[] { "lucide", "matdesign" }));
        Assert.All(Items.Where(i => i.Origin == "matdesign"), i => Assert.True(i.Tags.Length >= 3, $"{i.Name} has too few tags to be found by"));
        Assert.Contains("heating", Items.Single(i => i.Name == "radiator").Tags);
        Assert.True(Items.Count(i => i.Origin == "matdesign") >= 40);
    }

    [Fact]
    public void The_drawings_are_shapes_that_take_their_colour_from_the_text_around_them()
    {
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"mat-sprite\"", Sprite);
        Assert.DoesNotContain("<script", Sprite);
        Assert.DoesNotContain("style=", Sprite);
        Assert.DoesNotContain("#", Bodies.Values.Aggregate(string.Empty, (a, b) => a + b));
        foreach (var (name, body) in Bodies)
        {
            var rest = Shape().Replace(body, match =>
            {
                Assert.Contains(match.Groups[1].Value, new[] { "path", "circle", "rect", "line", "ellipse", "polyline", "polygon" });
                foreach (Match a in Attribute().Matches(match.Groups[2].Value))
                {
                    Assert.True(a.Groups[1].Value is "d" or "cx" or "cy" or "r" or "x" or "y" or "width" or "height" or "rx" or "ry" or "x1" or "y1" or "x2" or "y2" or "points" or "fill",
                        $"{name}: attribute {a.Groups[1].Value}");
                    if (a.Groups[1].Value == "fill")
                    {
                        Assert.Equal("currentColor", a.Groups[2].Value);
                    }
                }

                return string.Empty;
            });
            Assert.True(rest.Length == 0, $"{name}: not only shapes: {rest}");
        }
    }

    [Fact]
    public void The_icons_drawn_for_matdesign_stay_inside_the_grid_with_room_for_the_stroke()
    {
        foreach (var item in Items.Where(i => i.Origin == "matdesign"))
        {
            foreach (Match shape in Shape().Matches(Bodies[item.Name]))
            {
                var a = Attribute().Matches(shape.Groups[2].Value).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
                double N(string key) => double.Parse(a.GetValueOrDefault(key, "0"), CultureInfo.InvariantCulture);
                var (low, high) = shape.Groups[1].Value switch
                {
                    "rect" => (Math.Min(N("x"), N("y")), Math.Max(N("x") + N("width"), N("y") + N("height"))),
                    "circle" => (Math.Min(N("cx"), N("cy")) - N("r"), Math.Max(N("cx"), N("cy")) + N("r")),
                    "line" => (Math.Min(Math.Min(N("x1"), N("x2")), Math.Min(N("y1"), N("y2"))), Math.Max(Math.Max(N("x1"), N("x2")), Math.Max(N("y1"), N("y2")))),
                    _ => (1.0, 23.0),
                };
                Assert.True(low >= 0.99 && high <= 23.01, $"{item.Name}: a {shape.Groups[1].Value} leaves the grid ({low}..{high})");
            }
        }
    }

    [Fact]
    public void The_toolkit_does_not_depend_on_the_library()
    {
        foreach (var file in new[] { "css/matui.tokens.css", "css/matui.base.css", "css/matui.shell.css", "css/matui.controls.css", "css/matui.forms.css", "css/matui.pickers.css", "css/matui.lists.css",
                                     "css/matui.dialogs.css", "css/matui.layout.css", "css/matui.apps.css", "js/matui.js", "js/matui.boot.js", "img/matui-icons.svg" })
        {
            var text = TestPaths.ReadToolkit(file);
            Assert.DoesNotContain("iconlib", text);
            Assert.DoesNotContain("mat-il-", text);
        }

        // and the two sprites can be on one page: no id is in both
        var toolkitIds = Regex.Matches(TestPaths.ReadToolkit("img/matui-icons.svg"), "id=\"(mat-i-[a-z0-9-]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.DoesNotContain(Regex.Matches(Sprite, "id=\"(mat-i[a-z-]*-[a-z0-9-]+)\"").Select(m => m.Groups[1].Value), toolkitIds.Contains);
        Assert.DoesNotContain("id=\"mat-i-", Sprite);
    }

    [Fact]
    public void The_licence_names_where_the_icons_come_from_and_travels_with_them()
    {
        var licence = TestPaths.ReadToolkit("iconlib/LICENSE.txt");
        Assert.Contains("Lucide", licence);
        Assert.Contains("ISC License", licence);
        Assert.Contains("Feather", licence);
        Assert.Contains("The MIT License", licence);
        Assert.Contains(Items.Count(i => i.Origin == "lucide").ToString(CultureInfo.InvariantCulture) + " icons are from Lucide", licence);
        Assert.Contains(Items.Count(i => i.Origin == "matdesign").ToString(CultureInfo.InvariantCulture) + " icons were drawn for MatDesign", licence);
        Assert.Contains("mat-il-", TestPaths.ReadToolkit("iconlib/README.txt"));
    }

    [Fact]
    public void The_rule_says_the_library_is_optional_and_separate()
    {
        var rule = MatDesign.Core.RuleBook.All.Single(r => r.Id == "ICON-05");
        Assert.Equal(MatDesign.Core.RuleLevel.Should, rule.Level);
        Assert.Contains("matui-iconlib.svg", rule.Statement);
        Assert.Contains("not part of `matui.css`", rule.Statement);
    }
}

/// <summary>The library as the site shows and serves it: the page (search, categories, filter, sort, the dialog), the files for any app, the single icons, the archive.</summary>
public sealed partial class IconLibrarySiteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IconLibrarySiteTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static readonly JsonElement Catalogue = JsonDocument.Parse(TestPaths.ReadToolkit("iconlib/iconlib.json")).RootElement;

    [GeneratedRegex(@"data-iconlib=""([a-z0-9-]+)""")]
    private static partial Regex ItemName();

    [GeneratedRegex(@"<symbol id=""mat-il-([a-z0-9-]+)""")]
    private static partial Regex PageSymbol();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    private static string[] CategoriesOf(string name)
        => Catalogue.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("name").GetString() == name).GetProperty("categories").EnumerateArray().Select(c => c.GetString()!).ToArray();

    private static int CountOf(string category) => Catalogue.GetProperty("categories").EnumerateArray().Single(c => c.GetProperty("id").GetString() == category).GetProperty("count").GetInt32();

    private async Task<string> Page(string query = "")
    {
        var response = await _client.GetAsync("/Foundations/IconLibrary" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string[] Shown(string html) => ItemName().Matches(html).Select(m => m.Groups[1].Value).ToArray();

    [Theory]
    [InlineData("/ui/iconlib/matui-iconlib.svg", "image/svg+xml", "<symbol id=\"mat-il-lamp\"")]
    [InlineData("/ui/iconlib/iconlib.json", "application/json", "\"name\": \"MatUi icon library\"")]
    [InlineData("/ui/iconlib/LICENSE.txt", "text/plain", "ISC License")]
    [InlineData("/ui/iconlib/README.txt", "text/plain", "mat-il-")]
    [InlineData("/api/iconlib", "application/json", "\"categories\"")]
    public async Task The_files_of_the_library_are_served_with_their_type_for_any_origin(string url, string type, string contains)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(type, response.Content.Headers.ContentType!.MediaType);
        Assert.Contains(contains, await response.Content.ReadAsStringAsync());
        if (url.StartsWith("/ui/", StringComparison.Ordinal))
        {
            Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
    }

    [Fact]
    public async Task A_file_that_has_not_changed_is_answered_with_not_modified()
    {
        var first = await _client.GetAsync("/ui/iconlib/matui-iconlib.svg");
        var etag = first.Headers.ETag!.Tag;

        var request = new HttpRequestMessage(HttpMethod.Get, "/ui/iconlib/matui-iconlib.svg");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task An_icon_is_a_file_of_its_own_that_can_be_opened_as_xml()
    {
        var response = await _client.GetAsync("/ui/iconlib/svg/lamp.svg");
        var svg = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType!.MediaType);
        var document = XDocument.Parse(svg);
        Assert.Equal("svg", document.Root!.Name.LocalName);
        Assert.Equal("currentColor", document.Root.Attribute("stroke")!.Value);
        Assert.Equal("0 0 24 24", document.Root.Attribute("viewBox")!.Value);
        Assert.Equal("2", document.Root.Attribute("stroke-width")!.Value);
        Assert.NotEmpty(document.Root.Elements());

        // an icon drawn for MatDesign is a file too
        Assert.Contains("<rect", await _client.GetStringAsync("/ui/iconlib/svg/radiator.svg"));
    }

    [Theory]
    [InlineData("/ui/iconlib/svg/no-such-icon.svg")]
    [InlineData("/ui/iconlib/svg/..%2Fmatui.svg")]
    [InlineData("/ui/iconlib/svg/LAMP.svg")]
    [InlineData("/ui/iconlib/svg/.svg")]
    public async Task An_icon_that_is_not_in_the_library_is_not_found(string url)
        => Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);

    [Fact]
    public async Task The_archive_holds_every_icon_as_a_file_with_the_sprite_the_catalogue_and_the_licence()
    {
        var bytes = await _client.GetByteArrayAsync("/ui/iconlib/matui-iconlib.zip");
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var names = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Equal(Catalogue.GetProperty("icons").GetInt32() + 4, names.Count);
        foreach (var file in new[] { "matui-iconlib/matui-iconlib.svg", "matui-iconlib/iconlib.json", "matui-iconlib/LICENSE.txt", "matui-iconlib/README.txt", "matui-iconlib/svg/lamp.svg", "matui-iconlib/svg/curtains.svg" })
        {
            Assert.Contains(file, names);
        }
    }

    [Fact]
    public async Task The_catalogue_of_the_api_is_the_catalogue_of_the_file()
    {
        var api = await _client.GetStringAsync("/api/iconlib");
        Assert.Equal(await _client.GetStringAsync("/ui/iconlib/iconlib.json"), api);
        Assert.Equal(TestPaths.ReadToolkit("iconlib/iconlib.json").Replace("\r\n", "\n"), api.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task The_page_is_a_gallery_with_a_search_field_the_categories_beside_it_and_a_pager()
    {
        var html = await Page();

        Assert.Single(Heading().Matches(html));
        Assert.Contains("<h1", html);
        Assert.Contains("class=\"mat-toolbar\"", html);
        Assert.Contains("mat-toolbar__search", html);
        Assert.Contains("id=\"iconlib-filter\"", html);
        Assert.Contains("id=\"iconlib-sort\"", html);
        Assert.DoesNotContain("id=\"iconlib-search\"", html);
        Assert.Contains("class=\"mat-pager\"", html);
        Assert.Equal(96, Shown(html).Length);
        Assert.Contains("class=\"mat-toc-aside\"", html);
        Assert.Contains("id=\"iconlib-cats-dialog\"", html);
        Assert.Contains("id=\"iconlib-dialog\"", html);
        Assert.Contains("data-dialog-open=\"iconlib-dialog\"", html);
        Assert.Contains("<script src=\"/js/doc.js\"></script>", html);
        Assert.DoesNotMatch(@"\sstyle\s*=", html);

        // the header bar offers the categories where they are not beside the list
        Assert.Contains("mat-topbar__index", html);
        Assert.Contains("data-dialog-open=\"iconlib-cats-dialog\"", html);
        Assert.Contains("aria-label=\"Categories, choose one\"", html);
    }

    [Fact]
    public async Task The_page_carries_only_the_symbols_of_the_icons_it_shows()
    {
        var html = await Page();

        var shown = Shown(html);
        var symbols = PageSymbol().Matches(html).Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(shown.Order().ToArray(), symbols.Order().ToArray());
        Assert.True(html.Length < 250_000, $"the page is {html.Length:N0} characters");
    }

    [Fact]
    public async Task The_categories_are_listed_with_their_counts_home_automation_first()
    {
        var html = await Page();

        foreach (var id in new[] { "general", "home-automation", "lighting", "climate", "access", "alarm", "appliances", "energy", "sensors", "rooms", "outdoor", "automation", "weather", "arrows" })
        {
            Assert.Contains($"href=\"/Foundations/IconLibrary?category={id}\"", html);
        }

        Assert.True(html.IndexOf("Home automation", StringComparison.Ordinal) < html.IndexOf("Arrows", StringComparison.Ordinal));
        Assert.Contains("aria-current=\"page\"", html);
        Assert.Contains($"<span class=\"mat-nav__badge mat-nav__badge--plain\">{CountOf("home-automation")}</span>", html);
    }

    [Fact]
    public async Task A_category_narrows_the_list_to_the_icons_that_are_in_it_and_an_icon_can_be_in_several()
    {
        var lighting = await Page("?category=lighting");
        var shown = Shown(lighting);

        Assert.Equal(CountOf("lighting"), shown.Length);
        Assert.All(shown, name => Assert.Contains("lighting", CategoriesOf(name)));
        Assert.Contains("lightbulb", shown);
        Assert.Contains("pendant-light", shown);
        Assert.Contains("<h2>Lighting</h2>", lighting);

        // the thermometer is in more than one: it shows up in each of them
        foreach (var category in new[] { "climate", "sensors", "weather", "general" })
        {
            Assert.Contains("thermometer", Shown(await Page($"?category={category}&q=thermometer")));
        }

        // the category in effect is marked in the index
        Assert.Contains("is-active", lighting);
        Assert.Matches(@"class=""mat-toc__link is-active""[^>]*href=""/Foundations/IconLibrary\?category=lighting""", lighting);
    }

    [Fact]
    public async Task Home_automation_lists_all_its_areas_on_several_pages()
    {
        var first = Shown(await Page("?category=home-automation"));
        var second = Shown(await Page("?category=home-automation&page=2"));
        var third = Shown(await Page("?category=home-automation&page=3"));
        var fourth = Shown(await Page("?category=home-automation&page=4"));

        Assert.Equal(96, first.Length);
        Assert.Equal(CountOf("home-automation"), first.Length + second.Length + third.Length + fourth.Length);
        Assert.Empty(first.Intersect(second));
        Assert.Empty(second.Intersect(third));
    }

    [Fact]
    public async Task The_search_looks_at_the_name_the_tags_and_the_categories_and_the_best_match_comes_first()
    {
        var lamp = Shown(await Page("?q=lamp"));
        Assert.Equal("lamp", lamp[0]);
        Assert.Contains("lamp-ceiling", lamp);
        Assert.Contains("lamp-floor", lamp);

        // a tag: the radiator is found by what it is for
        Assert.Contains("radiator", Shown(await Page("?q=heating")));

        // a category: the garden
        Assert.Contains("sprout", Shown(await Page("?q=garden")));

        // two words must both be found
        var bulb = Shown(await Page("?q=light+bulb"));
        Assert.Contains("lightbulb", bulb);
        Assert.DoesNotContain("lamp-floor", bulb);

        // an old name of an icon finds it
        Assert.Contains("circle-alert", Shown(await Page("?q=alert-circle")));
    }

    [Fact]
    public async Task A_search_without_a_hit_says_so_and_offers_to_reset_the_tools()
    {
        var html = await Page("?q=zzzzzz");

        Assert.Empty(Shown(html));
        Assert.Contains("No icon matches", html);
        Assert.Contains("Reset the tools", html);
        Assert.DoesNotContain("class=\"doc-icons\"", html);
    }

    [Theory]
    [InlineData("?sort=name")]
    [InlineData("?sort=category")]
    [InlineData("?sort=category&dir=desc")]
    [InlineData("?q=lamp&sort=match")]
    [InlineData("?category=energy&sort=name&dir=desc")]
    [InlineData("?page=999")]
    [InlineData("?category=does-not-exist")]
    [InlineData("?sort=nonsense&page=-3")]
    public async Task Every_combination_of_the_tools_answers_with_a_list_of_icons(string query)
    {
        var html = await Page(query);

        Assert.NotEmpty(Shown(html));
        Assert.Single(Heading().Matches(html));
    }

    [Fact]
    public async Task The_sort_can_put_the_icons_in_the_order_of_their_categories_or_reverse_the_names()
    {
        var byName = Shown(await Page());
        var reversed = Shown(await Page("?dir=desc"));
        Assert.Equal(byName.Order(StringComparer.Ordinal).ToArray(), byName);
        Assert.Equal(reversed.Order(StringComparer.Ordinal).Reverse().ToArray(), reversed);
        Assert.NotEqual(byName[0], reversed[0]);

        var byCategory = Shown(await Page("?sort=category"));
        Assert.Contains("general", CategoriesOf(byCategory[0]));
    }

    [Fact]
    public async Task An_unknown_category_is_ignored_and_the_whole_library_is_shown()
    {
        var html = await Page("?category=does-not-exist");

        Assert.Equal(96, Shown(html).Length);
        Assert.DoesNotContain("doc-iconlib__intro", html);
    }

    [Fact]
    public async Task The_page_is_in_the_menu_and_the_tabs_and_the_download_page_offers_the_library_as_an_option()
    {
        var html = await Page();
        Assert.Contains("href=\"/Foundations/IconLibrary\"", html);
        Assert.Contains("Icon library", html);

        var tabs = await _client.GetStringAsync("/Foundations/Icons");
        Assert.Contains("href=\"/Foundations/IconLibrary\"", tabs);

        var download = await _client.GetStringAsync("/Download");
        Assert.Contains("More icons, if you need them", download);
        Assert.Contains("/ui/iconlib/matui-iconlib.svg", download);
        Assert.Contains("/ui/iconlib/matui-iconlib.zip", download);
        Assert.Contains("/api/iconlib", download);
        Assert.Contains("MatUi works without it", download);
    }
}
