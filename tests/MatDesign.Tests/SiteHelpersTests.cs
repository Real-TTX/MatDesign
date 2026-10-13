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
    public void Rich_also_sets_backticks_as_code_once_and_encodes_what_is_in_them()
    {
        Assert.Equal("The <code>data-nav-scope</code> names it and <code>a &lt; b</code> stays text.", Ui.Rich("The `data-nav-scope` names it and `a < b` stays text.").ToString());
        Assert.Equal("<code>_Menu</code> draws <code>mat-nav__item</code>.", Ui.Rich("`_Menu` draws mat-nav__item.").ToString());
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
    [InlineData("/Foundations/Icons", "/Foundations/Icons")]
    [InlineData("/Foundations/Type", "/Foundations/Type")]
    [InlineData("/Patterns/Detail", "/Patterns")]
    [InlineData("/rules/detail", "/Rules")]
    public void The_longest_matching_item_of_the_menu_is_the_active_one(string path, string expected)
        => Assert.Equal(expected, Navigation.ActiveHref(path));

    [Theory]
    [InlineData("/Reports", "?kind=weekly", "/Reports?kind=weekly")]
    [InlineData("/Reports", "?kind=monthly&x=1", "/Reports?kind=monthly")]
    [InlineData("/Reports", "?kind=other", "/Reports")]
    [InlineData("/Reports", "", "/Reports")]
    public void A_menu_item_with_a_query_is_active_only_when_the_query_matches(string path, string query, string expected)
        => Assert.Equal(expected, Navigation.ActiveHref(["/Reports", "/Reports?kind=weekly", "/Reports?kind=monthly"], path, query));

    [Fact]
    public void An_unknown_path_marks_nothing()
        => Assert.Null(Navigation.ActiveHref("/nowhere"));

    [Fact]
    public void Every_href_of_the_menu_is_unique()
    {
        var hrefs = Navigation.Pages.Select(i => i.Href).ToList();
        Assert.Equal(hrefs.Count, hrefs.Distinct().Count());
    }

    // ---------------------------------------------------------------------------------------------------- menus: badges, submenus, sets

    [Fact]
    public void The_menus_of_the_site_obey_the_rules_for_submenus()
    {
        // SHELL-13: one level of children, a parent is no page, every child is.
        Assert.True(Navigation.Sets.Count >= 2, "the site has a switcher of its own");
        Assert.All(Navigation.Sets, s => Assert.True(s.IsValid, s.Id));
        Assert.Equal(Navigation.Sets.Count, Navigation.Sets.Select(s => s.Id).Distinct().Count());
        Assert.Contains(Navigation.Sets, s => s.AllItems.Any(i => i.IsParent));
        foreach (var set in Navigation.Sets)
        {
            var parents = set.AllItems.Where(i => i.IsParent).ToList();
            Assert.All(parents, p => Assert.Null(p.Href));
            Assert.Equal(parents.Count, parents.Select(p => p.Key).Distinct().Count());
        }
    }

    [Fact]
    public void A_menu_with_a_second_level_below_the_first_is_not_valid()
    {
        var tooDeep = new NavSet("x", "X", "list", [new(null, [new("A", null, "list", Children: [new("B", null, null, Children: [new("C", "/c", null)])])])]);
        Assert.False(tooDeep.IsValid);
        var parentWithAddress = new NavSet("y", "Y", "list", [new(null, [new("A", "/a", "list", Children: [new("B", null, null)])])]);
        Assert.False(parentWithAddress.IsValid, "a child without an address is not a page");
    }

    [Theory]
    [InlineData(3, "3")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(12000, "99+")]
    public void A_count_in_the_menu_never_exceeds_99_plus(int number, string text)
        => Assert.Equal(text, NavBadge.Count(number).Text);

    [Fact]
    public void A_badge_is_a_count_or_a_dot_and_has_a_tone()
    {
        Assert.Equal("mat-nav__badge", NavBadge.Count(2).Classes());
        Assert.Equal("mat-nav__badge mat-nav__badge--warn", NavBadge.Count(2, "warn").Classes());
        Assert.Equal("mat-nav__badge mat-nav__badge--error mat-nav__badge--dot", NavBadge.Dot("error").Classes());
        Assert.Equal("mat-nav__badge mat-nav__badge--plain mat-nav__badge--sum", NavBadge.Count(2, "plain").Classes(sum: true));
    }

    [Fact]
    public void A_closed_parent_shows_the_sum_of_its_children_in_the_most_urgent_tone()
    {
        var parent = new NavItem("Items", null, "list", Children:
        [
            new("All", "/all", null, NavBadge.Count(12, "plain", "items")),
            new("Review", "/review", null, NavBadge.Count(3, "warn", "to review")),
            new("Plain", "/plain", null),
        ]);

        var sum = parent.SumOfChildren;
        Assert.NotNull(sum);
        Assert.Equal("15", sum.Text);
        Assert.Equal("warn", sum.Tone);
        Assert.Contains("to review", sum.Label);
    }

    [Fact]
    public void Badges_that_are_not_counts_add_up_to_a_dot()
    {
        var sum = NavBadge.Sum([NavBadge.Count(2, "ok"), NavBadge.Dot("error")]);
        Assert.NotNull(sum);
        Assert.True(sum.IsDot);
        Assert.Equal("error", sum.Tone);
        Assert.Null(NavBadge.Sum([]));
    }

    [Fact]
    public void A_parent_with_a_badge_of_its_own_does_not_add_one_up()
    {
        var parent = new NavItem("Items", null, "list", NavBadge.Count(7, "accent"), [new("A", "/a", null, NavBadge.Count(1, "warn"))]);
        Assert.Null(parent.SumOfChildren);
    }

    [Fact]
    public void The_menu_that_has_the_current_page_is_the_one_that_is_shown()
    {
        var shell = ShellModel.Site();
        Assert.True(shell.HasSwitcher);
        Assert.Equal("design", shell.CurrentMenu("/Identity").Id);
        Assert.Equal("design", shell.CurrentMenu("/Foundations/Type").Id);
        Assert.Equal("toolkit", shell.CurrentMenu("/Shell").Id);
        Assert.Equal("toolkit", shell.CurrentMenu("/Components").Id);
        Assert.Equal("design", shell.CurrentMenu("/nowhere").Id);
        Assert.Equal("design", shell.CurrentMenu(null).Id);
    }

    // ---------------------------------------------------------------------------------------------------- the switcher: place and groups (SHELL-15)

    private static ShellModel Shell(SwitcherPlace place, params NavSet[] menus) => new()
    {
        App = MatDesign.Core.AppRegistry.Get("matdesign")!,
        Menus = menus,
        UserName = "A",
        UserRole = "B",
        Version = "v",
        SwitcherPlace = place,
    };

    private static NavSet Menu(string id, NavSetParent? parent = null, bool attention = false)
        => new(id, id.ToUpperInvariant(), "list", [new(null, [new("Page " + id, "/" + id, "list", attention ? NavBadge.Count(1, "warn", "to look at") : null)])], parent);

    [Theory]
    [InlineData(SwitcherPlace.Top, true, false, false)]
    [InlineData(SwitcherPlace.Bottom, false, true, false)]
    [InlineData(SwitcherPlace.Both, true, true, false)]
    [InlineData(SwitcherPlace.End, false, false, true)]
    public void The_switcher_is_under_the_logo_at_the_foot_at_both_or_at_the_end_of_the_menu(SwitcherPlace place, bool top, bool bottom, bool end)
    {
        var shell = Shell(place, Menu("a"), Menu("b"));
        Assert.Equal(top, shell.SwitcherAtTop);
        Assert.Equal(bottom, shell.SwitcherAtBottom);
        Assert.Equal(end, shell.SwitcherAtEnd);
        // with one menu there is nothing to switch, wherever it would sit
        var single = Shell(place, Menu("a"));
        Assert.False(single.SwitcherAtTop || single.SwitcherAtBottom || single.SwitcherAtEnd);
    }

    [Fact]
    public void The_switcher_is_under_the_logo_unless_the_app_says_otherwise_and_only_with_two_menus()
    {
        Assert.True(Shell(SwitcherPlace.Top, Menu("a"), Menu("b")).SwitcherAtTop);
        Assert.Equal(SwitcherPlace.Top, new ShellModel { App = MatDesign.Core.AppRegistry.Get("matdesign")!, Menus = [Menu("a"), Menu("b")], UserName = "A", UserRole = "B", Version = "v" }.SwitcherPlace);
        // the site itself has it at the foot, above the user, and not under the logo
        var site = ShellModel.Site();
        Assert.True(site.SwitcherAtBottom);
        Assert.False(site.SwitcherAtTop);
        var single = Shell(SwitcherPlace.Both, Menu("a"));
        Assert.False(single.SwitcherAtTop);
        Assert.False(single.SwitcherAtBottom);
    }

    [Fact]
    public void Menus_of_one_parent_are_gathered_below_it_where_its_first_menu_is()
    {
        var admin = new NavSetParent("Administration", "shield");
        var shell = Shell(SwitcherPlace.Top, Menu("work"), Menu("people", admin), Menu("docs"), Menu("system", admin));
        var entries = shell.SwitcherEntries;
        Assert.Equal(3, entries.Count);
        Assert.Null(entries[0].Parent);
        Assert.Equal(["people", "system"], entries[1].Menus.Select(m => m.Id));
        Assert.Equal("administration", entries[1].Parent!.Key);
        Assert.Null(entries[2].Parent);
        Assert.Equal("docs", entries[2].Menus.Single().Id);
    }

    [Fact]
    public void A_parent_carries_the_attention_of_the_menus_below_it()
    {
        var admin = new NavSetParent("Administration", "shield");
        var shell = Shell(SwitcherPlace.Top, Menu("work"), Menu("people", admin, attention: true), Menu("system", admin));
        Assert.False(shell.SwitcherEntries[0].NeedsAttention);
        Assert.True(shell.SwitcherEntries[1].NeedsAttention);
    }

    [Fact]
    public void A_menu_has_a_start_page_when_it_names_an_address_and_only_the_studio_of_the_site_does()
    {
        Assert.False(Menu("a").HasStartPage);
        Assert.True(new NavSet("b", "B", "list", Menu("b").Groups, Href: "/b").HasStartPage);
        Assert.All(Navigation.Sets, s => Assert.False(s.HasStartPage, s.Id));
        Assert.Equal("/Studio", Navigation.StudioSet.Href);
        Assert.Contains(Navigation.StudioSet.Pages, p => p.Href == Navigation.StudioSet.Href);   // the start page is a page of the menu
    }

    [Fact]
    public void Menus_that_are_last_stand_after_the_choices_and_a_choice_has_fields_even_without_any()
    {
        var shell = new ShellModel
        {
            App = MatDesign.Core.AppRegistry.Get("matdesign")!,
            Menus = [Menu("work"), new NavSet("admin", "Administration", "shield", Menu("admin").Groups, Last: true), Menu("docs")],
            UserName = "A",
            UserRole = "B",
            Version = "v",
            Choices = [new SwitcherChoices("Tenants and more", "store", [new("Muster GmbH", "/switch", true), new("Beispiel AG", "/switch", false, new Dictionary<string, string> { ["tenantId"] = "2" })])],
        };

        Assert.Equal(["work", "docs"], shell.SwitcherEntries.Where(e => !e.Last).Select(e => e.Menus[0].Id));
        Assert.Equal(["admin"], shell.SwitcherEntries.Where(e => e.Last).Select(e => e.Menus[0].Id));
        Assert.Equal("tenants-and-more", shell.Choices[0].Key);
        Assert.Empty(shell.Choices[0].Items[0].Fields);
        Assert.Equal("2", shell.Choices[0].Items[1].Fields["tenantId"]);
        Assert.Empty(ShellModel.Site().Choices);   // the site itself has none
    }

    [Fact]
    public void The_menus_of_the_site_are_not_grouped()
        => Assert.All(Navigation.Sets, s => Assert.Null(s.Parent));

    [Fact]
    public void A_menu_asks_for_attention_when_an_item_has_a_warning_or_an_error_badge()
    {
        Assert.True(Navigation.Sets.Single(s => s.Id == "design").NeedsAttention, "the palette has pairs that are too close");
        Assert.False(Navigation.Sets.Single(s => s.Id == "toolkit").NeedsAttention);
    }

    [Fact]
    public void The_html_tag_keeps_its_attributes_for_values_only()
    {
        Assert.Equal(string.Empty, ShellModel.Site().RootAttributes);
        var shell = new ShellModel { App = MatDesign.Core.AppRegistry.Get("matmail")!, Menus = Navigation.Sets, UserName = "A", UserRole = "B", Version = "v", Theme = "dark", CustomAccent = "#ff6600", PreviewQuery = "app=matmail" };
        Assert.Equal("data-theme=\"dark\" data-accent=\"custom\" data-custom-accent=\"#ff6600\" data-preview", shell.RootAttributes);
        Assert.Equal(string.Empty, new ShellModel { App = shell.App, Menus = Navigation.Sets, UserName = "A", UserRole = "B", Version = "v", Theme = "<script>" }.RootAttributes);
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
    public void The_toolkit_files_are_the_four_the_download_page_promises_and_the_parts()
    {
        Assert.Equal(10, ToolkitFiles.CssParts.Count);
        Assert.All(ToolkitFiles.CssParts, p => Assert.True(File.Exists(TestPaths.Toolkit($"css/{p}")), p));
    }

    [Fact]
    public void The_shell_skeleton_is_the_one_of_the_layout()
    {
        // The skeleton on the shell page is written by hand: it must still name every part of the shell the layout draws.
        var layout = File.ReadAllText(TestPaths.Web("Pages/Shared/_Layout.cshtml")) + File.ReadAllText(TestPaths.Web("Pages/Shared/_Menu.cshtml")) + File.ReadAllText(TestPaths.Web("Pages/Shared/_MenuSwitcher.cshtml")) + File.ReadAllText(TestPaths.Web("Services/Navigation.cs"));
        foreach (var part in new[] { "mat-app", "mat-nav", "mat-nav__brand", "mat-nav__close", "mat-nav__switch", "mat-switcher", "mat-nav__branch", "mat-nav__sub", "mat-nav__badge", "mat-nav__scroll", "mat-nav__foot", "mat-user", "mat-theme", "mat-nav__version", "mat-main", "mat-topbar", "mat-burger", "mat-crumbs", "mat-page", "mat-page__head", "mat-menu" })
        {
            Assert.Contains(part, layout);
            Assert.Contains(part, ShellSkeleton.Html);
        }
    }
}
