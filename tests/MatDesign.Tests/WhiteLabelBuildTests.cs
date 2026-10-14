using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>
/// The white label build on the real files of the toolkit (CODE-09): a renamed copy for acme / AcmeUi is built in memory and every check of the toolkit is run on it; then the same checks are run on copies that
/// were deliberately broken, to show that they would have found what a faulty rename leaves behind.
/// </summary>
public sealed class WhiteLabelBuildTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly WhiteLabelSetting Acme = new("acme", "AcmeUi");

    private ReleaseService Releases => factory.Services.GetRequiredService<ReleaseService>();

    private ToolkitFiles Toolkit => factory.Services.GetRequiredService<ToolkitFiles>();

    private static readonly Dictionary<string, WhiteLabelBuildResult> Cache = [];

    private WhiteLabelBuildResult Trial(WhiteLabelSetting setting)
    {
        lock (Cache)
        {
            var key = $"{setting.Prefix}/{setting.Name}";
            if (!Cache.TryGetValue(key, out var result))
            {
                Cache[key] = result = Releases.TrialBuild(setting);
            }

            return result;
        }
    }

    private static string Text(WhiteLabelBuildResult result, string path) => result.Report.Single(f => f.Path == path).Text;

    private static IReadOnlyList<WhiteLabelFile> With(WhiteLabelBuildResult result, string path, Func<string, string> change)
        => result.Report.Select(f => f.Path == path ? f with { Text = change(f.Text) } : f).ToList();

    private static IReadOnlyList<WhiteLabelFinding> Findings(WhiteLabelBuildResult result, string path, Func<string, string> change)
        => WhiteLabelChecks.Run(result.Setting, With(result, path, change));

    // ------------------------------------------------------------------------------------------------------ the copy for acme

    [Fact]
    public void A_renamed_copy_for_acme_passes_every_check_of_the_toolkit()
    {
        var result = Trial(Acme);

        Assert.Empty(result.Findings);
        Assert.True(result.Passed);
        Assert.Equal(9, WhiteLabelChecks.All.Count);
        Assert.Equal("All 9 checks pass.", WhiteLabelChecks.Summary(result.Findings));
    }

    [Fact]
    public void The_copy_has_the_files_of_the_release_under_the_new_names_and_none_under_the_old()
    {
        var result = Trial(Acme);
        var paths = result.Files.Select(f => f.Path).ToList();

        foreach (var expected in new[]
        {
            "acmeui.css", "acmeui.js", "acmeui.boot.js", "acmeui-icons.svg", "CHANGELOG.md", "acmeui-0.1.0.zip", "iconlib/acmeui-iconlib.svg", "iconlib/iconlib.json", "iconlib/LICENSE.txt",
            "design.json", "styleguide.html", "styleguide.css", "sample.svg",
        })
        {
            Assert.Contains(expected, paths);
        }

        foreach (var part in ToolkitFiles.CssParts)
        {
            Assert.Contains("css/" + part.Replace("matui", "acmeui"), paths);
        }

        Assert.DoesNotContain(paths, p => p.Contains("matui", StringComparison.Ordinal));
    }

    [Fact]
    public void The_first_line_of_the_bundle_names_the_product_the_version_and_the_hash_of_the_copy()
    {
        var result = Trial(Acme);
        var first = Text(result, "acmeui.css").Split('\n')[0];

        Assert.Matches(new Regex($@"^/\* AcmeUi {Regex.Escape(AppVersion.MatUi)} \({result.Hash}\) - build .* - bundled from acmeui\.tokens\.css, "), first);
        Assert.NotEqual(Toolkit.Hash, result.Hash);
        Assert.Equal(Toolkit.Hash, result.SourceHash);
        Assert.DoesNotContain(Toolkit.Hash, string.Concat(result.Report.Select(f => f.Text)));

        // the hash is the toolkit's own formula over the renamed parts, script, boot script and sprite
        var parts = result.Report.Where(f => f.Path.StartsWith("css/", StringComparison.Ordinal)).Select(f => f.Text);
        Assert.Equal(result.Hash, WhiteLabelBuild.HashOf(parts, Text(result, "acmeui.js"), Text(result, "acmeui.boot.js"), Text(result, "acmeui-icons.svg")));
        Assert.StartsWith($"/* AcmeUi boot {AppVersion.MatUi}:", Text(result, "acmeui.boot.js"));
        Assert.Contains($"Version {AppVersion.MatUi} of the toolkit AcmeUi", Text(result, "acmeui.js"));
        Assert.Contains($"version: '{AppVersion.MatUi}'", Text(result, "acmeui.js"));
    }

    [Fact]
    public void The_renamed_script_keeps_its_behaviour_names_and_has_none_of_the_old_ones()
    {
        var script = Text(Trial(Acme), "acmeui.js");

        Assert.Contains("window.AcmeUi = {", script);
        Assert.Contains("'data-acme-sprite'", script);
        Assert.Contains("localStorage.setItem('acme-theme'", script);
        Assert.Contains("localStorage.setItem('acme-accent'", script);
        Assert.Contains("localStorage.setItem('acme-scheme'", script);
        Assert.Contains("new CustomEvent('acme:theme'", script);
        Assert.Contains("'#acme-i-'", script);
        Assert.Contains("._acmePicker", script);
        Assert.Contains("--acme-custom-accent", script);
        Assert.DoesNotMatch(new Regex(@"(?<![A-Za-z0-9_$-])(?:--|data-)?mat[-:]"), script);
        Assert.DoesNotMatch(new Regex(@"window\.MatUi"), script);
    }

    [Fact]
    public void Every_class_of_the_source_stylesheet_is_in_the_renamed_one_and_nothing_else()
    {
        var result = Trial(Acme);
        var source = MarkupChecks.ClassesIn(ToolkitFiles.CssParts.Select(p => TestPaths.ReadToolkit("css/" + p)));
        var renamed = MarkupChecks.ClassesIn(result.Report.Where(f => f.Path.StartsWith("css/", StringComparison.Ordinal)).Select(f => f.Text));

        Assert.True(source.Count > 500);
        Assert.Equal(source.Select(c => c.StartsWith("mat-", StringComparison.Ordinal) ? "acme-" + c[4..] : c).Order().ToList(), renamed.Order().ToList());
        Assert.All(renamed, c => Assert.True(c.StartsWith("acme-", StringComparison.Ordinal) || c.StartsWith("is-", StringComparison.Ordinal), c));
    }

    [Fact]
    public void Every_example_of_the_design_data_still_passes_the_markup_contract_against_the_renamed_toolkit()
    {
        var result = Trial(Acme);
        var css = result.Report.Where(f => f.Path.StartsWith("css/", StringComparison.Ordinal)).Select(f => f.Text).ToList();
        var icons = Regex.Matches(Text(result, "acmeui-icons.svg"), @"<symbol id=""acme-i-([a-z0-9-]+)""").Select(m => m.Groups[1].Value).ToHashSet();
        var facts = new MarkupFacts(MarkupChecks.ClassesIn(css), icons);
        using var design = JsonDocument.Parse(Text(result, "design.json"));

        var checkedExamples = 0;
        foreach (var component in design.RootElement.GetProperty("components").EnumerateArray())
        {
            var examples = component.GetProperty("examples").EnumerateArray().Select(e => e.GetProperty("html").GetString() ?? string.Empty).ToList();
            checkedExamples += examples.Count;
            Assert.Empty(MarkupChecks.Check(examples, facts, "acme"));
            Assert.All(examples, html => Assert.DoesNotMatch(new Regex(@"(?<![A-Za-z0-9_$-])mat-"), html));
        }

        Assert.True(checkedExamples > 200, $"only {checkedExamples} examples were checked");
    }

    [Fact]
    public void The_replacements_are_counted_per_file_and_are_what_the_lengths_say()
    {
        var result = Trial(Acme);

        Assert.True(result.Report.Single(f => f.Path == "acmeui.css").Counts["class"] > 2000);
        Assert.True(result.Report.Single(f => f.Path == "acmeui.js").Counts["class"] > 500);
        Assert.True(result.Report.Single(f => f.Path == "acmeui.js").Counts["expando"] > 50);
        Assert.True(result.Report.Single(f => f.Path == "acmeui.js").Counts["event"] > 30);
        Assert.Equal(1, result.Report.Single(f => f.Path == "acmeui.js").Counts["data"]);
        Assert.True(result.Report.Single(f => f.Path == "iconlib/acmeui-iconlib.svg").Counts["class"] > 1900);
        Assert.Equal(0, result.Report.Single(f => f.Path == "sample.svg").Replacements);
        Assert.True(result.Totals.Values.Sum() > 50_000);
    }

    [Fact]
    public void The_archive_is_renamed_inside_and_out_with_a_manifest_of_the_renamed_files()
    {
        var result = Trial(Acme);
        var zip = new ZipArchive(new MemoryStream(result.Files.Single(f => f.Path == "acmeui-0.1.0.zip").Bytes));
        string Read(string name)
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        Assert.All(zip.Entries, e => Assert.StartsWith("acmeui/", e.FullName));
        Assert.Contains(zip.Entries, e => e.FullName == "acmeui/acmeui.css");
        Assert.Contains(zip.Entries, e => e.FullName == "acmeui/css/acmeui.tokens.css");
        Assert.Contains(zip.Entries, e => e.FullName == "acmeui/acmeui.manifest.json");
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("matui", StringComparison.Ordinal));
        Assert.Equal(Text(result, "acmeui.css"), Read("acmeui/acmeui.css"));
        Assert.Equal(Text(result, "acmeui.js"), Read("acmeui/acmeui.js"));

        var manifest = JsonDocument.Parse(Read("acmeui/acmeui.manifest.json")).RootElement;
        Assert.Equal("AcmeUi", manifest.GetProperty("name").GetString());
        Assert.Equal(AppVersion.MatUi, manifest.GetProperty("version").GetString());
        Assert.Equal(result.Hash, manifest.GetProperty("hash").GetString());
        var listed = manifest.GetProperty("files").EnumerateArray().ToList();
        Assert.True(listed.Count >= 15);
        foreach (var file in listed)
        {
            var name = file.GetProperty("name").GetString()!;
            var content = Read("acmeui/" + name);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(), file.GetProperty("sha256").GetString());
            Assert.Equal(Encoding.UTF8.GetByteCount(content), file.GetProperty("bytes").GetInt32());
        }

        Assert.Contains(listed, f => f.GetProperty("name").GetString() == "acmeui-icons.svg");
        Assert.StartsWith($"AcmeUi {AppVersion.MatUi} ({result.Hash})", Read("acmeui/README.txt"));
        Assert.Contains("node tools/matui-check.js", Read("acmeui/README.txt"));
        Assert.Contains("--acme-app-accent", Read("acmeui/README.txt"));
        Assert.All(zip.Entries, e => Assert.Equal(new DateTime(2000, 1, 1), e.LastWriteTime.DateTime));
    }

    [Fact]
    public void The_style_guide_and_the_design_data_of_the_copy_are_renamed_and_need_nothing_of_the_source()
    {
        var result = Trial(Acme);
        var page = Text(result, "styleguide.html");

        Assert.Contains("<link rel=\"stylesheet\" href=\"acmeui.css\">", page);
        Assert.Contains("<script src=\"acmeui.boot.js\"></script>", page);
        Assert.Contains("<script src=\"acmeui.js\" defer></script>", page);
        Assert.Contains("<title>AcmeUi 0.1.0", page);
        Assert.Contains("<symbol id=\"acme-i-check\"", page);
        Assert.Contains("class=\"acme-btn", page);
        Assert.DoesNotContain("mat-btn", page);
        Assert.Contains(".sg__live--menu .acme-nav__scroll", Text(result, "styleguide.css"));
        Assert.Contains("var(--acme-space-6)", Text(result, "styleguide.css"));

        using var design = JsonDocument.Parse(Text(result, "design.json"));
        Assert.Contains("acme-", design.RootElement.GetProperty("components")[0].GetProperty("examples")[0].GetProperty("html").GetString());
        // the rules cite the classes of the copy: the rule that names the prefix names the company's
        var naming = design.RootElement.GetProperty("rules").EnumerateArray().Single(r => r.GetProperty("id").GetString() == "CODE-04");
        Assert.Contains("`acme-`", naming.GetProperty("statement").GetString());
        Assert.Contains("`--acme-`", naming.GetProperty("statement").GetString());
        // the names of the other apps of the family are not the toolkit's: Mattiverse stays
        Assert.Contains("Mattiverse", Text(result, "design.json") + page);
    }

    [Fact]
    public void The_build_is_deterministic_the_same_files_make_the_same_copy_byte_for_byte()
    {
        var first = Releases.TrialBuild(Acme);
        var second = Releases.TrialBuild(Acme);

        Assert.Equal(first.Files.Select(f => f.Path), second.Files.Select(f => f.Path));
        foreach (var (a, b) in first.Files.Zip(second.Files))
        {
            Assert.True(a.Bytes.AsSpan().SequenceEqual(b.Bytes), a.Path + " differs between two builds");
        }
    }

    [Theory]
    [InlineData("ac", "Acme")]
    [InlineData("abcdefgh", "Zed")]
    [InlineData("c4", "CompanyDesignSystem")]
    [InlineData("mat", "AcmeUi")]
    [InlineData("zed", "Zed")]
    [InlineData("acme", "Acme")]
    public void Other_prefixes_and_names_build_and_pass_too(string prefix, string name)
    {
        var setting = new WhiteLabelSetting(prefix, name);
        var result = Trial(setting);

        Assert.Empty(result.Findings);
        Assert.Contains(result.Files, f => f.Path == $"{name.ToLowerInvariant()}.css");
        Assert.Contains(result.Files, f => f.Path == $"{name.ToLowerInvariant()}-0.1.0.zip");
        Assert.Contains($"window.{name} = {{", Text(result, $"{name.ToLowerInvariant()}.js"));
    }

    [Fact]
    public void A_copy_that_renames_nothing_runs_no_checks_and_counts_no_replacement()
    {
        Assert.Empty(WhiteLabelChecks.Run(WhiteLabelSetting.None, []));
        var result = Releases.TrialBuild(WhiteLabelSetting.None);
        Assert.Equal(0, result.Totals.Values.Sum());
        Assert.Empty(result.Findings);
    }

    // ------------------------------------------------------------------------------------------------------ the checks have teeth: a faulty rename is found

    [Fact]
    public void A_name_of_the_source_that_is_left_in_a_file_is_found()
    {
        var found = Findings(Trial(Acme), "acmeui.js", t => t + "\nvar leftover = '.mat-btn';");
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Leftover && f.Path == "acmeui.js" && f.Message.Contains("class", StringComparison.Ordinal));

        var product = Findings(Trial(Acme), "acmeui.css", t => t.Replace("AcmeUi", "MatUi", StringComparison.Ordinal));
        Assert.Contains(product, f => f.Check == WhiteLabelChecks.Leftover && f.Message.Contains("name", StringComparison.Ordinal));
        Assert.Contains(product, f => f.Check == WhiteLabelChecks.Names);

        var file = Findings(Trial(Acme), "styleguide.html", t => t.Replace("href=\"acmeui.css\"", "href=\"matui.css\"", StringComparison.Ordinal));
        Assert.Contains(file, f => f.Check == WhiteLabelChecks.Leftover && f.Message.Contains("file", StringComparison.Ordinal));
    }

    [Fact]
    public void A_class_that_is_no_longer_in_the_stylesheet_is_found_both_as_missing_and_as_used()
    {
        var result = Trial(Acme);
        var broken = result.Report.Select(f => f.Path.StartsWith("css/", StringComparison.Ordinal) ? f with { Text = f.Text.Replace(".acme-dialog", ".acme-dialogx", StringComparison.Ordinal) } : f).ToList();
        var found = WhiteLabelChecks.Run(Acme, broken);

        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Classes && f.Message.Contains(".acme-dialog of the source", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Classes && f.Path == "acmeui.js" && f.Message.Contains("'acme-dialog", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Markup && f.Message.Contains("the toolkit has no class 'acme-dialog'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_class_the_script_uses_that_the_stylesheet_has_not_is_found()
    {
        var found = Findings(Trial(Acme), "acmeui.js", t => t.Replace("'acme-toast'", "'acme-toest'", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Classes && f.Message.Contains("'acme-toest'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_custom_property_that_is_used_and_not_defined_is_found()
    {
        var found = Findings(Trial(Acme), "css/acmeui.tokens.css", t => t.Replace("--acme-radius:", "--acme-radiusx:", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Properties && f.Message.StartsWith("--acme-radius is used", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Properties && f.Message.Contains("--acme-radius was defined in the source", StringComparison.Ordinal));
    }

    [Fact]
    public void An_icon_that_is_named_and_not_in_the_sprite_is_found()
    {
        var found = Findings(Trial(Acme), "acmeui-icons.svg", t => t.Replace("<symbol id=\"acme-i-check\"", "<symbol id=\"acme-i-checkx\"", StringComparison.Ordinal));

        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Sprite && f.Message.Contains("acme-i-check' of the source sprite", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Sprite && f.Path == "design.json" && f.Message.Contains("acme-i-check", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Markup && f.Message.Contains("the icon 'check'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_script_that_is_not_well_formed_or_has_another_shape_is_found()
    {
        var bracket = Findings(Trial(Acme), "acmeui.js", t => t.Replace("window.AcmeUi = {", "window.AcmeUi = (", StringComparison.Ordinal));
        Assert.Contains(bracket, f => f.Check == WhiteLabelChecks.Script && f.Message.Contains("not well formed", StringComparison.Ordinal));

        var quote = Findings(Trial(Acme), "acmeui.boot.js", t => t.Replace("'acme-theme'", "'acme-theme", StringComparison.Ordinal));
        Assert.Contains(quote, f => f.Check == WhiteLabelChecks.Script && f.Path == "acmeui.boot.js");

        var shape = Findings(Trial(Acme), "acmeui.js", t => t + "\nvar x = ['a', 'b'];");
        Assert.Contains(shape, f => f.Check == WhiteLabelChecks.Script && f.Message.Contains("another shape", StringComparison.Ordinal));
    }

    [Fact]
    public void A_global_that_is_defined_twice_or_not_at_all_is_found()
    {
        var twice = Findings(Trial(Acme), "acmeui.js", t => t + "\nwindow.AcmeUi = {};");
        Assert.Contains(twice, f => f.Check == WhiteLabelChecks.Script && f.Message.Contains("window.AcmeUi is defined 2 times", StringComparison.Ordinal));

        var none = Findings(Trial(Acme), "acmeui.js", t => t.Replace("window.AcmeUi = {", "window.AcmeUx = {", StringComparison.Ordinal));
        Assert.Contains(none, f => f.Check == WhiteLabelChecks.Script && f.Message.Contains("defined 0 times", StringComparison.Ordinal));
    }

    [Fact]
    public void A_stylesheet_with_unbalanced_braces_is_found()
    {
        var found = Findings(Trial(Acme), "css/acmeui.forms.css", t => t + "\n}");
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Stylesheet && f.Path == "css/acmeui.forms.css");
    }

    [Fact]
    public void A_file_whose_length_does_not_fit_its_replacements_is_found()
    {
        var found = Findings(Trial(Acme), "acmeui.boot.js", t => t + "x");
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Count && f.Path == "acmeui.boot.js");

        var nothing = WhiteLabelChecks.Run(Acme, Trial(Acme).Report.Select(f => f.Path is "acmeui.css" or "css/acmeui.base.css" ? f with { Counts = new Dictionary<string, int>() } : f).ToList());
        Assert.Contains(nothing, f => f.Check == WhiteLabelChecks.Count);
    }

    [Fact]
    public void An_example_that_uses_a_class_the_copy_does_not_have_is_found_by_the_markup_contract()
    {
        var found = Findings(Trial(Acme), "design.json", t => t.Replace("acme-btn--primary", "acme-btn--primaryx", StringComparison.Ordinal));
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Markup && f.Message.Contains("the toolkit has no class 'acme-btn--primaryx'", StringComparison.Ordinal));

        var page = Findings(Trial(Acme), "styleguide.html", t => t.Replace("class=\"acme-btn", "class=\"acme-bogus", StringComparison.Ordinal));
        Assert.Contains(page, f => f.Check == WhiteLabelChecks.Markup && f.Path == "styleguide.html");
    }

    [Fact]
    public void A_bundle_that_is_not_named_after_the_product_is_found()
    {
        var first = Findings(Trial(Acme), "acmeui.css", t => "/* Other 0.1.0 */" + t[t.IndexOf('\n')..]);
        Assert.Contains(first, f => f.Check == WhiteLabelChecks.Names && f.Message.Contains("first line", StringComparison.Ordinal));

        var files = Trial(Acme).Report.Where(f => f.Path != "acmeui.css").ToList();
        Assert.Contains(WhiteLabelChecks.Run(Acme, files), f => f.Check == WhiteLabelChecks.Names && f.Message.Contains("no acmeui.css", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rename_that_forgets_one_rule_is_found_by_the_checks()
    {
        // a renamer that does not know the rule for custom properties: the copy must not pass
        var result = Trial(Acme);
        var faulty = result.Report.Select(f => f with { Text = f.Text.Replace("--acme-", "--mat-", StringComparison.Ordinal), Counts = f.Counts.ToDictionary(p => p.Key, p => p.Key == "property" ? 0 : p.Value) }).ToList();
        var found = WhiteLabelChecks.Run(Acme, faulty);

        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Leftover);
        Assert.Contains(found, f => f.Check == WhiteLabelChecks.Properties);
    }

    // ------------------------------------------------------------------------------------------------------ the tokenizer

    [Fact]
    public void The_tokenizer_reads_strings_comments_templates_regular_expressions_and_brackets()
    {
        var shape = JsShape.Of("""
            // a comment with ' and "
            /* a block ' */
            var a = 'it\'s', b = "say \"x\"", c = `t ${ f({ k: '}' }) } and ${ `inner ${ 1 }` }`;
            var re = /[/'"]+\/x/gi, d = a / 2 / 3, e = (b) / 2;
            if (x) { y[0] = z(1); }
            """);

        Assert.Null(shape.Problem);
        Assert.Equal(2, shape.Comments);
        Assert.Equal(3, shape.Strings);
        Assert.Equal(1, shape.Regexes);
        Assert.Equal(2, shape.Templates);
        Assert.Equal(4, shape.Parens);
        Assert.Equal(1, shape.Brackets);
        Assert.Equal(2, shape.Braces);
    }

    [Theory]
    [InlineData("var a = (1;", "bracket")]
    [InlineData("var a = 1);", ") has no (")]
    [InlineData("var a = [1, 2);", ") has no (")]
    [InlineData("var a = {;", "bracket")]
    [InlineData("var a = 'x;\nvar b = 1;", "string is not closed")]
    [InlineData("var a = `x ${ 1 ;", "bracket")]
    [InlineData("/* never closed", "comment is not closed")]
    [InlineData("var a = `never closed", "template literal is not closed")]
    [InlineData("var a = /never closed;", "regular expression is not closed")]
    public void A_script_that_is_not_well_formed_is_named(string js, string problem)
        => Assert.Contains(problem, JsShape.Of(js).Problem);

    [Fact]
    public void The_real_script_the_boot_script_and_the_stylesheets_are_well_formed()
    {
        foreach (var file in new[] { "js/matui.js", "js/matui.boot.js" })
        {
            var shape = JsShape.Of(TestPaths.ReadToolkit(file));
            Assert.Null(shape.Problem);
            Assert.True(shape.Strings > 0);
        }

        Assert.True(JsShape.Of(TestPaths.ReadToolkit("js/matui.js")).Strings > 3000);
        foreach (var part in ToolkitFiles.CssParts)
        {
            var (open, close) = JsShape.CssBraces(TestPaths.ReadToolkit("css/" + part));
            Assert.Equal(open, close);
            Assert.True(open > 0);
        }
    }
}
