using System.Text.RegularExpressions;

namespace MatDesign.Core;

/// <summary>One declaration of a CSS rule, with the comment that follows it on the same line.</summary>
public sealed record CssDeclaration(string Name, string Value, string? Comment);

/// <summary>A CSS rule: its selector, the media query it sits in (if any) and its declarations.</summary>
public sealed record CssBlock(string Selector, string? Media, IReadOnlyList<CssDeclaration> Declarations);

/// <summary>A design token as the site shows it: the light value, the dark value, what it is for and the group it belongs to.</summary>
public sealed record DesignToken(string Name, string Light, string? Dark, string Description, string Group)
{
    /// <summary>True for tokens that hold a colour (they must exist for both modes).</summary>
    public bool IsColor => Light.StartsWith('#') || Light.StartsWith("rgb", StringComparison.Ordinal) || Light.StartsWith("color-mix", StringComparison.Ordinal)
        || Light.StartsWith("var(--mat-app", StringComparison.Ordinal) || Light.StartsWith("var(--mat-accent", StringComparison.Ordinal);
}

/// <summary>
/// Reads <c>matui.tokens.css</c>. The CSS is the source of truth; the site, the documentation and the tests read it instead of keeping a
/// second list. A token's description is the comment behind its declaration, its group the last <c>/* ---- Name ---- */</c> comment before it.
/// </summary>
public static partial class CssTokens
{
    // `--name: value;` followed, on the same line, by an optional `/* comment */`
    [GeneratedRegex(@"(--[\w-]+)\s*:\s*((?:[^;/]|/(?!\*))+?)\s*;[ \t]*(?:/\*\s*(.*?)\s*\*/)?")]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex(@"^-+\s*(.*?)\s*-+$")]
    private static partial Regex GroupRegex();

    /// <summary>Splits a stylesheet into its rules (at-rules such as <c>@media</c> are entered, their rules carry the query).</summary>
    public static IReadOnlyList<CssBlock> ParseBlocks(string css)
    {
        var blocks = new List<CssBlock>();
        Scan(css, null, blocks);
        return blocks;
    }

    private static int SkipComment(string css, int i)
    {
        var end = css.IndexOf("*/", i, StringComparison.Ordinal);
        return end < 0 ? css.Length : end + 2;
    }

    private static int FindClose(string css, int open)
    {
        var depth = 1;
        var j = open + 1;
        while (j < css.Length && depth > 0)
        {
            if (css[j] == '/' && j + 1 < css.Length && css[j + 1] == '*')
            {
                j = SkipComment(css, j);
                continue;
            }

            depth += css[j] == '{' ? 1 : css[j] == '}' ? -1 : 0;
            j++;
        }

        return j;
    }

    private static void Scan(string css, string? media, List<CssBlock> blocks)
    {
        var i = 0;
        while (i < css.Length)
        {
            if (char.IsWhiteSpace(css[i]))
            {
                i++;
                continue;
            }

            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                i = SkipComment(css, i);
                continue;
            }

            var open = css.IndexOf('{', i);
            if (open < 0)
            {
                break;
            }

            var selector = Regex.Replace(css[i..open], @"\s+", " ").Trim();
            var close = FindClose(css, open);
            var body = css[(open + 1)..Math.Max(open + 1, close - 1)];

            if (!selector.StartsWith('@'))
            {
                blocks.Add(new CssBlock(selector, media, ParseDeclarations(body)));
            }
            else if (selector.StartsWith("@media", StringComparison.Ordinal))
            {
                Scan(body, selector["@media".Length..].Trim(), blocks);
            }

            i = close;
        }
    }

    public static IReadOnlyList<CssDeclaration> ParseDeclarations(string body)
        => DeclarationRegex().Matches(body)
            .Select(m => new CssDeclaration(m.Groups[1].Value, Regex.Replace(m.Groups[2].Value, @"\s+", " ").Trim(), m.Groups[3].Success ? m.Groups[3].Value : null))
            .ToList();

    /// <summary>The declarations of the first block that starts with <paramref name="selector"/> at the beginning of a line, each with its group heading.</summary>
    private static IEnumerable<(string Group, CssDeclaration Declaration)> Grouped(string css, string selector)
    {
        var start = css.StartsWith(selector + " {", StringComparison.Ordinal) ? 0 : css.IndexOf("\n" + selector + " {", StringComparison.Ordinal) + 1;
        if (start <= 0 && !css.StartsWith(selector + " {", StringComparison.Ordinal))
        {
            yield break;
        }

        var open = css.IndexOf('{', start);
        var body = css[(open + 1)..(FindClose(css, open) - 1)];
        var group = string.Empty;
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("/*", StringComparison.Ordinal) && line.EndsWith("*/", StringComparison.Ordinal))
            {
                var heading = GroupRegex().Match(line[2..^2].Trim());
                if (heading.Success && heading.Groups[1].Value.Length > 0)
                {
                    group = heading.Groups[1].Value;
                }

                continue;
            }

            foreach (var d in ParseDeclarations(line))
            {
                yield return (group, d);
            }
        }
    }

    /// <summary>The tokens with light value, dark value and description: the base block, the derived accent block, and the dark overrides.</summary>
    public static IReadOnlyList<DesignToken> Tokens(string tokensCss)
    {
        var blocks = ParseBlocks(tokensCss);
        var dark = blocks
            .Where(b => b.Media is null && b.Selector.StartsWith(":root[data-theme=\"dark\"]", StringComparison.Ordinal))
            .SelectMany(b => b.Declarations)
            .GroupBy(d => d.Name)
            .ToDictionary(g => g.Key, g => g.Last().Value);

        var tokens = new List<DesignToken>();
        foreach (var (group, d) in Grouped(tokensCss, ":root"))
        {
            if (d.Name.StartsWith("--mat-", StringComparison.Ordinal))
            {
                tokens.Add(new DesignToken(d.Name, d.Value, dark.GetValueOrDefault(d.Name), d.Comment ?? string.Empty, group));
            }
        }

        var derived = blocks.First(b => b.Media is null && b.Selector.StartsWith(":root,", StringComparison.Ordinal) && b.Selector.Contains("[data-app]"));
        foreach (var d in derived.Declarations)
        {
            tokens.Add(new DesignToken(d.Name, d.Value, dark.GetValueOrDefault(d.Name), d.Comment ?? string.Empty, "Accent"));
        }

        return tokens;
    }
}
