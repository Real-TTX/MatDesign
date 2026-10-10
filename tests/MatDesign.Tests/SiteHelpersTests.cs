using MatDesign.Web.Services;
using Microsoft.AspNetCore.Http;

namespace MatDesign.Tests;

public class SiteHelpersTests
{
    private static HttpRequest Request(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    // ---------------------------------------------------------------------------------------------------- Ui

    [Fact]
    public void Inline_encodes_the_text_and_turns_backticks_into_code()
        => Assert.Equal("&lt;b&gt;x&lt;/b&gt; and <code>mat-btn</code>", Ui.Inline("<b>x</b> and `mat-btn`").ToString());

    [Fact]
    public void Rich_sets_the_identifiers_of_the_toolkit_as_code_and_encodes_the_rest()
    {
        var html = Ui.Rich("Use mat-btn-row--plain and data-confirm, not <b>this</b>; aria-label names it.").ToString();
        Assert.Contains("<code>mat-btn-row--plain</code>", html);
        Assert.Contains("<code>data-confirm</code>", html);
        Assert.Contains("<code>aria-label</code>", html);
        Assert.Contains("&lt;b&gt;this&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>", html);
    }

    [Fact]
    public void An_icon_is_a_use_of_the_sprite_and_a_bad_name_is_refused()
    {
        Assert.Contains("href=\"#mat-i-star\"", Ui.Icon("star").ToString());
        Assert.Contains("class=\"mat-icon extra\"", Ui.Icon("star", "extra").ToString());
        Assert.Throws<ArgumentException>(() => Ui.Icon("\"><script>"));
    }

    [Fact]
    public void A_swatch_carries_its_colour_as_an_attribute_not_as_a_style()
    {
        var html = Ui.Swatch("#d6336c", "chip").ToString();
        Assert.Contains("fill=\"#d6336c\"", html);
        Assert.Contains("aria-label=\"#d6336c\"", html);
        Assert.DoesNotContain("style=", html);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(841, "841 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(48000, "46.9 KB")]
    public void Sizes_read_the_same_on_every_machine(int bytes, string expected)
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(expected, Ui.Kb(bytes));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = before;
        }
    }

    [Theory]
    [InlineData("Alex Morgan", "AM")]
    [InlineData("visitor", "V")]
    [InlineData("a b c", "AB")]
    public void Initials_are_the_first_letters_of_the_first_two_words(string name, string expected)
        => Assert.Equal(expected, Ui.Initials(name));

    // ---------------------------------------------------------------------------------------------------- the menu

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/Identity", "/Identity")]
    [InlineData("/Identity/Palette", "/Identity/Palette")]
    [InlineData("/Identity/App", "/Identity")]
    [InlineData("/Foundations/Icons", "/Foundations")]
    [InlineData("/Components/Detail", "/Components")]
    [InlineData("/rules/detail", "/Rules")]
    public void The_longest_matching_item_of_the_menu_is_the_active_one(string path, string expected)
        => Assert.Equal(expected, Navigation.ActiveHref(path));

    [Fact]
    public void An_unknown_path_marks_nothing()
        => Assert.Null(Navigation.ActiveHref("/nowhere"));

    [Fact]
    public void Every_href_of_the_menu_is_unique()
    {
        var hrefs = Navigation.Groups.SelectMany(g => g.Items).Select(i => i.Href).ToList();
        Assert.Equal(hrefs.Count, hrefs.Distinct().Count());
    }

    // ---------------------------------------------------------------------------------------------------- lists

    [Fact]
    public void The_address_of_a_list_carries_every_tool_in_effect()
    {
        var toolbar = new ToolbarModel
        {
            Id = "x",
            Action = "/List",
            Search = "a b",
            Sort = "size",
            Descending = true,
            DefaultSort = "name",
            Filters = [new("cat", "Category", [new("f", "F")], "f"), new("empty", "Empty", [new("v", "V")], null)],
            Keep = new Dictionary<string, string?> { ["app"] = "m", ["theme"] = "" },
            Views = [new("list", "List", "list")],
            View = "list",
        };

        Assert.Equal("/List?app=m&q=a+b&cat=f&sort=size&dir=desc&view=list", toolbar.UrlWithout(string.Empty));
        Assert.Equal("/List?app=m&cat=f&sort=size&dir=desc&view=list", toolbar.UrlWithout("q"));
        Assert.Equal("/List?app=m&q=a+b&sort=size&dir=desc&view=list", toolbar.UrlWithout("cat"));
        Assert.Equal("/List?app=m&q=a+b&cat=f&view=list", toolbar.UrlWithout("sort"));
        Assert.Equal("/List?app=m&q=a+b&cat=f&sort=size&dir=desc&view=list&page=3", toolbar.UrlWithPage(3));
        Assert.Equal(toolbar.UrlWithout(string.Empty), toolbar.UrlWithPage(1));
    }

    [Fact]
    public void A_list_without_tools_in_effect_has_the_plain_address()
    {
        var toolbar = new ToolbarModel { Id = "x", Action = "/List", Sort = "name", DefaultSort = "name" };
        Assert.Equal("/List", toolbar.UrlWithout(string.Empty));
        Assert.False(toolbar.SearchActive);
        Assert.False(toolbar.SortActive);
        Assert.Equal(0, toolbar.ActiveFilters);
    }

    [Theory]
    [InlineData(1, 3, new[] { 1, 2, 3 })]
    [InlineData(5, 10, new[] { 1, 0, 3, 4, 5, 6, 7, 0, 10 })]
    [InlineData(1, 10, new[] { 1, 2, 3, 0, 10 })]
    [InlineData(10, 10, new[] { 1, 0, 8, 9, 10 })]
    public void The_pager_shows_the_ends_and_two_pages_around_the_current_one(int page, int pages, int[] expected)
    {
        var pager = new PagerModel { Page = page, PageSize = 10, Total = pages * 10, Url = p => $"?page={p}" };
        Assert.Equal(pages, pager.Pages);
        Assert.Equal(expected, pager.Window());
    }

    [Fact]
    public void The_pager_counts_from_and_to()
    {
        var pager = new PagerModel { Page = 3, PageSize = 25, Total = 190, Url = p => $"?page={p}" };
        Assert.Equal(51, pager.From);
        Assert.Equal(75, pager.To);
        Assert.Equal(8, pager.Pages);

        var last = new PagerModel { Page = 8, PageSize = 25, Total = 190, Url = p => $"?page={p}" };
        Assert.Equal(190, last.To);

        var none = new PagerModel { Page = 1, PageSize = 25, Total = 0, Url = p => $"?page={p}" };
        Assert.Equal(0, none.From);
        Assert.Equal(1, none.Pages);
    }

    [Fact]
    public void The_list_parameters_are_read_from_the_query_string()
    {
        var request = Request("?q=%20alpha%20&page=3&dir=desc&empty=");
        Assert.Equal("alpha", ListQuery.Text(request, "q"));
        Assert.Null(ListQuery.Text(request, "empty"));
        Assert.Null(ListQuery.Text(request, "missing"));
        Assert.Equal(3, ListQuery.Page(request));
        Assert.True(ListQuery.Descending(request));
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-4")]
    [InlineData("?page=abc")]
    [InlineData("")]
    public void A_bad_page_number_is_page_one(string query)
        => Assert.Equal(1, ListQuery.Page(Request(query)));

    [Fact]
    public void A_page_of_a_list_is_a_slice()
        => Assert.Equal([4, 5, 6], ListQuery.Slice(Enumerable.Range(1, 10), 2, 3));

    // ---------------------------------------------------------------------------------------------------- the catalogue lists

    [Fact]
    public void The_toolkit_files_are_the_four_the_download_page_promises_and_the_seven_parts()
    {
        Assert.Equal(7, ToolkitFiles.CssParts.Count);
        Assert.All(ToolkitFiles.CssParts, p => Assert.True(File.Exists(TestPaths.Toolkit($"css/{p}")), p));
    }

    [Fact]
    public void The_shell_skeleton_is_the_one_of_the_layout()
    {
        // The skeleton on the shell page is written by hand: it must still name every part of the shell the layout draws.
        var layout = File.ReadAllText(TestPaths.Web("Pages/Shared/_Layout.cshtml"));
        foreach (var part in new[] { "mat-app", "mat-nav", "mat-nav__brand", "mat-nav__close", "mat-nav__scroll", "mat-nav__foot", "mat-user", "mat-theme", "mat-nav__version", "mat-main", "mat-topbar", "mat-burger", "mat-crumbs", "mat-page", "mat-page__head", "mat-menu" })
        {
            Assert.Contains(part, layout);
            Assert.Contains(part, ShellSkeleton.Html);
        }
    }
}
