using System.Globalization;
using System.Text.RegularExpressions;

namespace MatDesktop.Tests;

/// <summary>The sources of MatDesktop: no literal colour, the files that a WinForms tray copies know nothing of Avalonia, one version, every text in both languages.</summary>
public sealed partial class SourceTests
{
    /// <summary>The files that need nothing but System (AGENT-05: a WinForms tray copies them; tools/matdesktop-copy.js --tokens-only takes the first three).</summary>
    public static readonly string[] AvaloniaFree =
    [
        "Tokens/MatTokens.g.cs", "Tokens/MatColor.cs", "Tokens/MatPalette.cs",
        "MatText.cs", "MatVersion.cs", "MatState.cs", "MatDesktopInfo.cs", "MatSingleInstance.cs", "Tray/MatTrayMenu.cs",
    ];

    /// <summary>The literal colours that are allowed, and where: the red of the close button of Windows (MatTitleBar says why).</summary>
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.Ordinal)
    {
        ["Controls/MatTitleBar.cs"] = ["#C42B1C", "#FFFFFF"],
    };

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
    private static partial Regex Hex();

    [GeneratedRegex(@"(?<![\w.])(?:Colors|Brushes)\.(?!Transparent\b)[A-Z]\w*|Color\.From(?:Rgb|Argb)\(\s*\d|new\s+Color\(\s*\d|Color\.Parse\(\s*""")]
    private static partial Regex CodeColour();

    [GeneratedRegex(@"\b(?:Background|Foreground|Fill|Stroke|BorderBrush|Color)=""(?!\{)(?!Transparent"")[^""]*""")]
    private static partial Regex XamlColour();

    [GeneratedRegex(@"<MatDesktopVersion>([^<]+)</MatDesktopVersion>")]
    private static partial Regex ProjectVersion();

    private static IEnumerable<(string Relative, string Text)> Sources(params string[] patterns) => patterns
        .SelectMany(p => Directory.GetFiles(Paths.Package, p, SearchOption.AllDirectories))
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .Select(f => (Path.GetRelativePath(Paths.Package, f).Replace('\\', '/'), File.ReadAllText(f)));

    /// <summary>The code without its comments (a comment may name a colour of the CSS).</summary>
    private static string Code(string text) => string.Join('\n', text.Split('\n').Select(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : l));

    private static string Markup(string text) => Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

    [Fact]
    public void No_literal_colour_outside_the_generated_tokens_but_the_red_of_the_close_button()
    {
        var findings = new List<string>();
        foreach (var (file, text) in Sources("*.cs", "*.axaml"))
        {
            if (file == "Tokens/MatTokens.g.cs" || file == "Themes/MatIcons.axaml")
            {
                continue;
            }

            var code = file.EndsWith(".axaml", StringComparison.Ordinal) ? Markup(text) : Code(text);
            var allowed = Allowed.GetValueOrDefault(file, []);
            findings.AddRange(Hex().Matches(code).Select(m => m.Value).Where(v => !allowed.Contains(v)).Select(v => $"{file}: {v}"));
            findings.AddRange(CodeColour().Matches(code).Select(m => $"{file}: {m.Value}"));
            if (file.EndsWith(".axaml", StringComparison.Ordinal))
            {
                findings.AddRange(XamlColour().Matches(code).Select(m => $"{file}: {m.Value}"));
            }
        }

        Assert.True(findings.Count == 0, string.Join("\n", findings));

        // the exception says why it is one
        var titleBar = File.ReadAllText(Path.Combine(Paths.Package, "Controls", "MatTitleBar.cs"));
        Assert.Contains("public const string WindowsCloseHover = \"#C42B1C\";", titleBar, StringComparison.Ordinal);
        Assert.Contains("the one literal colour of MatDesktop", titleBar, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scan_finds_a_literal_colour_where_there_is_one()
    {
        Assert.Matches(Hex(), "Background=\"#eee\"");
        Assert.Matches(XamlColour(), "<Border Background=\"White\" />");
        Assert.DoesNotMatch(XamlColour(), "<Border Background=\"{DynamicResource Mat.Surface}\" />");
        Assert.DoesNotMatch(XamlColour(), "<Border Background=\"Transparent\" />");
        Assert.Matches(CodeColour(), "Foreground = Brushes.White;");
        Assert.Matches(CodeColour(), "Color.FromRgb(1, 2, 3)");
        Assert.DoesNotMatch(CodeColour(), "Background = Brushes.Transparent;");
        Assert.DoesNotMatch(CodeColour(), "MatTokens.Colors.ToDictionary()");
    }

    [Fact]
    public void The_files_a_winforms_tray_copies_know_nothing_of_avalonia()
    {
        foreach (var file in AvaloniaFree)
        {
            var text = File.ReadAllText(Path.Combine(Paths.Package, file));
            Assert.DoesNotContain("using Avalonia", text, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\bAvalonia\.", Code(text));
            Assert.Matches("(?m)^namespace MatDesktop;\r?$", text);
        }

        // the three of --tokens-only need only each other
        foreach (var file in AvaloniaFree.Take(3))
        {
            var code = Code(File.ReadAllText(Path.Combine(Paths.Package, file)));
            Assert.DoesNotMatch(@"\b(?:MatText|MatTheme|MatState|MatVersion|MatTrayMenu)\b", code);
        }
    }

    [Fact]
    public void Every_file_is_in_the_namespace_and_names_no_program_of_the_family()
    {
        foreach (var (file, text) in Sources("*.cs", "*.axaml"))
        {
            if (file.EndsWith(".cs", StringComparison.Ordinal))
            {
                Assert.Matches("(?m)^namespace MatDesktop;\r?$", text);
            }

            Assert.DoesNotMatch(@"ThixClip|Matmon|Matgate|MatHub|ANNA-PC", text);
            Assert.DoesNotContain("a copy from MatDesign", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_version_of_the_project_and_of_the_code_are_the_same()
    {
        var project = File.ReadAllText(Path.Combine(Paths.Package, "MatDesktop.csproj"));
        var version = ProjectVersion().Match(project).Groups[1].Value;
        Assert.Equal(MatDesktopInfo.Version, version);
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Assert.Contains($"## {version}", File.ReadAllText(Path.Combine(Paths.Package, "README.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_generated_files_say_that_they_are_generated_and_use_lf()
    {
        foreach (var file in new[] { "Tokens/MatTokens.g.cs", "Themes/MatIcons.axaml" })
        {
            var text = File.ReadAllText(Path.Combine(Paths.Package, file));
            Assert.Contains("tools/desktop.js", text, StringComparison.Ordinal);
            Assert.Contains("do not edit", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        }
    }

    // ---- the texts (DESK-08)

    [GeneratedRegex(@"""((?:state|tray|caption|nav|info|setup|copy)\.[A-Za-z.]+|version(?:\.local)?|copy|copied)""")]
    private static partial Regex TextKey();

    [Fact]
    public void Both_languages_have_the_same_texts_and_every_text_the_code_asks_for()
    {
        Assert.Equal(MatText.English.Keys.Order(StringComparer.Ordinal), MatText.German.Keys.Order(StringComparer.Ordinal));
        Assert.All(MatText.English, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), e.Key));
        Assert.All(MatText.German, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), e.Key));
        foreach (var (key, text) in MatText.English)
        {
            // a text with a value has it in both languages
            Assert.Equal(Regex.Matches(text, @"\{\d\}").Count, Regex.Matches(MatText.German[key], @"\{\d\}").Count);
        }

        foreach (var (file, text) in Sources("*.cs"))
        {
            foreach (Match m in TextKey().Matches(Code(text)))
            {
                var key = m.Groups[1].Value;
                Assert.True(
                    MatText.English.ContainsKey(key) || MatText.English.Keys.Any(k => k.StartsWith(key + ".", StringComparison.Ordinal)),
                    $"{file} asks for the text {key}, which MatText does not have");
            }
        }

        // the keys the setup window puts together from the mode
        foreach (var prefix in new[] { "setup.mode", "setup.progress", "setup.finish", "setup.failed" })
        {
            foreach (var mode in new[] { "setup", "update", "uninstall" })
            {
                Assert.True(MatText.English.ContainsKey($"{prefix}.{mode}"), $"{prefix}.{mode}");
            }
        }
    }

    [Theory]
    [InlineData(null, "de-DE", true)]
    [InlineData(null, "de-AT", true)]
    [InlineData(null, "de", true)]
    [InlineData(null, "en-US", false)]
    [InlineData(null, "fr-FR", false)]
    [InlineData("de", "en-US", true)]
    [InlineData("en", "de-DE", false)]
    public void German_for_a_german_system_english_for_every_other_and_a_setting_wins(string? language, string culture, bool german)
        => Assert.Equal(german, MatText.ChoosesGerman(language, CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void The_tray_says_what_the_guideline_says_in_both_languages()
    {
        Assert.Equal("{0} öffnen", MatText.German["tray.open"]);
        Assert.Equal("Open {0}", MatText.English["tray.open"]);
        Assert.Equal("Beenden", MatText.German["tray.quit"]);
        Assert.Equal("Log-Ordner öffnen", MatText.German["tray.logFolder"]);
        Assert.Equal("Update installieren …", MatText.German["tray.installUpdate"]);
        Assert.Equal("Dienst neu starten …", MatText.German["tray.serviceRestart"]);
        Assert.Equal(["Läuft", "Wartet", "Gestoppt", "Nicht eingerichtet"], new[] { "state.running", "state.waiting", "state.stopped", "state.notSetUp" }.Select(k => MatText.German[k]));
        Assert.Equal("Auch meine Daten löschen", MatText.German["setup.deleteData"]);
        Assert.Equal("Also delete my data", MatText.English["setup.deleteData"]);
    }
}
