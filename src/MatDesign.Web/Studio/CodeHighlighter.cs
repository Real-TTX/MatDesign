using System.Net;
using System.Text;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>
/// Colours the code of an example, markup or script, for the component pages and the styleguide of a release. It has no dependency and no script: it
/// walks the text once and wraps the parts in <c>&lt;span class="mat-code__…"&gt;</c>. Everything that comes from the code is HTML-encoded and the spans are
/// the only tags in the result, so what it returns is safe to write into a page, and the text of the result is exactly the code (copying it copies the code).
/// The colours are in <c>matui.controls.css</c> (<c>mat-code__tag</c>, <c>__attr</c>, <c>__str</c>, <c>__com</c>, <c>__pun</c>, <c>__kw</c>, <c>__num</c>).
/// </summary>
public static class CodeHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "async", "await", "break", "case", "catch", "class", "const", "continue", "default", "delete", "do", "else", "export", "false", "finally", "for", "from", "function",
        "if", "import", "in", "instanceof", "let", "new", "null", "of", "return", "switch", "this", "throw", "true", "try", "typeof", "undefined", "var", "void", "while", "yield",
    };

    /// <summary>
    /// The code of an example: markup, or script when <paramref name="script"/> is set. Markup is indented and wrapped first (<see cref="HtmlFormatter"/>), so that the
    /// code reads the same whatever the example's source looked like; script is shown as it is written.
    /// </summary>
    public static string Of(string code, bool script) => script ? Script(code) : Html(HtmlFormatter.Pretty(code));

    /// <summary>Markup: tags, attribute names and values, comments.</summary>
    public static string Html(string code)
    {
        var result = new StringBuilder(code.Length * 2);
        var i = 0;
        while (i < code.Length)
        {
            if (code[i] == '<')
            {
                if (string.CompareOrdinal(code, i, "<!--", 0, 4) == 0)
                {
                    var end = code.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    end = end < 0 ? code.Length : end + 3;
                    Span(result, "com", code[i..end]);
                    i = end;
                    continue;
                }

                var end2 = TagEnd(code, i);
                if (end2 > 0)
                {
                    Tag(result, code[i..end2]);
                    i = end2;
                    continue;
                }
            }

            var next = code.IndexOf('<', i + 1);
            next = next < 0 ? code.Length : next;
            result.Append(WebUtility.HtmlEncode(code[i..next]));
            i = next;
        }

        return result.ToString();
    }

    /// <summary>Script: comments, strings, numbers and keywords.</summary>
    public static string Script(string code)
    {
        var result = new StringBuilder(code.Length * 2);
        var i = 0;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                var end = code.IndexOf('\n', i);
                end = end < 0 ? code.Length : end;
                Span(result, "com", code[i..end]);
                i = end;
            }
            else if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? code.Length : end + 2;
                Span(result, "com", code[i..end]);
                i = end;
            }
            else if (c is '\'' or '"' or '`')
            {
                var end = i + 1;
                while (end < code.Length && code[end] != c)
                {
                    end += code[end] == '\\' ? 2 : 1;
                }

                end = Math.Min(code.Length, end + 1);
                Span(result, "str", code[i..end]);
                i = end;
            }
            else if (char.IsDigit(c) && (i == 0 || !IsWord(code[i - 1])))
            {
                var end = i;
                while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] is '.' or '_'))
                {
                    end++;
                }

                Span(result, "num", code[i..end]);
                i = end;
            }
            else if (IsWord(c))
            {
                var end = i;
                while (end < code.Length && IsWord(code[end]))
                {
                    end++;
                }

                var word = code[i..end];
                if (Keywords.Contains(word) && (i == 0 || code[i - 1] != '.'))
                {
                    Span(result, "kw", word);
                }
                else
                {
                    result.Append(WebUtility.HtmlEncode(word));
                }

                i = end;
            }
            else
            {
                result.Append(WebUtility.HtmlEncode(c.ToString()));
                i++;
            }
        }

        return result.ToString();
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    private static void Span(StringBuilder result, string kind, string text)
        => result.Append("<span class=\"mat-code__").Append(kind).Append("\">").Append(WebUtility.HtmlEncode(text)).Append("</span>");

    /// <summary>The index after the <c>&gt;</c> of the tag that starts at <paramref name="start"/>, or 0 when it is not a tag (a lone <c>&lt;</c> is text, so is a tag that never ends).</summary>
    private static int TagEnd(string code, int start)
    {
        var j = start + 1;
        if (j < code.Length && code[j] == '/')
        {
            j++;
        }

        if (j >= code.Length || !(char.IsLetter(code[j]) || code[j] == '!'))
        {
            return 0;
        }

        var quote = '\0';
        for (var k = j; k < code.Length; k++)
        {
            var c = code[k];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return k + 1;
            }
        }

        return 0;
    }

    /// <summary>One tag, from its <c>&lt;</c> to its <c>&gt;</c>: the name, the attribute names and the values.</summary>
    private static void Tag(StringBuilder result, string tag)
    {
        var i = 0;
        var open = tag.StartsWith("</", StringComparison.Ordinal) ? 2 : 1;
        Span(result, "pun", tag[..open]);
        i = open;

        var nameEnd = i;
        while (nameEnd < tag.Length && !char.IsWhiteSpace(tag[nameEnd]) && tag[nameEnd] != '>' && !(tag[nameEnd] == '/' && nameEnd + 1 < tag.Length && tag[nameEnd + 1] == '>'))
        {
            nameEnd++;
        }

        Span(result, "tag", tag[i..nameEnd]);
        i = nameEnd;

        while (i < tag.Length)
        {
            var c = tag[i];
            if (char.IsWhiteSpace(c))
            {
                result.Append(c);
                i++;
            }
            else if (c == '>' || c == '/')
            {
                Span(result, "pun", c.ToString());
                i++;
            }
            else
            {
                var end = i;
                while (end < tag.Length && !char.IsWhiteSpace(tag[end]) && tag[end] is not ('=' or '>') && !(tag[end] == '/' && end + 1 < tag.Length && tag[end + 1] == '>'))
                {
                    end++;
                }

                if (end == i)
                {
                    // a stray '=' with no name: show it as it is
                    Span(result, "pun", c.ToString());
                    i++;
                    continue;
                }

                Span(result, "attr", tag[i..end]);
                i = end;
                if (i < tag.Length && tag[i] == '=')
                {
                    Span(result, "pun", "=");
                    i++;
                    var valueEnd = i;
                    if (i < tag.Length && tag[i] is '"' or '\'')
                    {
                        var quote = tag[i];
                        valueEnd = tag.IndexOf(quote, i + 1);
                        valueEnd = valueEnd < 0 ? tag.Length : valueEnd + 1;
                    }
                    else
                    {
                        while (valueEnd < tag.Length && !char.IsWhiteSpace(tag[valueEnd]) && tag[valueEnd] != '>')
                        {
                            valueEnd++;
                        }
                    }

                    Span(result, "str", tag[i..valueEnd]);
                    i = valueEnd;
                }
            }
        }
    }
}
