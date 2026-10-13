using System.Text;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// Indents the markup of an example for showing it as code: two spaces per level, one element per line where it has structure, a short element with only text and
/// inline elements on one line, and a long opening tag with one attribute per line under the first. It changes white space and nothing else: the tags, the attributes,
/// the text and their order stay as they are (a unit test holds that for every example of the catalogue), and a text that cannot be read as markup (a tag that is
/// never closed) is returned as it is, only dedented. <c>pre</c>, <c>textarea</c>, <c>script</c> and <c>style</c> keep their content untouched; an <c>svg</c> (an icon)
/// stays on one line.
/// </summary>
public static partial class HtmlFormatter
{
    /// <summary>The line length that the formatter works for: the code of an example is shown beside the example, in a column of about this many characters.</summary>
    public const int DefaultWidth = 80;

    private const int Step = 2;

    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };

    private static readonly HashSet<string> VerbatimElements = new(StringComparer.OrdinalIgnoreCase) { "pre", "textarea", "script", "style" };

    private static readonly HashSet<string> InlineElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "abbr", "b", "bdi", "bdo", "br", "button", "cite", "code", "data", "dfn", "em", "i", "img", "input", "kbd", "label", "mark", "output", "q", "s", "samp",
        "small", "span", "strong", "sub", "sup", "svg", "time", "u", "var",
    };

    [GeneratedRegex(@"\G<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9:-]*)(?<attrs>(?:\s+[^\s""'>/=]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'>]+))?)*)\s*(?<self>/)?>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+(?<n>[^\s""'>/=]+)(?:\s*=\s*(?<v>""[^""]*""|'[^']*'|[^\s""'>]+))?")]
    private static partial Regex AttributePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[ \t]*\n[ \t]*")]
    private static partial Regex LineBreak();

    private enum Kind
    {
        Root,
        Element,
        Text,
        Comment,
    }

    private sealed class Node(Kind kind)
    {
        public Kind Kind { get; } = kind;

        /// <summary>The name of an element as written; the text of a text node (white space collapsed) or of a comment.</summary>
        public string Name { get; init; } = string.Empty;

        public List<string> Attributes { get; } = [];

        public List<Node> Children { get; } = [];

        /// <summary>The tag ends with a slash (<c>&lt;input /&gt;</c>), a void element or not.</summary>
        public bool Slash { get; set; }

        /// <summary>The content of a verbatim element or of an svg, which is not parsed.</summary>
        public string? Inner { get; set; }

        public bool IsVoid => Kind == Kind.Element && (VoidElements.Contains(Name) || Slash);

        public bool IsText => Kind == Kind.Text;
    }

    /// <summary>The markup of <paramref name="html"/> formatted for showing, with lines of at most <paramref name="width"/> characters where the markup allows it.</summary>
    public static string Pretty(string html, int width = DefaultWidth)
    {
        var text = Dedent(html);
        if (text.Length == 0)
        {
            return text;
        }

        var nodes = Parse(text);
        if (nodes is null)
        {
            return text;
        }

        var lines = new List<string>();
        Children(nodes, 0, lines, width);
        return string.Join('\n', lines);
    }

    /// <summary>The text without its common indentation, its blank lines at the start and the end and its trailing spaces; line breaks are <c>\n</c>.</summary>
    public static string Dedent(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var indent = lines.Where(l => l.Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join('\n', lines.Select(l => l.Length >= indent ? l[indent..] : l));
    }

    // ------------------------------------------------------------------------------------------------------- reading the markup

    private static List<Node>? Parse(string html)
    {
        var root = new Node(Kind.Root);
        var stack = new Stack<Node>();
        stack.Push(root);
        var i = 0;
        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
                {
                    var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        return null;
                    }

                    stack.Peek().Children.Add(new Node(Kind.Comment) { Name = html[i..(end + 3)] });
                    i = end + 3;
                    continue;
                }

                if (i + 1 < html.Length && html[i + 1] == '!')
                {
                    var end = html.IndexOf('>', i);
                    if (end < 0)
                    {
                        return null;
                    }

                    stack.Peek().Children.Add(new Node(Kind.Comment) { Name = html[i..(end + 1)] });
                    i = end + 1;
                    continue;
                }

                var tag = TagPattern().Match(html, i);
                if (tag.Success)
                {
                    var name = tag.Groups["name"].Value;
                    i = tag.Index + tag.Length;
                    if (tag.Groups["close"].Success)
                    {
                        // an element that is not closed before its parent is, or a closing tag without an opening one: markup that only a browser can make sense of
                        if (stack.Peek().Kind != Kind.Element || !stack.Peek().Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }

                        stack.Pop();
                        continue;
                    }

                    var element = new Node(Kind.Element) { Name = name, Slash = tag.Groups["self"].Success };
                    foreach (Match attribute in AttributePattern().Matches(tag.Groups["attrs"].Value))
                    {
                        element.Attributes.Add(attribute.Groups["v"].Success
                            ? attribute.Groups["n"].Value + "=" + LineBreak().Replace(attribute.Groups["v"].Value, " ")
                            : attribute.Groups["n"].Value);
                    }

                    stack.Peek().Children.Add(element);
                    if (element.IsVoid)
                    {
                        continue;
                    }

                    if (VerbatimElements.Contains(name) || name.Equals("svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var close = IndexOfClose(html, i, name);
                        if (close.Start < 0)
                        {
                            return null;
                        }

                        var inner = html[i..close.Start];
                        element.Inner = name.Equals("svg", StringComparison.OrdinalIgnoreCase) ? Spaces().Replace(inner, " ").Trim() : inner;
                        i = close.End;
                        continue;
                    }

                    stack.Push(element);
                    continue;
                }
            }

            var next = html.IndexOf('<', i + 1);
            next = next < 0 ? html.Length : next;
            stack.Peek().Children.Add(new Node(Kind.Text) { Name = Spaces().Replace(html[i..next], " ") });
            i = next;
        }

        return stack.Count == 1 ? root.Children : null;
    }

    /// <summary>The place of the closing tag of <paramref name="name"/> from <paramref name="from"/> on, for an element whose content is not read (an svg may hold an svg).</summary>
    private static (int Start, int End) IndexOfClose(string html, int from, string name)
    {
        var depth = 1;
        var open = new Regex($@"<(?<c>/)?{Regex.Escape(name)}(?=[\s/>])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match m in open.Matches(html, from))
        {
            depth += m.Groups["c"].Success ? -1 : (m.Value.EndsWith("/>", StringComparison.Ordinal) ? 0 : 1);
            if (depth == 0)
            {
                return (m.Index, m.Index + m.Length);
            }
        }

        return (-1, -1);
    }

    // ------------------------------------------------------------------------------------------------------- writing it

    private static bool IsInline(Node node)
        => node.Kind == Kind.Text
           || (node.Kind == Kind.Element && InlineElements.Contains(node.Name) && node.Children.All(IsInline));

    private static string Pad(int indent) => new(' ', indent);

    private static string OpenTag(Node element)
        => "<" + element.Name + string.Concat(element.Attributes.Select(a => " " + a)) + (element.Slash ? " /" : string.Empty) + ">";

    private static string CloseTag(Node element) => "</" + element.Name + ">";

    /// <summary>The whole element on one line, its content as it is written (text with single spaces).</summary>
    private static string OneLine(Node element)
    {
        if (element.IsVoid)
        {
            return OpenTag(element);
        }

        var sb = new StringBuilder(OpenTag(element));
        if (element.Inner is not null)
        {
            sb.Append(element.Inner);
        }
        else
        {
            sb.Append(Inline(element.Children).Trim());
        }

        return sb.Append(CloseTag(element)).ToString();
    }

    private static string Inline(IReadOnlyList<Node> nodes)
    {
        var sb = new StringBuilder();
        foreach (var node in nodes)
        {
            sb.Append(node.Kind == Kind.Text ? node.Name : node.Kind == Kind.Comment ? node.Name : OneLine(node));
        }

        return sb.ToString();
    }

    /// <summary>The opening tag as lines: on one line when it fits, else one attribute per line under the first.</summary>
    private static List<string> OpenLines(Node element, int indent, int width)
    {
        var single = Pad(indent) + OpenTag(element);
        if (single.Length <= width || element.Attributes.Count < 2)
        {
            return [single];
        }

        var lines = new List<string> { Pad(indent) + "<" + element.Name + " " + element.Attributes[0] };
        var column = Pad(indent + 1 + element.Name.Length + 1);
        for (var i = 1; i < element.Attributes.Count; i++)
        {
            lines.Add(column + element.Attributes[i]);
        }

        lines[^1] += element.Slash ? " />" : ">";
        return lines;
    }

    private static void Children(IReadOnlyList<Node> nodes, int indent, List<string> lines, int width)
    {
        var run = new List<Node>();
        void Flush()
        {
            if (run.Count > 0)
            {
                Run(run, indent, lines, width);
                run = [];
            }
        }

        foreach (var node in nodes)
        {
            if (IsInline(node))
            {
                run.Add(node);
                continue;
            }

            Flush();
            if (node.Kind == Kind.Comment)
            {
                lines.Add(Pad(indent) + node.Name);
            }
            else
            {
                Element(node, indent, lines, width);
            }
        }

        Flush();
    }

    /// <summary>A run of text and inline elements: one line when it fits, else the elements on lines of their own and the words of the text wrapped.</summary>
    private static void Run(List<Node> run, int indent, List<string> lines, int width)
    {
        var whole = Inline(run).Trim();
        if (whole.Length == 0)
        {
            return;
        }

        if (indent + whole.Length <= width)
        {
            lines.Add(Pad(indent) + whole);
            return;
        }

        var words = new StringBuilder();
        void FlushWords()
        {
            var text = words.ToString().Trim();
            words.Clear();
            if (text.Length == 0)
            {
                return;
            }

            var line = new StringBuilder();
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && indent + line.Length + 1 + word.Length > width)
                {
                    lines.Add(Pad(indent) + line);
                    line.Clear();
                }

                line.Append(line.Length > 0 ? " " : string.Empty).Append(word);
            }

            if (line.Length > 0)
            {
                lines.Add(Pad(indent) + line);
            }
        }

        foreach (var node in run)
        {
            if (node.IsText)
            {
                words.Append(node.Name);
                continue;
            }

            FlushWords();
            Element(node, indent, lines, width);
        }

        FlushWords();
    }

    private static void Element(Node element, int indent, List<string> lines, int width)
    {
        if (element.Inner is not null && element.Name.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(Pad(indent) + OneLine(element));
            return;
        }

        if (element.Inner is not null)
        {
            // pre, textarea, script, style: the content is exactly as written, the tags are placed
            var raw = OpenTag(element) + element.Inner + CloseTag(element);
            var parts = raw.Split('\n');
            lines.Add(Pad(indent) + parts[0]);
            lines.AddRange(parts.Skip(1));
            return;
        }

        if (element.IsVoid)
        {
            lines.AddRange(OpenLines(element, indent, width));
            return;
        }

        if (element.Children.All(IsInline))
        {
            var single = Pad(indent) + OneLine(element);
            if (single.Length <= width || element.Children.Count == 0)
            {
                lines.Add(single);
                return;
            }
        }

        lines.AddRange(OpenLines(element, indent, width));
        Children(element.Children, indent + Step, lines, width);
        lines.Add(Pad(indent) + CloseTag(element));
    }
}
