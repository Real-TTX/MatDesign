using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>The renamer of the white label build (CODE-09): every rule, every trap, validation, idempotence, determinism. No site, no files: text in, text out.</summary>
public sealed class WhiteLabelTests
{
    private static readonly WhiteLabelSetting Acme = new("acme", "AcmeUi");

    private static string R(string text) => WhiteLabel.Rename(text, Acme);

    // ------------------------------------------------------------------------------------------------------ validation

    [Theory]
    [InlineData("acme", "AcmeUi")]
    [InlineData("ac", "Acme")]
    [InlineData("abcdefgh", "Zed")]
    [InlineData("a1b2", "AcmeDesignSystemToolkitX")]
    [InlineData("mat", "MatUi")]
    [InlineData("mat", "AcmeUi")]
    [InlineData("matx", "Matrix")]
    public void A_usable_setting_is_valid(string prefix, string name)
        => Assert.Empty(WhiteLabel.Validate(new WhiteLabelSetting(prefix, name)));

    [Theory]
    [InlineData("", "prefix")]
    [InlineData("a", "prefix")]
    [InlineData("abcdefghi", "prefix")]
    [InlineData("Acme", "prefix")]
    [InlineData("1ab", "prefix")]
    [InlineData("ac-me", "prefix")]
    [InlineData("ac_me", "prefix")]
    [InlineData("ac me", "prefix")]
    [InlineData("data", "prefix")]
    [InlineData("aria", "prefix")]
    [InlineData("js", "prefix")]
    [InlineData("is", "prefix")]
    [InlineData("has", "prefix")]
    [InlineData("on", "prefix")]
    [InlineData("ng", "prefix")]
    [InlineData("xml", "prefix")]
    [InlineData("xmlns", "prefix")]
    [InlineData("xmlfoo", "prefix")]
    [InlineData("matui", "prefix")]
    public void A_prefix_the_browser_or_the_toolkit_would_misread_is_refused(string prefix, string field)
    {
        var problems = WhiteLabel.Validate(new WhiteLabelSetting(prefix, "AcmeUi"));
        Assert.Contains(problems, p => p.Field.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ac")]
    [InlineData("acmeui")]
    [InlineData("Acme Ui")]
    [InlineData("Acme-Ui")]
    [InlineData("Acme2")]
    [InlineData("AcmeUiAcmeUiAcmeUiAcmeUiAcmeUi")]
    [InlineData("MatUi")]
    [InlineData("MATUI")]
    [InlineData("Matui")]
    [InlineData("Mat")]
    [InlineData("MAT")]
    public void A_name_that_is_not_a_capitalised_word_or_is_taken_for_the_toolkit_is_refused(string name)
        => Assert.Contains(WhiteLabel.Validate(new WhiteLabelSetting("acme", name)), p => p.Field == "Name");

    [Fact]
    public void The_toolkits_own_prefix_and_name_are_valid_together_and_rename_nothing()
    {
        var none = WhiteLabelSetting.None;
        Assert.Empty(WhiteLabel.Validate(none));
        Assert.True(none.IsNoOp);
        Assert.False(Acme.IsNoOp);
        Assert.False(new WhiteLabelSetting("mat", "AcmeUi").IsNoOp);
        Assert.False(new WhiteLabelSetting("acme", "MatUi").IsNoOp);
        Assert.Equal("acmeui", Acme.FileStem);
    }

    [Fact]
    public void A_setting_is_normalised_trimmed_with_the_prefix_in_lower_case_and_empty_means_the_toolkits_own()
    {
        Assert.Equal(new WhiteLabelSetting("acme", "AcmeUi"), WhiteLabel.Normalize(new WhiteLabelSetting("  ACME ", " AcmeUi ")));
        Assert.Equal(WhiteLabelSetting.None, WhiteLabel.Normalize(new WhiteLabelSetting("", "  ")));
    }

    // ------------------------------------------------------------------------------------------------------ the rules

    [Theory]
    [InlineData("<button class=\"mat-btn mat-btn--primary\">", "<button class=\"acme-btn acme-btn--primary\">")]
    [InlineData(".mat-card__head > .mat-card__title { }", ".acme-card__head > .acme-card__title { }")]
    [InlineData("<div id=\"mat-nav\">", "<div id=\"acme-nav\">")]
    [InlineData("@keyframes mat-spin { } animation: mat-spin 1s", "@keyframes acme-spin { } animation: acme-spin 1s")]
    [InlineData("el.className = 'mat-toast' + (o.tone ? ' mat-toast--' + o.tone : '');", "el.className = 'acme-toast' + (o.tone ? ' acme-toast--' + o.tone : '');")]
    [InlineData("$('.mat-item__options--' + side + ' .mat-item__option', item)", "$('.acme-item__options--' + side + ' .acme-item__option', item)")]
    public void Classes_ids_and_animations_get_the_prefix(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("padding: var(--mat-space-4); color: var(--mat-text)", "padding: var(--acme-space-4); color: var(--acme-text)")]
    [InlineData(":root { --mat-radius: 8px; }", ":root { --acme-radius: 8px; }")]
    [InlineData("root.style.setProperty(\"--mat-custom-accent\", hex);", "root.style.setProperty(\"--acme-custom-accent\", hex);")]
    [InlineData("el.style.setProperty('--mat-sch-' + n, v)", "el.style.setProperty('--acme-sch-' + n, v)")]
    [InlineData("calc(var(--mat-a) + var(--mat-b))", "calc(var(--acme-a) + var(--acme-b))")]
    public void Custom_properties_get_the_prefix_also_when_the_name_is_built_in_parts(string before, string after) => Assert.Equal(after, R(before));

    [Fact]
    public void Only_data_attributes_that_start_with_data_mat_are_renamed_not_the_ones_that_merely_contain_it()
    {
        Assert.Equal("root.getAttribute('data-acme-sprite')", R("root.getAttribute('data-mat-sprite')"));
        Assert.Equal("<i data-matter=\"1\" data-format-mat=\"2\" data-x-mat-y=\"3\">", R("<i data-matter=\"1\" data-format-mat=\"2\" data-x-mat-y=\"3\">"));
    }

    [Theory]
    [InlineData("root.dispatchEvent(new CustomEvent('mat:theme', { detail: 1 }))", "root.dispatchEvent(new CustomEvent('acme:theme', { detail: 1 }))")]
    [InlineData("el.addEventListener(\"mat:otp-complete\", f)", "el.addEventListener(\"acme:otp-complete\", f)")]
    [InlineData("// sends mat:item-action and mat:reorder", "// sends acme:item-action and acme:reorder")]
    public void Events_get_the_prefix(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("localStorage.setItem('mat-theme', theme)", "localStorage.setItem('acme-theme', theme)")]
    [InlineData("localStorage.getItem('mat-view-' + key)", "localStorage.getItem('acme-view-' + key)")]
    [InlineData("localStorage.getItem('mat-accent'); localStorage.getItem('mat-scheme')", "localStorage.getItem('acme-accent'); localStorage.getItem('acme-scheme')")]
    [InlineData("sessionStorage.setItem(\"mat-groups\", s)", "sessionStorage.setItem(\"acme-groups\", s)")]
    [InlineData("document.cookie = \"mat-theme=dark; path=/\"", "document.cookie = \"acme-theme=dark; path=/\"")]
    public void The_keys_of_storage_and_the_names_of_cookies_get_the_prefix(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("<use href=\"#mat-i-check\"></use>", "<use href=\"#acme-i-check\"></use>")]
    [InlineData("'<use href=\"' + sprite + '#mat-i-' + name + '\"></use>'", "'<use href=\"' + sprite + '#acme-i-' + name + '\"></use>'")]
    [InlineData("<symbol id=\"mat-i-alert\" viewBox=\"0 0 24 24\">", "<symbol id=\"acme-i-alert\" viewBox=\"0 0 24 24\">")]
    [InlineData("<symbol id=\"mat-il-lamp\">", "<symbol id=\"acme-il-lamp\">")]
    [InlineData("/matui-iconlib.svg#mat-il-lamp", "/acmeui-iconlib.svg#acme-il-lamp")]
    public void The_ids_of_the_sprite_get_the_prefix(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("w._matUpload = x; el._matPicker", "w._acmeUpload = x; el._acmePicker")]
    [InlineData("if (item && item._matSlide) { }", "if (item && item._acmeSlide) { }")]
    public void The_private_properties_of_the_script_get_the_prefix(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("window.MatUi = { version: '1' };", "window.AcmeUi = { version: '1' };")]
    [InlineData("MatUi.dialog.open(el); MatUi.toast('x')", "AcmeUi.dialog.open(el); AcmeUi.toast('x')")]
    [InlineData("/* MatUi 0.1.0 (abc) - build x */", "/* AcmeUi 0.1.0 (abc) - build x */")]
    [InlineData("MatUi · tokens", "AcmeUi · tokens")]
    [InlineData("The toolkit MatUi", "The toolkit AcmeUi")]
    public void The_product_name_replaces_MatUi(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("matui.css", "acmeui.css")]
    [InlineData("matui.js", "acmeui.js")]
    [InlineData("matui.boot.js", "acmeui.boot.js")]
    [InlineData("matui-icons.svg", "acmeui-icons.svg")]
    [InlineData("matui.manifest.json", "acmeui.manifest.json")]
    [InlineData("matui-0.1.0.zip", "acmeui-0.1.0.zip")]
    [InlineData("css/matui.tokens.css", "css/acmeui.tokens.css")]
    [InlineData("iconlib/matui-iconlib.svg", "iconlib/acmeui-iconlib.svg")]
    [InlineData("matui/matui.css", "acmeui/acmeui.css")]
    [InlineData("<link rel=\"stylesheet\" href=\"/matui.css\" />", "<link rel=\"stylesheet\" href=\"/acmeui.css\" />")]
    [InlineData("<script src=\"matui.boot.js\"></script>", "<script src=\"acmeui.boot.js\"></script>")]
    [InlineData("see matui.js and matui.boot.js.", "see acmeui.js and acmeui.boot.js.")]
    public void The_file_names_follow_the_name_in_lower_case(string before, string after) => Assert.Equal(after, R(before));

    [Fact]
    public void File_names_are_renamed_as_paths_with_the_same_rules()
    {
        Assert.Equal("css/acmeui.apps.css", WhiteLabel.RenamePath("css/matui.apps.css", Acme));
        Assert.Equal("acmeui-1.2.3.zip", WhiteLabel.RenamePath("matui-1.2.3.zip", Acme));
        Assert.Equal("CHANGELOG.md", WhiteLabel.RenamePath("CHANGELOG.md", Acme));
        Assert.Equal("design.json", WhiteLabel.RenamePath("design.json", Acme));
        Assert.Equal("styleguide.html", WhiteLabel.RenamePath("styleguide.html", Acme));
    }

    // ------------------------------------------------------------------------------------------------------ the traps: what is not a name of the toolkit

    [Theory]
    [InlineData("format-money")]
    [InlineData("a format-")]
    [InlineData("automat-1")]
    [InlineData("thermat-x")]
    [InlineData("Mattiverse")]
    [InlineData("mattiverse-sync")]
    [InlineData("Matdo")]
    [InlineData("MatMail MatPaper MatSplit MatCMS Matmon")]
    [InlineData("MatDesign")]
    [InlineData("the Mat apps and matdesign")]
    [InlineData("Math.max(1, 2)")]
    [InlineData("a.match(/x/); Mismatch; formatSize; animationX")]
    [InlineData("_matter and _mat and _mat_x and x_matUp")]
    [InlineData("MatUiVersion in Directory.Build.props")]
    [InlineData("MatUi2 and MatUis and MatUi_x")]
    [InlineData("rematui and matuix and matui2")]
    [InlineData("something-mat-else and a--mat- and x-mat-y")]
    [InlineData("mat: a space after the colon, and mat:1")]
    [InlineData("$mat-x and _mat-x and 9mat-x")]
    public void A_name_that_is_part_of_another_word_or_is_another_name_is_not_touched(string text) => Assert.Equal(text, R(text));

    [Fact]
    public void An_address_with_a_scheme_is_never_touched_but_a_relative_address_of_the_toolkit_is()
    {
        const string url = "see https://example.com/mat-docs/matui.css?x=mat-1#MatUi and http://localhost:8060/ui/matui.js done";
        Assert.Equal(url, R(url));
        Assert.Equal("<a href=\"https://example.com/mat-btn\">x</a> <a class=\"acme-btn\">", R("<a href=\"https://example.com/mat-btn\">x</a> <a class=\"mat-btn\">"));
        Assert.Equal("href=\"/ui/acmeui.css\"", R("href=\"/ui/matui.css\""));
    }

    [Fact]
    public void The_tool_of_the_repository_keeps_its_name_in_the_README_of_a_copy()
    {
        const string line = "  To see how an app's copy stands against the toolkit:  node tools/matui-check.js <folder of the app>";
        Assert.Equal(line, R(line));
        Assert.Equal("node tools/matui-check.js and acmeui.css", R("node tools/matui-check.js and matui.css"));
    }

    [Theory]
    [InlineData("[class*=\"mat-\"]", "[class*=\"acme-\"]")]
    [InlineData("[class^=\"mat-\"], [class*=' mat-']", "[class^=\"acme-\"], [class*=' acme-']")]
    [InlineData(":is(.mat-a, .mat-b) > .mat-c:not(.mat-d)", ":is(.acme-a, .acme-b) > .acme-c:not(.acme-d)")]
    [InlineData("el.querySelector('.mat-dialog[open] .mat-dialog__title')", "el.querySelector('.acme-dialog[open] .acme-dialog__title')")]
    [InlineData("el.closest('.mat-field, .mat-check')", "el.closest('.acme-field, .acme-check')")]
    [InlineData("el.classList.contains('mat-rating--' + cls)", "el.classList.contains('acme-rating--' + cls)")]
    [InlineData("new RegExp('^mat-(.+)$')", "new RegExp('^acme-(.+)$')")]
    [InlineData("var re = /^mat-[a-z]+$/;", "var re = /^acme-[a-z]+$/;")]
    [InlineData("`<div class=\"mat-x ${y}\"></div>`", "`<div class=\"acme-x ${y}\"></div>`")]
    public void Selectors_regular_expressions_and_template_strings_are_renamed_like_any_text(string before, string after) => Assert.Equal(after, R(before));

    [Theory]
    [InlineData("&quot;mat-btn&quot;", "&quot;acme-btn&quot;")]
    [InlineData("class=&quot;mat-btn mat-btn--primary&quot;", "class=&quot;acme-btn acme-btn--primary&quot;")]
    [InlineData("&#39;mat-theme&#39;", "&#39;acme-theme&#39;")]
    [InlineData("&lt;span class=&quot;mat-pill&quot;&gt;", "&lt;span class=&quot;acme-pill&quot;&gt;")]
    public void Html_entities_in_front_of_a_name_are_a_boundary(string before, string after) => Assert.Equal(after, R(before));

    [Fact]
    public void A_json_text_is_renamed_as_strings_because_the_serializer_writes_a_quote_as_u0022()
    {
        var model = new { html = "<button class=\"mat-btn\" data-mat-x=\"1\">MatUi</button>", js = "MatUi.toast('mat:theme')", light = new Dictionary<string, string> { ["--mat-radius"] = "8px" }, count = 3, flag = true, nothing = (string?)null };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var json = JsonSerializer.Serialize(model, options);

        // as text the name stands behind the 2 of \u0022: it looks like the middle of a word, and a plain text rename would leave it
        Assert.Contains("\\u0022mat-btn", json);
        Assert.Contains("\\u0022mat-btn", R(json));

        var renamed = WhiteLabel.RenameJson(json, Acme, options);
        Assert.DoesNotContain("mat-btn", renamed.Text);
        Assert.Contains("\\u0022acme-btn\\u0022", renamed.Text);
        using var doc = JsonDocument.Parse(renamed.Text);
        Assert.Equal("<button class=\"acme-btn\" data-acme-x=\"1\">AcmeUi</button>", doc.RootElement.GetProperty("html").GetString());
        Assert.Equal("AcmeUi.toast('acme:theme')", doc.RootElement.GetProperty("js").GetString());
        Assert.Equal("8px", doc.RootElement.GetProperty("light").GetProperty("--acme-radius").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("count").GetInt32());
        Assert.True(doc.RootElement.GetProperty("flag").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("nothing").ValueKind);
        Assert.Equal(6, renamed.Total);
    }

    [Fact]
    public void A_json_rename_that_renames_nothing_returns_the_text_as_it_was()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var json = JsonSerializer.Serialize(new { a = "mat-x" }, options);
        Assert.Equal(json, WhiteLabel.RenameJson(json, WhiteLabelSetting.None, options).Text);
    }

    [Fact]
    public void Adjacent_names_in_one_text_are_all_renamed_and_nothing_is_renamed_twice()
    {
        Assert.Equal("acme-a acme-b--c --acme-d acme:e data-acme-f _acmeG AcmeUi acmeui", R("mat-a mat-b--c --mat-d mat:e data-mat-f _matG MatUi matui"));
        // a prefix that contains the first letters of another rule's name is not looked at again
        Assert.Equal("matx-btn", WhiteLabel.Rename("mat-btn", new WhiteLabelSetting("matx", "AcmeUi")));
        Assert.Equal("m1-btn --m1-x", WhiteLabel.Rename("mat-btn --mat-x", new WhiteLabelSetting("m1", "AcmeUi")));
    }

    [Fact]
    public void Each_rename_counts_what_it_replaced_by_rule()
    {
        var result = WhiteLabel.RenameCounted("mat-a mat-b --mat-c data-mat-d mat:e _matF MatUi MatUi matui.css format-x https://x/mat-y", Acme);
        Assert.Equal(2, result.Counts["class"]);
        Assert.Equal(1, result.Counts["property"]);
        Assert.Equal(1, result.Counts["data"]);
        Assert.Equal(1, result.Counts["event"]);
        Assert.Equal(1, result.Counts["expando"]);
        Assert.Equal(2, result.Counts["name"]);
        Assert.Equal(1, result.Counts["file"]);
        Assert.Equal(9, result.Total);
    }

    [Theory]
    [InlineData("acme", "AcmeUi")]
    [InlineData("ac", "Acme")]
    [InlineData("abcdefgh", "Zed")]
    public void The_length_of_a_text_changes_by_exactly_what_its_replacements_add(string prefix, string name)
    {
        var setting = new WhiteLabelSetting(prefix, name);
        const string text = "mat-a --mat-b data-mat-c mat:d _matE MatUi matui.css";
        var result = WhiteLabel.RenameCounted(text, setting);
        var delta = (prefix.Length - 3) * 5 + (name.Length - 5) + (name.Length - 5);
        Assert.Equal(text.Length + delta, result.Text.Length);
    }

    // ------------------------------------------------------------------------------------------------------ the part of the setting that is not renamed, no-op, idempotence, determinism

    [Fact]
    public void The_toolkits_own_prefix_and_name_change_nothing_not_a_byte()
    {
        var text = File.ReadAllText(TestPaths.Toolkit("js/matui.js")) + File.ReadAllText(TestPaths.Toolkit("css/matui.shell.css"));
        Assert.Same(text, WhiteLabel.Rename(text, WhiteLabelSetting.None));
        Assert.Equal(0, WhiteLabel.RenameCounted(text, WhiteLabelSetting.None).Total);
    }

    [Fact]
    public void Renaming_only_the_name_leaves_the_prefix_and_renaming_only_the_prefix_leaves_the_name()
    {
        Assert.Equal("mat-btn AcmeUi acmeui.css --mat-x", WhiteLabel.Rename("mat-btn MatUi matui.css --mat-x", new WhiteLabelSetting("mat", "AcmeUi")));
        Assert.Equal("acme-btn MatUi matui.css --acme-x", WhiteLabel.Rename("mat-btn MatUi matui.css --mat-x", new WhiteLabelSetting("acme", "MatUi")));
    }

    [Fact]
    public void A_rename_is_idempotent_and_deterministic_on_the_real_files()
    {
        foreach (var file in new[] { "js/matui.js", "js/matui.boot.js", "css/matui.tokens.css", "css/matui.controls.css", "css/matui.forms.css", "img/matui-icons.svg" })
        {
            var text = File.ReadAllText(TestPaths.Toolkit(file));
            var once = R(text);
            Assert.Equal(once, R(text));
            Assert.Equal(once, R(once));
            Assert.NotEqual(text, once);
            Assert.Empty(WhiteLabel.Leftovers(once, Acme));
        }
    }

    [Fact]
    public void Leftovers_finds_what_a_rename_missed_with_the_line_and_ignores_addresses_and_the_tool()
    {
        var found = WhiteLabel.Leftovers("a { }\n.acme-x { }\n.mat-y { }\nhttps://x/mat-z\nnode tools/matui-check.js\nMatUi", Acme);
        Assert.Equal([(3, "class"), (6, "name")], found.Select(f => (f.Line, f.Rule)).ToArray());
        Assert.Empty(WhiteLabel.Leftovers(".mat-y MatUi", WhiteLabelSetting.None));
        Assert.Equal([(1, "class")], WhiteLabel.Leftovers(".mat-y MatUi", new WhiteLabelSetting("acme", "MatUi")).Select(f => (f.Line, f.Rule)).ToArray());
    }

    [Fact]
    public void The_page_shows_the_examples_with_the_names_of_the_setting()
    {
        var examples = WhiteLabel.Examples(Acme).ToDictionary(e => e.Before, e => e.After);
        Assert.Equal("acme-btn acme-btn--primary", examples["mat-btn mat-btn--primary"]);
        Assert.Equal("--acme-radius", examples["--mat-radius"]);
        Assert.Equal("window.AcmeUi", examples["window.MatUi"]);
        Assert.Equal("acmeui.css", examples["matui.css"]);
        Assert.Equal("acmeui-0.1.0.zip", examples["matui-0.1.0.zip"]);
        Assert.Equal(7, WhiteLabel.Rules(Acme).Count);
    }

    // ------------------------------------------------------------------------------------------------------ what the script really does with the names (the traps found in matui.js)

    [Fact]
    public void The_script_builds_no_name_of_the_toolkit_from_parts_that_a_rename_could_not_see()
    {
        var script = TestPaths.ReadToolkit("js/matui.js");
        // a name cut in two strings ('mat' + '-x', 'data-' + 'mat-x', 'm' + 'at-') would be missed: there is none
        Assert.DoesNotMatch(new Regex(@"['""`]mat['""`]"), script);
        Assert.DoesNotMatch(new Regex(@"['""`]data-['""`]\s*\+\s*['""`]mat"), script);
        Assert.DoesNotMatch(new Regex(@"['""`]m['""`]\s*\+\s*['""`]at"), script);
        Assert.DoesNotMatch(new Regex(@"['""`]ma['""`]\s*\+\s*['""`]t"), script);
        // the dynamic names keep the prefix whole at the start of a string ('mat-view-' + key, 'mat-stepper__btn--' + kind): there are some, and they are renamed like any other
        Assert.Matches(@"'mat-view-'\s*\+", script);
        Assert.Contains("'acme-view-' + key", R(script));
        Assert.Contains("acme-stepper__btn--' + kind", R(script));

        // the script does not cut the prefix off a name by its length (slice(4) ...), which a longer prefix would break
        Assert.DoesNotMatch(new Regex(@"\.(?:slice|substring|substr)\(\s*[3-5]\s*\)"), script);
        // no regular expression literal reads a name of the toolkit by character class tricks like [m]at-
        Assert.DoesNotMatch(new Regex(@"\[m\]at|m\[a\]t|ma\[t\]"), script);
        // the one data attribute that carries the name is read by its whole name
        Assert.Contains("'data-mat-sprite'", script);
    }

    [Fact]
    public void The_script_stylesheets_and_sprite_have_no_selector_on_a_part_of_the_prefix()
    {
        var css = string.Join("\n", MatDesign.Web.Services.ToolkitFiles.CssParts.Select(p => TestPaths.ReadToolkit("css/" + p)));
        Assert.DoesNotMatch(new Regex(@"\[class[*^$|~]?=[^\]]*mat"), css);
        Assert.DoesNotMatch(new Regex(@"\[id[*^$|~]?=[^\]]*mat"), css);
        Assert.DoesNotMatch(new Regex(@"\[data-[a-z-]*[*^$|~]=[^\]]*mat"), css);
        var script = TestPaths.ReadToolkit("js/matui.js");
        Assert.DoesNotMatch(new Regex(@"\[class[*^$|~]=[^\]]*mat"), script);
        // the escaped form of a name in a stylesheet (\. or \- in front of mat) does not occur either
        Assert.DoesNotMatch(new Regex(@"\\[.\-:]mat|mat\\[-:]"), css + script);
    }
}
