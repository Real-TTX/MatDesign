using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>A file of a white label build: its source text, the renamed text and what the renamer did to it. <see cref="Path"/> is the path in the release, already renamed.</summary>
public sealed record WhiteLabelFile(string Path, string Original, string Text, IReadOnlyDictionary<string, int> Counts)
{
    public int Replacements => Counts.Values.Sum();
}

/// <summary>Something a check of the white label build found. <see cref="Check"/> names the check, <see cref="Path"/> the file it is about.</summary>
public sealed record WhiteLabelFinding(string Check, string Message, string? Path = null);

/// <summary>What <see cref="JsShape"/> saw in a script: how many of each kind of token, and the first thing that was not well formed.</summary>
public sealed record JsShapeResult(int Strings, int Templates, int Comments, int Regexes, int Parens, int Brackets, int Braces, string? Problem);

/// <summary>
/// A small tokenizer for a script, enough to tell that its strings, comments, template literals, regular expressions and brackets are well formed. It does not parse: it walks the text once and
/// counts. A rename changes what is inside a string or a comment and nothing else, so the shape of the renamed script has to be the shape of the original.
/// </summary>
public static class JsShape
{
    private static readonly HashSet<string> RegexAfterWord = new(StringComparer.Ordinal) { "return", "typeof", "case", "in", "of", "delete", "void", "throw", "new", "else", "do", "yield", "instanceof", "await" };

    public static JsShapeResult Of(string js)
    {
        int strings = 0, templates = 0, comments = 0, regexes = 0, parens = 0, brackets = 0, braces = 0;
        var stack = new Stack<char>();
        var i = 0;
        var n = js.Length;
        var previous = '\0';          // the last significant character: ')' ']' '}' or an identifier end mean that a / is a division
        var previousWord = string.Empty;
        string? problem = null;

        void Fail(string text)
        {
            problem ??= $"{text} (line {js.AsSpan(0, Math.Min(i, n)).Count('\n') + 1})";
        }

        // reads a template literal from just after a ` or a } that closes a ${ ... }; returns when the template ends or a ${ opens
        void Template()
        {
            while (i < n)
            {
                var c = js[i];
                if (c == '\\')
                {
                    i += 2;
                }
                else if (c == '`')
                {
                    i++;
                    templates++;
                    previous = 'a';
                    return;
                }
                else if (c == '$' && i + 1 < n && js[i + 1] == '{')
                {
                    i += 2;
                    stack.Push('T');
                    previous = '{';
                    return;
                }
                else
                {
                    i++;
                }
            }

            Fail("a template literal is not closed");
        }

        while (i < n && problem is null)
        {
            var c = js[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '/' && i + 1 < n && js[i + 1] == '/')
            {
                while (i < n && js[i] != '\n')
                {
                    i++;
                }

                comments++;
            }
            else if (c == '/' && i + 1 < n && js[i + 1] == '*')
            {
                var end = js.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    Fail("a comment is not closed");
                    break;
                }

                i = end + 2;
                comments++;
            }
            else if (c is '\'' or '"')
            {
                var quote = c;
                i++;
                while (i < n && js[i] != quote)
                {
                    if (js[i] == '\\')
                    {
                        i++;
                    }
                    else if (js[i] == '\n')
                    {
                        Fail("a string is not closed");
                        break;
                    }

                    i++;
                }

                i++;
                strings++;
                previous = 'a';
                previousWord = string.Empty;
            }
            else if (c == '`')
            {
                i++;
                Template();
            }
            else if (c == '/')
            {
                var regexAllowed = previous == '\0' || (previous != 'a' && previous != ')' && previous != ']' && previous != '}') || (previous == 'a' && RegexAfterWord.Contains(previousWord));
                if (regexAllowed)
                {
                    i++;
                    var inClass = false;
                    while (i < n && js[i] != '\n' && (inClass || js[i] != '/'))
                    {
                        if (js[i] == '\\')
                        {
                            i++;
                        }
                        else if (js[i] == '[')
                        {
                            inClass = true;
                        }
                        else if (js[i] == ']')
                        {
                            inClass = false;
                        }

                        i++;
                    }

                    if (i >= n || js[i] != '/')
                    {
                        Fail("a regular expression is not closed");
                        break;
                    }

                    i++;
                    while (i < n && char.IsAsciiLetter(js[i]))
                    {
                        i++;
                    }

                    regexes++;
                    previous = 'a';
                    previousWord = string.Empty;
                }
                else
                {
                    i++;
                    previous = '/';
                    previousWord = string.Empty;
                }
            }
            else if (char.IsLetterOrDigit(c) || c is '_' or '$')
            {
                var start = i;
                while (i < n && (char.IsLetterOrDigit(js[i]) || js[i] is '_' or '$'))
                {
                    i++;
                }

                previous = 'a';
                previousWord = js[start..i];
            }
            else
            {
                previousWord = string.Empty;
                switch (c)
                {
                    case '(':
                        stack.Push('(');
                        parens++;
                        break;
                    case '[':
                        stack.Push('[');
                        brackets++;
                        break;
                    case '{':
                        stack.Push('{');
                        braces++;
                        break;
                    case ')':
                        if (stack.Count == 0 || stack.Pop() != '(')
                        {
                            Fail("a ) has no (");
                        }

                        break;
                    case ']':
                        if (stack.Count == 0 || stack.Pop() != '[')
                        {
                            Fail("a ] has no [");
                        }

                        break;
                    case '}':
                        if (stack.Count > 0 && stack.Peek() == 'T')
                        {
                            stack.Pop();
                            i++;
                            Template();
                            continue;
                        }

                        if (stack.Count == 0 || stack.Pop() != '{')
                        {
                            Fail("a } has no {");
                        }

                        break;
                }

                previous = c;
                i++;
            }
        }

        if (problem is null && stack.Count > 0)
        {
            problem = $"{stack.Count} bracket(s) are not closed";
        }

        return new JsShapeResult(strings, templates, comments, regexes, parens, brackets, braces, problem);
    }

    /// <summary>The number of <c>{</c> and of <c>}</c> in a stylesheet outside comments and strings; equal when it is well formed.</summary>
    public static (int Open, int Close) CssBraces(string css)
    {
        int open = 0, close = 0;
        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? css.Length : end + 1;
            }
            else if (c is '"' or '\'')
            {
                var quote = c;
                i++;
                while (i < css.Length && css[i] != quote && css[i] != '\n')
                {
                    if (css[i] == '\\')
                    {
                        i++;
                    }

                    i++;
                }
            }
            else if (c == '{')
            {
                open++;
            }
            else if (c == '}')
            {
                close++;
            }
        }

        return (open, close);
    }
}

/// <summary>
/// The tests of the toolkit, run on the result of a white label build. Without node (the image has none) they are written in C#, and they hold what the renamed copy has to keep: no name of the source is left,
/// every class the script and the examples use is in the renamed stylesheet, every custom property that is used is defined, every icon the markup names is in the sprite, the script is well formed and
/// has its global once, and the number of replacements is what the length of the files says. Whatever was already wrong in the source (a class the script uses that no stylesheet defines) is not
/// reported: only what the rename broke, found by running the same look at the original and comparing (<see cref="WhiteLabelFile.Original"/>).
/// </summary>
public static partial class WhiteLabelChecks
{
    public const string Leftover = "leftover";
    public const string Classes = "classes";
    public const string Properties = "properties";
    public const string Sprite = "sprite";
    public const string Script = "script";
    public const string Stylesheet = "stylesheet";
    public const string Count = "count";
    public const string Markup = "markup";
    public const string Names = "names";

    /// <summary>The checks in the order of the report, with what each holds.</summary>
    public static readonly IReadOnlyList<(string Id, string What)> All =
    [
        (Leftover, "No name of the source (the prefix, the product name, the file names) is left in any file of the copy."),
        (Classes, "Every class the script and the examples use is in the renamed stylesheet, and the stylesheet has the same classes as the source, renamed."),
        (Properties, "Every custom property that is used is defined, as it was in the source."),
        (Sprite, "Every icon the script and the markup name is in the renamed sprite."),
        (Script, "The renamed script has the same strings, comments, regular expressions and brackets as the source, all well formed, and its global is defined once."),
        (Stylesheet, "The braces of every renamed stylesheet are balanced."),
        (Count, "The length of every renamed file differs from the source by exactly what its replacements add."),
        (Markup, "Every renamed example passes the contract of the markup (MarkupChecks) against the renamed stylesheet and sprite."),
        (Names, "The files are named by the product name and the first line of the bundle names the product."),
    ];

    [GeneratedRegex(@"<symbol id=""([A-Za-z0-9_-]+)""")]
    private static partial Regex SymbolId();

    [GeneratedRegex(@"class=""([^""]*)""")]
    private static partial Regex ClassAttribute();

    private static bool IsCss(WhiteLabelFile f) => f.Path.EndsWith(".css", StringComparison.Ordinal);

    private static bool IsScript(WhiteLabelFile f) => f.Path.EndsWith(".js", StringComparison.Ordinal);

    private static bool IsSprite(WhiteLabelFile f) => f.Path.EndsWith(".svg", StringComparison.Ordinal) && f.Text.Contains("<symbol ", StringComparison.Ordinal);

    private static bool IsDesign(WhiteLabelFile f) => f.Path == "design.json";

    /// <summary>Runs every check on the files of a renamed copy. An empty list means the copy can be released.</summary>
    public static IReadOnlyList<WhiteLabelFinding> Run(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files)
    {
        var findings = new List<WhiteLabelFinding>();
        if (setting.IsNoOp)
        {
            return findings;
        }

        CheckLeftovers(setting, files, findings);
        CheckClasses(setting, files, findings);
        CheckProperties(setting, files, findings);
        CheckSprite(setting, files, findings);
        CheckScripts(setting, files, findings);
        CheckStylesheets(files, findings);
        CheckCounts(setting, files, findings);
        CheckMarkup(setting, files, findings);
        CheckNames(setting, files, findings);
        return findings;
    }

    // ---- (a) nothing of the source is left

    private static void CheckLeftovers(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        foreach (var file in files)
        {
            var left = WhiteLabel.Leftovers(file.Text, setting);
            if (left.Count > 0)
            {
                var first = left[0];
                findings.Add(new(Leftover, $"{left.Count} name(s) of the source are left ({string.Join(", ", left.Select(l => l.Rule).Distinct())}); the first on line {first.Line}: ...{first.Text}...", file.Path));
            }

            if (WhiteLabel.Leftovers(file.Path, setting).Count > 0)
            {
                findings.Add(new(Leftover, "The name of the file still carries a name of the source.", file.Path));
            }
        }
    }

    // ---- (b) classes

    /// <summary>The names starting with the prefix that stand in a text and are not defined as a class: whole names must be a class, a name that ends in a dash or underscore is the start of one.</summary>
    private static HashSet<string> UsedClassTokens(string prefix, IEnumerable<string> texts)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        // a name that is a file (acme-icons.svg, when the product is called Acme) is not a class: the atomic group does not give back a shorter name to match
        var rx = new Regex($@"(?<![A-Za-z0-9_$-]){Regex.Escape(prefix)}-(?>[a-z0-9_-]*)(?!\.(?:css|js|svg|json|zip|html|txt|md)\b)", RegexOptions.CultureInvariant);
        foreach (var text in texts)
        {
            foreach (Match m in rx.Matches(text))
            {
                tokens.Add(m.Value);
            }
        }

        return tokens;
    }

    private static HashSet<string> Undefined(IEnumerable<string> used, IReadOnlySet<string> classes)
        => used.Where(t => t.EndsWith('-') || t.EndsWith('_') ? !classes.Any(c => c.StartsWith(t, StringComparison.Ordinal)) : !classes.Contains(t)).ToHashSet(StringComparer.Ordinal);

    private static void CheckClasses(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        var css = files.Where(f => IsCss(f) && f.Path.StartsWith("css/", StringComparison.Ordinal)).ToList();
        if (css.Count == 0)
        {
            css = files.Where(IsCss).ToList();
        }

        var renamedClasses = MarkupChecks.ClassesIn(css.Select(f => f.Text));
        var originalClasses = MarkupChecks.ClassesIn(css.Select(f => f.Original));
        var expected = originalClasses.Select(c => WhiteLabel.RenameToken(c, setting)).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in expected.Where(c => !renamedClasses.Contains(c)).Order(StringComparer.Ordinal).Take(10))
        {
            findings.Add(new(Classes, $"The class .{missing} of the source is not in the renamed stylesheet."));
        }

        foreach (var extra in renamedClasses.Where(c => !expected.Contains(c)).Order(StringComparer.Ordinal).Take(10))
        {
            findings.Add(new(Classes, $"The renamed stylesheet has a class .{extra} that the source does not."));
        }

        // what the scripts and the style guide use: the same names undefined as in the source, renamed, and no more
        foreach (var file in files.Where(f => IsScript(f) || f.Path.EndsWith(".html", StringComparison.Ordinal)))
        {
            var usedNow = Undefined(UsedClassTokens(setting.Prefix, [file.Text]), renamedClasses);
            var usedBefore = Undefined(UsedClassTokens(WhiteLabelSetting.SourcePrefix, [file.Original]), originalClasses).Select(t => WhiteLabel.RenameToken(t, setting)).ToHashSet(StringComparer.Ordinal);
            foreach (var token in usedNow.Where(t => !usedBefore.Contains(t)).Order(StringComparer.Ordinal).Take(10))
            {
                findings.Add(new(Classes, $"'{token}' is used and the renamed stylesheet has no such class.", file.Path));
            }
        }
    }

    // ---- (c) custom properties

    private static (HashSet<string> Used, HashSet<string> Defined) PropertiesIn(string prefix, IReadOnlyList<WhiteLabelFile> files, bool original)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var name = $@"--{Regex.Escape(prefix)}-[a-z0-9-]*[a-z0-9]";
        var use = new Regex($@"var\(\s*({name})", RegexOptions.CultureInvariant);
        var declare = new Regex($@"(?<![A-Za-z0-9_-])({name})\s*:", RegexOptions.CultureInvariant);
        var any = new Regex($@"(?<![A-Za-z0-9_-])({name})", RegexOptions.CultureInvariant);
        var set = new Regex($@"setProperty\(\s*['""]({name})['""]", RegexOptions.CultureInvariant);
        var hasParts = files.Any(f => IsCss(f) && f.Path.StartsWith("css/", StringComparison.Ordinal));
        foreach (var file in files)
        {
            // the bundle is the parts again: with the parts at hand it is not read twice (and a part that was changed is not hidden by it)
            if (hasParts && IsCss(file) && file.Original.StartsWith("/* MatUi ", StringComparison.Ordinal))
            {
                continue;
            }

            var text = original ? file.Original : file.Text;
            if (IsCss(file))
            {
                foreach (Match m in use.Matches(text))
                {
                    used.Add(m.Groups[1].Value);
                }

                foreach (Match m in declare.Matches(text))
                {
                    defined.Add(m.Groups[1].Value);
                }
            }
            else if (IsScript(file))
            {
                foreach (Match m in any.Matches(text))
                {
                    used.Add(m.Groups[1].Value);
                }

                foreach (Match m in set.Matches(text))
                {
                    defined.Add(m.Groups[1].Value);
                }
            }
        }

        return (used, defined);
    }

    private static void CheckProperties(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        var (usedNow, definedNow) = PropertiesIn(setting.Prefix, files, original: false);
        var (usedBefore, definedBefore) = PropertiesIn(WhiteLabelSetting.SourcePrefix, files, original: true);
        var undefinedBefore = usedBefore.Where(p => !definedBefore.Contains(p)).Select(p => WhiteLabel.RenameToken(p, setting)).ToHashSet(StringComparer.Ordinal);
        foreach (var property in usedNow.Where(p => !definedNow.Contains(p) && !undefinedBefore.Contains(p)).Order(StringComparer.Ordinal).Take(15))
        {
            findings.Add(new(Properties, $"{property} is used and no stylesheet or script defines it."));
        }

        var expectedDefined = definedBefore.Select(p => WhiteLabel.RenameToken(p, setting)).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in expectedDefined.Where(p => !definedNow.Contains(p)).Order(StringComparer.Ordinal).Take(15))
        {
            findings.Add(new(Properties, $"{missing} was defined in the source and is not any more."));
        }
    }

    // ---- (d) sprite ids

    private static void CheckSprite(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        var renamedIds = files.Where(IsSprite).SelectMany(f => SymbolId().Matches(f.Text).Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        var originalIds = files.Where(IsSprite).SelectMany(f => SymbolId().Matches(f.Original).Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in originalIds.Select(i => WhiteLabel.RenameToken(i, setting)).Where(i => !renamedIds.Contains(i)).Order(StringComparer.Ordinal).Take(10))
        {
            findings.Add(new(Sprite, $"The symbol '{missing}' of the source sprite is not in the renamed one."));
        }

        var use = new Regex($@"#({Regex.Escape(setting.Prefix)}-(?:i|il)-[a-z0-9-]*[a-z0-9])", RegexOptions.CultureInvariant);
        var useBefore = new Regex($@"#({WhiteLabelSetting.SourcePrefix}-(?:i|il)-[a-z0-9-]*[a-z0-9])", RegexOptions.CultureInvariant);
        foreach (var file in files.Where(f => !IsSprite(f)))
        {
            // what the source named and its sprite did not have either: not the rename's doing
            var before = useBefore.Matches(file.Original).Select(m => m.Groups[1].Value).Where(i => !originalIds.Contains(i)).Select(i => WhiteLabel.RenameToken(i, setting)).ToHashSet(StringComparer.Ordinal);
            foreach (var id in use.Matches(file.Text).Select(m => m.Groups[1].Value).Distinct().Where(i => !renamedIds.Contains(i) && !before.Contains(i)).Take(10))
            {
                findings.Add(new(Sprite, $"The icon '{id}' is named and is not in the renamed sprite.", file.Path));
            }
        }
    }

    // ---- (e) the script

    private static void CheckScripts(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        foreach (var file in files.Where(IsScript))
        {
            var now = JsShape.Of(file.Text);
            var before = JsShape.Of(file.Original);
            if (now.Problem is not null && before.Problem is null)
            {
                findings.Add(new(Script, "The renamed script is not well formed: " + now.Problem + ".", file.Path));
            }
            else if (now != before)
            {
                findings.Add(new(Script, $"The renamed script has another shape than the source: {Describe(before)} became {Describe(now)}.", file.Path));
            }
        }

        if (!setting.RenamesName)
        {
            return;
        }

        var main = files.FirstOrDefault(f => IsScript(f) && f.Original.Contains("window.MatUi", StringComparison.Ordinal) && f.Original.Contains("window.MatUi = {", StringComparison.Ordinal));
        if (main is not null)
        {
            var defines = Regex.Matches(main.Text, $@"window\.{Regex.Escape(setting.Name)}\s*=(?!=)").Count;
            if (defines != 1)
            {
                findings.Add(new(Script, $"window.{setting.Name} is defined {defines} times: it has to be once.", main.Path));
            }
        }
    }

    private static string Describe(JsShapeResult s) => $"{s.Strings} strings, {s.Comments} comments, {s.Regexes} regular expressions, {s.Braces} braces";

    // ---- the braces of the stylesheets

    private static void CheckStylesheets(IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        foreach (var file in files.Where(IsCss))
        {
            var (open, close) = JsShape.CssBraces(file.Text);
            var (openBefore, closeBefore) = JsShape.CssBraces(file.Original);
            if (open != close && openBefore == closeBefore)
            {
                findings.Add(new(Stylesheet, $"The renamed stylesheet has {open} {{ and {close} }}.", file.Path));
            }
            else if (open != openBefore)
            {
                findings.Add(new(Stylesheet, $"The renamed stylesheet has {open} blocks, the source {openBefore}.", file.Path));
            }
        }
    }

    // ---- (f) the arithmetic of the replacements

    private static void CheckCounts(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        var prefixDelta = setting.Prefix.Length - WhiteLabelSetting.SourcePrefix.Length;
        var nameDelta = setting.Name.Length - WhiteLabelSetting.SourceName.Length;
        foreach (var file in files)
        {
            var expected = file.Counts.Sum(p => p.Key switch
            {
                "name" or "file" => p.Value * nameDelta,
                _ => p.Value * prefixDelta,
            });
            var actual = file.Text.Length - file.Original.Length;
            if (expected != actual)
            {
                findings.Add(new(Count, $"{file.Replacements} replacement(s) should change the length by {expected}, and it changed by {actual}.", file.Path));
            }
        }

        var css = files.Where(f => IsCss(f) && f.Path != "styleguide.css").Sum(f => f.Counts.GetValueOrDefault("class"));
        var script = files.Where(IsScript).Sum(f => f.Counts.GetValueOrDefault("class"));
        if (setting.RenamesPrefix && (css == 0 || script == 0))
        {
            findings.Add(new(Count, $"Nothing was renamed in the stylesheets ({css}) or in the scripts ({script}): the rename did not reach them."));
        }
    }

    // ---- (b) markup

    private static void CheckMarkup(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        var design = files.FirstOrDefault(IsDesign);
        if (design is null)
        {
            return;
        }

        var css = files.Where(f => IsCss(f) && f.Path.StartsWith("css/", StringComparison.Ordinal)).ToList();
        var sprite = files.Where(IsSprite).ToList();
        var now = new MarkupFacts(MarkupChecks.ClassesIn(css.Select(f => f.Text)), IconsOf(sprite.Select(f => f.Text), setting.Prefix));
        var before = new MarkupFacts(MarkupChecks.ClassesIn(css.Select(f => f.Original)), IconsOf(sprite.Select(f => f.Original), WhiteLabelSetting.SourcePrefix));
        var renamedProblems = Problems(design.Text, now, setting.Prefix);
        var sourceProblems = Problems(design.Original, before, WhiteLabelSetting.SourcePrefix).Select(p => (p.Component, p.Problem with { Message = WhiteLabel.Rename(p.Problem.Message, setting) })).ToHashSet();
        foreach (var (component, problem) in renamedProblems.Where(p => !sourceProblems.Contains(p)).Take(20))
        {
            findings.Add(new(Markup, $"{component}: {problem.Check} - {problem.Message}", design.Path));
        }

        // the page of the style guide: every class it uses is in a stylesheet of the copy, or was not in the source either
        foreach (var page in files.Where(f => f.Path.EndsWith(".html", StringComparison.Ordinal)))
        {
            var known = now.Classes.Concat(files.Where(f => IsCss(f) && !f.Path.StartsWith("css/", StringComparison.Ordinal) && f.Path != $"{setting.FileStem}.css").SelectMany(f => MarkupChecks.ClassesIn([f.Text]))).ToHashSet(StringComparer.Ordinal);
            var knownBefore = before.Classes.Concat(files.Where(f => IsCss(f) && !f.Path.StartsWith("css/", StringComparison.Ordinal) && f.Path != $"{setting.FileStem}.css").SelectMany(f => MarkupChecks.ClassesIn([f.Original]))).ToHashSet(StringComparer.Ordinal);
            var usedNow = ClassAttribute().Matches(page.Text).SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.Ordinal);
            var usedBefore = ClassAttribute().Matches(page.Original).SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(c => !knownBefore.Contains(c)).Select(c => WhiteLabel.RenameToken(c, setting)).ToHashSet(StringComparer.Ordinal);
            foreach (var token in usedNow.Where(c => !known.Contains(c) && !usedBefore.Contains(c)).Order(StringComparer.Ordinal).Take(10))
            {
                findings.Add(new(Markup, $"The page uses the class '{token}' and no stylesheet of the copy has it.", page.Path));
            }
        }
    }

    private static HashSet<string> IconsOf(IEnumerable<string> sprites, string prefix)
        => sprites.SelectMany(s => SymbolId().Matches(s).Select(m => m.Groups[1].Value))
            .Where(id => id.StartsWith(prefix + "-i-", StringComparison.Ordinal))
            .Select(id => id[(prefix.Length + 3)..])
            .ToHashSet(StringComparer.Ordinal);

    private static List<(string Component, MarkupProblem Problem)> Problems(string designJson, MarkupFacts facts, string prefix)
    {
        var result = new List<(string, MarkupProblem)>();
        using var document = JsonDocument.Parse(designJson);
        if (!document.RootElement.TryGetProperty("components", out var components))
        {
            return result;
        }

        foreach (var component in components.EnumerateArray())
        {
            var id = component.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "?" : "?";
            var examples = component.TryGetProperty("examples", out var list)
                ? list.EnumerateArray().Select(e => e.TryGetProperty("html", out var html) ? html.GetString() ?? string.Empty : string.Empty).ToList()
                : [];
            result.AddRange(MarkupChecks.Check(examples, facts, prefix).Select(p => (id, p)));
        }

        return result;
    }

    // ---- the names of the files and the first line

    private static void CheckNames(WhiteLabelSetting setting, IReadOnlyList<WhiteLabelFile> files, List<WhiteLabelFinding> findings)
    {
        if (!setting.RenamesName)
        {
            return;
        }

        var stem = setting.FileStem;
        var bundle = files.FirstOrDefault(f => f.Path == $"{stem}.css");
        if (bundle is null)
        {
            findings.Add(new(Names, $"There is no {stem}.css: the bundle was not renamed."));
        }
        else if (!bundle.Text.StartsWith($"/* {setting.Name} ", StringComparison.Ordinal))
        {
            findings.Add(new(Names, $"The first line of the bundle does not name the product ({setting.Name}).", bundle.Path));
        }

        foreach (var file in files.Where(f => f.Path.Contains("matui", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new(Names, "A file is still called after MatUi.", file.Path));
        }
    }

    /// <summary>The findings as lines of a log.</summary>
    public static IEnumerable<string> Lines(IEnumerable<WhiteLabelFinding> findings)
        => findings.Select(f => $"[{f.Check}] {(f.Path is null ? string.Empty : f.Path + ": ")}{f.Message}");

    /// <summary>The names of the checks that found something, for a summary.</summary>
    public static string Summary(IReadOnlyList<WhiteLabelFinding> findings)
    {
        if (findings.Count == 0)
        {
            return $"All {All.Count} checks pass.";
        }

        var failed = findings.Select(f => f.Check).Distinct().ToList();
        return $"{findings.Count} finding(s) in {failed.Count} of {All.Count} checks: {string.Join(", ", failed)}.";
    }
}
