using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>
/// The pages and the catalogue of examples against the toolkit: classes that exist, icons that exist, no inline styles, labelled cells and
/// controls, unique ids. The documentation is only worth anything if it cannot drift from what the toolkit ships.
/// </summary>
public partial class MarkupContractTests
{
    private static readonly string[] Parts = ["tokens", "base", "shell", "controls", "forms", "pickers", "lists", "dialogs", "layout", "apps"];

    private static readonly Lazy<IReadOnlySet<string>> ToolkitClasses = new(() => ClassesIn(Parts.Select(p => TestPaths.ReadToolkit($"css/matui.{p}.css"))));

    private static readonly Lazy<IReadOnlySet<string>> SiteClasses = new(() => ClassesIn([File.ReadAllText(TestPaths.Web("wwwroot/css/site.css"))]));

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex AttributeSelector();

    [GeneratedRegex(@"\.([A-Za-z_][\w-]*)")]
    private static partial Regex ClassSelector();

    [GeneratedRegex(@"class=""([^""]*)""")]
    private static partial Regex ClassAttribute();

    // a class built with Razor (doc-w-@size, mat-pill--@tone) is only a prefix here: skip it
    [GeneratedRegex(@"\b((?>(?:mat|doc)-[A-Za-z0-9_-]*[A-Za-z0-9]))(?!-*@)")]
    private static partial Regex PrefixedClass();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"\son(?:click|change|submit|load|input|focus|blur|keydown|keyup|mouseover)\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();

    [GeneratedRegex(@"<script(?![^>]*\ssrc=)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"<td\b([^>]*)>")]
    private static partial Regex TableCell();

    [GeneratedRegex(@"<button\b([^>]*)>")]
    private static partial Regex Button();

    [GeneratedRegex(@"<(input|select|textarea)\b([^>]*)>")]
    private static partial Regex Control();

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"#mat-i-([a-z0-9-]+)")]
    private static partial Regex SpriteReference();

    [GeneratedRegex(@"Icon\(\s*""([a-z0-9-]+)""")]
    private static partial Regex IconCall();

    private static IReadOnlySet<string> ClassesIn(IEnumerable<string> stylesheets)
        => stylesheets
            .SelectMany(css => CssTokens.ParseBlocks(css).Select(b => b.Selector))
            .SelectMany(selector => ClassSelector().Matches(AttributeSelector().Replace(selector, string.Empty)).Select(m => m.Groups[1].Value))
            .ToHashSet();

    private static IEnumerable<string> Files(string pattern)
        => Directory.EnumerateFiles(TestPaths.Web("Pages"), pattern, SearchOption.AllDirectories);

    // ---------------------------------------------------------------------------------------------------- the toolkit

    [Fact]
    public void Toolkit_classes_and_properties_follow_the_naming()
    {
        // CODE-04: classes start with mat- (or are a state such as is-active), custom properties with --mat-.
        var badClasses = ToolkitClasses.Value.Where(c => !c.StartsWith("mat-", StringComparison.Ordinal) && !c.StartsWith("is-", StringComparison.Ordinal)).Order().ToList();
        Assert.True(badClasses.Count == 0, "Classes without the prefix: " + string.Join(", ", badClasses));

        var badProperties = Parts
            .SelectMany(p => CssTokens.ParseBlocks(TestPaths.ReadToolkit($"css/matui.{p}.css")).SelectMany(b => b.Declarations))
            .Select(d => d.Name).Where(n => n.StartsWith("--", StringComparison.Ordinal) && !n.StartsWith("--mat-", StringComparison.Ordinal)).Distinct().Order().ToList();
        Assert.True(badProperties.Count == 0, "Custom properties without the prefix: " + string.Join(", ", badProperties));
    }

    // ---------------------------------------------------------------------------------------------------- the pages

    [Fact]
    public void Pages_use_only_classes_that_exist()
    {
        var problems = new List<string>();
        foreach (var file in Files("*.cshtml").Concat(Directory.EnumerateFiles(TestPaths.Web("Services"), "*.cs")))
        {
            var text = File.ReadAllText(file);
            var tokens = file.EndsWith(".cshtml", StringComparison.Ordinal)
                ? ClassAttribute().Matches(text).SelectMany(m => PrefixedClass().Matches(m.Groups[1].Value).Select(t => t.Value))
                : Regex.Matches(text, @"""((?:mat|doc)-[a-z0-9_-]*[a-z0-9])""").Select(m => m.Groups[1].Value);
            foreach (var token in tokens.Distinct())
            {
                var known = token.StartsWith("mat-", StringComparison.Ordinal) ? ToolkitClasses.Value : SiteClasses.Value;
                if (!known.Contains(token))
                {
                    problems.Add($"{Path.GetFileName(file)}: .{token}");
                }
            }
        }

        Assert.True(problems.Count == 0, "Classes that no stylesheet defines:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Pages_have_no_inline_style_handler_or_script()
    {
        // CODE-02
        var problems = new List<string>();
        foreach (var file in Files("*.cshtml"))
        {
            var text = File.ReadAllText(file);
            problems.AddRange(InlineStyle().Matches(text).Select(_ => $"{Path.GetFileName(file)}: style attribute"));
            problems.AddRange(InlineHandler().Matches(text).Select(_ => $"{Path.GetFileName(file)}: event handler attribute"));
            problems.AddRange(InlineScript().Matches(text).Select(_ => $"{Path.GetFileName(file)}: inline script"));
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Pages_load_nothing_from_another_site()
    {
        // CODE-03
        var problems = Files("*.cshtml").Where(f => Regex.IsMatch(File.ReadAllText(f), @"(src|href)=""https?://")).Select(Path.GetFileName).ToList();
        Assert.True(problems.Count == 0, "Pages with an outside resource: " + string.Join(", ", problems));
    }

    [Fact]
    public void Every_icon_a_page_names_is_in_the_sprite()
    {
        var problems = new List<string>();
        foreach (var file in Files("*.cshtml"))
        {
            var text = File.ReadAllText(file);
            var names = IconCall().Matches(text).Select(m => m.Groups[1].Value).Concat(SpriteReference().Matches(text).Select(m => m.Groups[1].Value));
            problems.AddRange(names.Distinct().Where(n => !RegistryTests.Sprite.Contains(n)).Select(n => $"{Path.GetFileName(file)}: {n}"));
        }

        Assert.True(problems.Count == 0, "Icons that are not in matui-icons.svg:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Every_icon_of_the_menu_the_catalogues_and_the_skeleton_is_in_the_sprite()
    {
        var names = Navigation.Sets.SelectMany(s => s.AllItems).Select(i => i.Icon).OfType<string>()
            .Concat(Navigation.Sets.Select(s => s.Icon))
            .Concat(ComponentCatalog.All.Select(c => c.Icon))
            .Concat(PatternCatalog.All.Select(p => p.Icon))
            .Concat(SpriteReference().Matches(ShellSkeleton.Html).Select(m => m.Groups[1].Value))
            .Distinct();
        Assert.All(names, n => Assert.True(RegistryTests.Sprite.Contains(n), $"icon '{n}' is not in matui-icons.svg"));
    }

    [Fact]
    public void Every_item_of_the_menu_has_a_page()
    {
        foreach (var item in Navigation.Pages)
        {
            var path = item.Href!.Split('?')[0].Trim('/').Replace('/', Path.DirectorySeparatorChar);
            var direct = path.Length == 0 ? TestPaths.Web("Pages/Index.cshtml") : TestPaths.Web($"Pages/{path}.cshtml");
            var nested = TestPaths.Web($"Pages/{path}/Index.cshtml");
            Assert.True(File.Exists(direct) || File.Exists(nested), $"no page for the menu item {item.Title} ({item.Href})");
        }
    }

    // ---------------------------------------------------------------------------------------------------- the catalogues

    [Fact]
    public void Component_ids_are_unique_and_in_a_known_group()
    {
        Assert.Equal(ComponentCatalog.All.Count, ComponentCatalog.All.Select(c => c.Id).Distinct().Count());
        Assert.All(ComponentCatalog.All, c => Assert.Contains(c.Group, ComponentCatalog.Groups));
        Assert.All(ComponentCatalog.Groups, g => Assert.Contains(ComponentCatalog.All, c => c.Group == g));
        Assert.All(ComponentCatalog.All, c => Assert.NotEmpty(c.Examples));
        Assert.All(ComponentCatalog.All, c => Assert.NotEmpty(c.Rules));
    }

    [Fact]
    public void Every_rule_a_component_or_pattern_cites_exists()
    {
        foreach (var rule in ComponentCatalog.All.SelectMany(c => c.Rules.Select(r => (c.Id, r))).Concat(PatternCatalog.All.SelectMany(p => p.Rules.Select(r => (p.Id, r)))).Concat(PatternCatalog.All.SelectMany(p => p.Parts.Where(x => x.Rule is not null).Select(x => (p.Id, x.Rule!)))))
        {
            Assert.True(RuleBook.Get(rule.Item2) is not null, $"{rule.Id} cites {rule.Item2}, which is not in the rulebook");
        }
    }

    [Fact]
    public void Examples_use_only_classes_the_toolkit_ships()
    {
        var problems = new List<string>();
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var token in component.Examples.SelectMany(e => ClassAttribute().Matches(e.Html).SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))).Distinct())
            {
                if (!ToolkitClasses.Value.Contains(token))
                {
                    problems.Add($"{component.Id}: .{token}");
                }
            }
        }

        Assert.True(problems.Count == 0, "Examples with classes the toolkit does not have:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Examples_have_no_inline_styles_handlers_or_scripts()
    {
        var problems = ComponentCatalog.All.SelectMany(c => c.Examples.Select(e => (c.Id, e.Html)))
            .Where(x => InlineStyle().IsMatch(x.Html) || InlineHandler().IsMatch(x.Html) || InlineScript().IsMatch(x.Html) || x.Html.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id).ToList();
        Assert.True(problems.Count == 0, "Examples that break CODE-02: " + string.Join(", ", problems));
    }

    [Fact]
    public void Every_icon_an_example_uses_is_in_the_sprite()
    {
        var names = ComponentCatalog.All.SelectMany(c => c.Examples).SelectMany(e => SpriteReference().Matches(e.Html).Select(m => m.Groups[1].Value)).Distinct();
        Assert.All(names, n => Assert.True(RegistryTests.Sprite.Contains(n), $"icon '{n}' is not in matui-icons.svg"));
    }

    [Fact]
    public void Every_cell_of_an_example_table_has_a_label()
    {
        // TABLE-02: on a phone the label is what the card line shows.
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var example in component.Examples)
            {
                foreach (Match cell in TableCell().Matches(example.Html))
                {
                    var attributes = cell.Groups[1].Value;
                    Assert.True(attributes.Contains("data-label=") || attributes.Contains("mat-table__cell-actions"), $"{component.Id}: a <td> without data-label: {cell.Value}");
                }
            }
        }
    }

    [Fact]
    public void Every_button_of_an_example_has_a_type()
    {
        // A button without a type submits the form it happens to sit in.
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var button in component.Examples.SelectMany(e => Button().Matches(e.Html)))
            {
                Assert.True(button.Groups[1].Value.Contains("type=\""), $"{component.Id}: {button.Value}");
            }
        }
    }

    [Fact]
    public void Every_control_of_an_example_has_an_id_and_the_ids_of_a_component_are_unique()
    {
        // FORM-08
        foreach (var component in ComponentCatalog.All)
        {
            var ids = component.Examples.SelectMany(e => IdAttribute().Matches(e.Html).Select(m => m.Groups[1].Value)).ToList();
            Assert.True(ids.Count == ids.Distinct().Count(), $"{component.Id}: ids twice: {string.Join(", ", ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key))}");

            foreach (var control in component.Examples.SelectMany(e => Control().Matches(e.Html)))
            {
                if (control.Groups[2].Value.Contains("type=\"hidden\""))
                {
                    continue;
                }

                Assert.True(control.Groups[2].Value.Contains(" id=\"") || control.Groups[2].Value.StartsWith(" id=\""), $"{component.Id}: a control without an id: {control.Value}");
            }
        }
    }

    [Fact]
    public void References_between_the_elements_of_an_example_resolve()
    {
        var references = new[] { @"\sfor=""([^""]+)""", @"aria-labelledby=""([^""]+)""", @"aria-controls=""([^""]+)""", @"data-dialog-open=""([^""]+)""", @"data-menu-toggle=""([^""]+)""", @"\sform=""([^""]+)""", @"data-copy=""#([^""]+)""" };
        foreach (var component in ComponentCatalog.All)
        {
            var html = string.Join("\n", component.Examples.Select(e => e.Html));
            var ids = IdAttribute().Matches(html).Select(m => m.Groups[1].Value).ToHashSet();
            foreach (var pattern in references)
            {
                foreach (Match reference in Regex.Matches(html, pattern))
                {
                    Assert.True(ids.Contains(reference.Groups[1].Value), $"{component.Id}: {reference.Value} points at nothing");
                }
            }
        }
    }

    [Fact]
    public void Every_dialog_of_an_example_is_named_and_can_be_closed()
    {
        // DIALOG-04, DIALOG-03: a title that names it, and the X.
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var html in component.Examples.Select(e => e.Html).Where(h => h.Contains("<dialog")))
            {
                var dialogs = Regex.Matches(html, @"<dialog\b[^>]*aria-labelledby=""[^""]+""[^>]*>");
                Assert.Equal(Regex.Matches(html, "<dialog\\b").Count, dialogs.Count);
                Assert.Equal(Regex.Matches(html, "<dialog\\b").Count, Regex.Matches(html, @"mat-dialog__close[^>]*aria-label=""Close""").Count);
                Assert.Equal(Regex.Matches(html, "<dialog\\b").Count, Regex.Matches(html, @"class=""mat-dialog__title""").Count);
            }
        }
    }

    [Fact]
    public void Every_icon_only_button_of_an_example_has_an_accessible_name()
    {
        // ICON-03: a button whose only content is an icon has an aria-label.
        var checkedButtons = 0;
        foreach (var component in ComponentCatalog.All)
        {
            foreach (Match button in Regex.Matches(string.Join("\n", component.Examples.Select(e => e.Html)), @"<(?:button|a)\b([^>]*)>\s*<svg[^>]*><use[^>]*></use></svg>\s*</(?:button|a)>"))
            {
                Assert.Contains("aria-label=", button.Groups[1].Value);
                checkedButtons++;
            }
        }

        Assert.True(checkedButtons >= 5, $"the pattern found only {checkedButtons} icon-only buttons: the test no longer looks at anything");
    }

    [Fact]
    public void Example_markup_is_balanced()
    {
        foreach (var component in ComponentCatalog.All)
        {
            foreach (var example in component.Examples)
            {
                foreach (var tag in new[] { "div", "span", "section", "table", "tbody", "tr", "td", "ul", "ol", "li", "form", "dialog", "nav", "button", "label", "dl", "pre" })
                {
                    var open = Regex.Matches(example.Html, $@"<{tag}\b").Count;
                    var close = Regex.Matches(example.Html, $@"</{tag}>").Count;
                    Assert.True(open == close, $"{component.Id} / {example.Title}: {open} <{tag}> but {close} </{tag}>");
                }
            }
        }
    }

    [Fact]
    public void An_example_that_is_a_script_names_the_demo_that_runs_it()
    {
        foreach (var example in ComponentCatalog.All.SelectMany(c => c.Examples).Where(e => e.Js is not null))
        {
            Assert.StartsWith("MatUi.", example.Js!.TrimStart().Replace("const ok = await ", "").Replace("const name = await ", ""));
            Assert.False(string.IsNullOrEmpty(example.Demo));
        }
    }

    [Fact]
    public void Pattern_ids_are_unique_and_every_pattern_has_a_frame_to_show()
    {
        var views = new HashSet<string> { "list", "gallery", "form", "settings", "dashboard", "auth" };
        Assert.Equal(PatternCatalog.All.Count, PatternCatalog.All.Select(p => p.Id).Distinct().Count());
        Assert.All(PatternCatalog.All, p => Assert.Contains(p.View, views));
        Assert.All(PatternCatalog.All, p => Assert.Contains(p.Kind, PatternCatalog.Kinds));
        Assert.All(PatternCatalog.All, p => Assert.NotEmpty(p.Parts));
        Assert.All(PatternCatalog.Apps, a => Assert.NotNull(AppRegistry.Get(a)));
    }

    [Fact]
    public void The_toolbar_dialogs_of_the_partial_name_every_control_with_an_id()
    {
        // FORM-08 holds for the real toolbar too: every radio has an id.
        var toolbar = File.ReadAllText(TestPaths.Web("Pages/Shared/_Toolbar.cshtml"));
        foreach (Match input in Regex.Matches(toolbar, @"<input\b[^>]*>"))
        {
            if (input.Value.Contains("type=\"hidden\""))
            {
                continue;
            }

            Assert.Contains(" id=\"", input.Value);
        }
    }
}
