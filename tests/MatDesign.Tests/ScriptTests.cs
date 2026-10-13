using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>What the rulebook says about the toolkit's script, checked on the script itself.</summary>
public partial class ScriptTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"(?m)^\s*//.*$")]
    private static partial Regex LineComment();

    [GeneratedRegex(@"data-[a-z]+(?:-[a-z]+)*")]
    private static partial Regex DataAttribute();

    [GeneratedRegex(@"(?s)\ben: \{(.*?)\n    \},\n    de: \{(.*?)\n    \}\n  \};")]
    private static partial Regex TextTables();

    private static string Code => LineComment().Replace(BlockComment().Replace(Js, string.Empty), string.Empty);

    [Fact]
    public void No_browser_dialog_is_ever_opened()
    {
        // DIALOG-01: confirmations, alerts and inputs are dialogs of the toolkit, never window.confirm, alert or prompt.
        Assert.DoesNotMatch(@"window\.(confirm|alert|prompt)\s*\(", Code);
        Assert.DoesNotMatch(@"(?<![\w.$])(?:alert|confirm|prompt)\s*\(", Code);
    }

    [Fact]
    public void The_script_is_plain_and_dependency_free()
    {
        Assert.DoesNotMatch(@"(?m)^\s*(import|export)\s", Code);
        Assert.DoesNotContain("require(", Code);
        Assert.DoesNotContain("eval(", Code);
        Assert.DoesNotContain("new Function(", Code);
        Assert.DoesNotMatch(@"https?://", Code);
        Assert.Contains("'use strict'", Code);
    }

    [Fact]
    public void Every_documented_data_attribute_is_handled()
    {
        // The header lists the attributes an app can use. A documented attribute that nothing reads is a lie.
        var header = BlockComment().Match(Js).Value;
        var documented = DataAttribute().Matches(header).Select(m => m.Value).Where(v => v != "data-attributes").Distinct().ToList();
        Assert.True(documented.Count >= 12, $"only {documented.Count} attributes are documented");

        var code = Code;
        foreach (var attribute in documented)
        {
            Assert.True(code.Contains(attribute, StringComparison.Ordinal), $"{attribute} is documented in the header of matui.js but the script never reads it");
        }
    }

    [Theory]
    [InlineData("dialog")]
    [InlineData("confirm")]
    [InlineData("alert")]
    [InlineData("prompt")]
    [InlineData("toast")]
    [InlineData("theme")]
    [InlineData("scheme")]
    [InlineData("accent")]
    [InlineData("refresh")]
    public void The_public_api_has_its_documented_members(string member)
    {
        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains($"{member}:", api);
    }

    [Fact]
    public void The_theme_cycles_system_light_dark_and_system_means_no_attribute()
    {
        // SHELL-04
        Assert.Contains("var THEMES = ['system', 'light', 'dark'];", Js);
        Assert.Contains("root.removeAttribute('data-theme')", Js);
    }

    [Fact]
    public void The_texts_the_script_writes_are_in_the_tables_in_both_languages_and_an_app_can_change_them()
    {
        // The label of the theme button, the AM and PM of a time and the hint of a typed time are texts of PK_TEXT (English and German, kept complete by the picker
        // test), never a string in the code that draws them. <html data-theme-label> and data-theme-name-* replace the label.
        var tables = TextTables().Match(Js);
        Assert.True(tables.Success, "the text tables were not found");
        foreach (var language in new[] { tables.Groups[1].Value, tables.Groups[2].Value })
        {
            foreach (var key in new[] { "themeLabel", "themeSystem", "themeLight", "themeDark", "am", "pm", "phTimeEntry", "phTimeEntryAmPm" })
            {
                Assert.Matches($@"\b{key}: '", language);
            }
        }

        Assert.Equal(1, Regex.Count(Code, "Click for"));
        Assert.Equal(1, Regex.Count(Code, "Klicken für"));
        Assert.DoesNotContain("'Theme: '", Code);
        Assert.DoesNotContain("text: 'AM'", Code);
        Assert.Equal(2, Regex.Count(Code, "'hh:mm am'"));
        Assert.Contains("data-theme-label", Code);
        Assert.Contains("data-theme-name-", Code);
    }

    [Fact]
    public void A_preview_frame_does_not_save_its_theme_for_the_whole_site()
        => Assert.Contains("!root.hasAttribute('data-preview')", Js);

    [Fact]
    public void The_colour_scheme_is_chosen_saved_and_announced_like_the_theme_and_names_the_schemes_of_the_css()
    {
        // COLOR-09: data-scheme-set (aria-pressed follows), data-scheme-save-url (anti-forgery token like the theme), localStorage "mat-scheme" (not in a preview), the event mat:scheme.
        Assert.Contains("var SCHEMES = ['paper', 'contrast', 'oled'];", Js);
        Assert.Equal(ColorSchemes.All.Select(s => s.Id), Regex.Match(Js, @"var SCHEMES = \[([^\]]*)\]").Groups[1].Value.Split(',').Select(s => s.Trim().Trim('\'')));
        Assert.Contains("data-scheme-set", Code);
        Assert.Contains("data-scheme-save-url", Code);
        Assert.Contains("mat-scheme", Code);
        Assert.Contains("mat:scheme", Code);
        Assert.Contains("new URLSearchParams({ scheme: name })", Code);
        Assert.Contains("root.removeAttribute('data-scheme')", Code);

        var save = Js[Js.IndexOf("function chooseScheme", StringComparison.Ordinal)..];
        Assert.Contains("__RequestVerificationToken", save[..save.IndexOf("// ----", StringComparison.Ordinal)]);
        var apply = Js[Js.IndexOf("function applyScheme", StringComparison.Ordinal)..];
        Assert.Contains("!root.hasAttribute('data-preview')", apply[..apply.IndexOf("function chooseScheme", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Scrollbars_show_only_while_scrolling()
    {
        // SPACE-05
        Assert.Contains("is-scrolling", Js);
        Assert.Contains("setTimeout", Js);
    }

    [Fact]
    public void The_boot_script_only_applies_what_is_saved_and_is_tiny()
    {
        var boot = TestPaths.ReadToolkit("js/matui.boot.js");
        Assert.True(boot.Length < 1500, $"the boot script is {boot.Length} bytes");
        Assert.Contains("mat-theme", boot);
        Assert.Contains("hasAttribute('data-theme')", boot);
        Assert.Contains("mat-scheme", boot);
        Assert.Contains("hasAttribute('data-scheme')", boot);
        Assert.DoesNotContain("fetch(", boot);
    }

    [Fact]
    public void Dialogs_are_native_and_return_to_the_button_that_opened_them()
    {
        // DIALOG-05
        Assert.Contains("showModal", Js);
        Assert.Contains("opener", Js);
    }

    [Fact]
    public void A_submit_button_with_formnovalidate_skips_the_check_of_the_form_before_anything_is_checked_and_it_is_documented()
    {
        // FORM-05: the check of the toolkit replaces the one of the browser, so it leaves alone what the browser leaves alone. The browser checks e.submitter as well, and a form with data-confirm
        // submits again after the question, so the button that was pressed has to go along.
        var start = Js.IndexOf("form.matches('form.mat-form, form[data-validate]')", StringComparison.Ordinal);
        Assert.True(start > 0, "the submit check of the forms was not found");
        var handler = Js[start..Js.IndexOf("}, true);", start, StringComparison.Ordinal)];
        Assert.Contains("e.submitter && e.submitter.hasAttribute('formnovalidate')", handler);
        Assert.True(handler.IndexOf("formnovalidate", StringComparison.Ordinal) < handler.IndexOf("checkValidity", StringComparison.Ordinal), "formnovalidate is looked at after the fields were checked");
        Assert.Contains("form.requestSubmit(e.submitter || undefined)", Js);

        Assert.Contains("formnovalidate", BlockComment().Match(Js).Value);
        Assert.Contains("formnovalidate", RuleBook.All.Single(r => r.Id == "FORM-05").Statement);
        Assert.Contains("formnovalidate", File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md")));
        Assert.Contains(ComponentCatalog.All.Single(c => c.Id == "validation").Examples, e => e.Html.Contains("formnovalidate", StringComparison.Ordinal));
    }

    [Fact]
    public void The_toasts_live_in_the_topmost_open_dialog_and_go_back_to_the_page_when_it_closes_and_it_is_documented()
    {
        // SHELL-12: a modal dialog is in the top layer, so a toast outside of it is hidden behind it, whatever its z-index. The host is placed when a toast is shown, when a dialog
        // opens and in the close handler (which runs before a dialog that the script built removes itself: with the host inside it the toasts would go with it).
        Assert.Contains("var dialogStack = [];", Js);
        Assert.DoesNotContain("openDialogs", Js);

        var open = Js[Js.IndexOf("function openDialog(", StringComparison.Ordinal)..];
        Assert.Contains("dialogStack.push(d);", open[..open.IndexOf("function closeDialog(", StringComparison.Ordinal)]);
        Assert.Contains("placeToasts();", open[..open.IndexOf("function closeDialog(", StringComparison.Ordinal)]);

        var closed = Js[Js.IndexOf("doc.addEventListener('close'", StringComparison.Ordinal)..];
        closed = closed[..closed.IndexOf("}, true);", StringComparison.Ordinal)];
        Assert.Contains("dialogStack.splice(at, 1);", closed);
        Assert.Contains("placeToasts();", closed);

        var toast = Js[Js.IndexOf("function toast(", StringComparison.Ordinal)..];
        Assert.Contains("toastHost()", toast[..toast.IndexOf("host.appendChild(t);", StringComparison.Ordinal)]);
        Assert.DoesNotContain("doc.body.appendChild(host)", Js);

        Assert.Contains("top layer", File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md")));
        Assert.Contains(ComponentCatalog.All.Single(c => c.Id == "toasts").Behaviour, b => b.Contains("topmost", StringComparison.Ordinal));
    }

    [Fact]
    public void The_item_of_a_ticked_check_is_marked_whether_it_is_a_row_a_tile_or_a_card_and_it_is_documented()
    {
        // data-selectable is not only for the rows of a table: the item is the nearest tr, .mat-tile or [data-select-item], and it has to be inside the box that is selectable (a table
        // in a cell of another table must not mark that cell's row). The mark is a tint behind the whole item (COLOR-08, OneSidedAccentTests scans the CSS for a bar).
        var update = Js[Js.IndexOf("function updateSelection(", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("doc.addEventListener('change'", StringComparison.Ordinal)];
        Assert.Contains("r.closest('tr, .mat-tile, [data-select-item]')", update);
        Assert.Contains("box.contains(item)", update);
        Assert.Contains("item.classList.toggle('is-selected', r.checked)", update);

        Assert.Contains("data-select-item", BlockComment().Match(Js).Value);
        Assert.Contains("[data-select-item].is-selected { background: var(--mat-accent-soft); border-color: var(--mat-accent); }", TestPaths.ReadToolkit("css/matui.lists.css"));
        Assert.Contains("data-select-item", File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md")));

        var example = ComponentCatalog.All.Single(c => c.Id == "cards").Examples.Single(e => e.Title.StartsWith("Selectable cards", StringComparison.Ordinal));
        Assert.Equal(3, Regex.Matches(example.Html, "data-select-item").Count);
        Assert.Equal(3, Regex.Matches(example.Html, "data-select-row").Count);
        Assert.Contains("data-bulk-bar", example.Html);
    }
}
