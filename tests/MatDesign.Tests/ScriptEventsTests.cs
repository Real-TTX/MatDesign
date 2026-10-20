using System.Text.RegularExpressions;

namespace MatDesign.Tests;

/// <summary>
/// The events the script sends (CODE-13, docs/VERSIONING.md: events and their detail are part of the contract). Every `new CustomEvent('mat:...')` of matui.js is in the header of the script and in the
/// table of docs/ADOPTING.md (section 3b), and the table says what the code does: where the event is sent, whether it bubbles, whether it can be cancelled, which keys its `detail` has. An event that is
/// added without a row, a row without an event and a row that says something else than the code all fail here.
/// </summary>
public partial class ScriptEventsTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");

    private static readonly string Adopting = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md"));

    private static string Header => Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];

    /// <summary>One place in the script that sends an event: its name, the options it passes, who it is sent on (when it is written as `x.dispatchEvent(new CustomEvent(...))`) and the keys of a literal `detail`.</summary>
    private sealed record Site(string Name, bool Bubbles, bool Cancelable, string? Receiver, IReadOnlyList<string>? DetailKeys);

    /// <summary>One row of the table in ADOPTING.md.</summary>
    private sealed record Row(string Name, string SentOn, string Detail, bool Bubbles, bool Cancelable);

    [GeneratedRegex(@"new (?:Custom)?Event\(\s*(?<q>['""])(?<name>mat:[^'""]+)\k<q>")]
    private static partial Regex EventSite();

    [GeneratedRegex(@"(?<![\w$])(?<receiver>[\w$]+(?:\.[\w$]+)*)\.dispatchEvent\(\s*$")]
    private static partial Regex ReceiverBefore();

    [GeneratedRegex(@"mat:[a-z]+(?:-[a-z]+)+")]
    private static partial Regex EventName();

    [GeneratedRegex(@"(?m)^\s+(?<name>mat:[a-z]+(?:-[a-z]+)*)\s")]
    private static partial Regex HeaderEventLine();

    /// <summary>The text from the brace at <paramref name="open"/> to its closing brace, outside of strings.</summary>
    private static string Block(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\'' or '"')
            {
                for (i++; text[i] != c; i += text[i] == '\\' ? 2 : 1)
                {
                }

                continue;
            }

            depth += c is '{' or '[' or '(' ? 1 : c is '}' or ']' or ')' ? -1 : 0;
            if (depth == 0)
            {
                return text[open..(i + 1)];
            }
        }

        throw new InvalidOperationException("no closing brace after " + text[open..Math.Min(text.Length, open + 60)]);
    }

    /// <summary>The members of an object literal `{ a: 1, b: { c: 2 } }` at its first level: (key, value text).</summary>
    private static List<(string Key, string Value)> Members(string literal)
    {
        var members = new List<(string, string)>();
        var inner = literal[1..^1];
        var depth = 0;
        var start = 0;
        void Add(int end)
        {
            var part = inner[start..end].Trim();
            var colon = part.IndexOf(':', StringComparison.Ordinal);
            if (part.Length > 0 && colon > 0)
            {
                members.Add((part[..colon].Trim(), part[(colon + 1)..].Trim()));
            }
        }

        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c is '\'' or '"')
            {
                for (i++; inner[i] != c; i += inner[i] == '\\' ? 2 : 1)
                {
                }

                continue;
            }

            depth += c is '{' or '[' or '(' ? 1 : c is '}' or ']' or ')' ? -1 : 0;
            if (c == ',' && depth == 0)
            {
                Add(i);
                start = i + 1;
            }
        }

        Add(inner.Length);
        return members;
    }

    private static List<Site> Sites()
    {
        var sites = new List<Site>();
        foreach (Match m in EventSite().Matches(Js))
        {
            var at = m.Index + m.Length;
            while (char.IsWhiteSpace(Js[at]))
            {
                at++;
            }

            var bubbles = false;
            var cancelable = false;
            IReadOnlyList<string>? keys = null;
            if (Js[at] == ',')
            {
                at++;
                while (char.IsWhiteSpace(Js[at]))
                {
                    at++;
                }

                Assert.Equal('{', Js[at]);
                var options = Members(Block(Js, at));
                bubbles = options.Any(o => o is { Key: "bubbles", Value: "true" });
                cancelable = options.Any(o => o is { Key: "cancelable", Value: "true" });
                var detail = options.FirstOrDefault(o => o.Key == "detail");
                if (detail.Value is not null && detail.Value.StartsWith('{'))
                {
                    keys = Members(detail.Value).Select(d => d.Key).ToList();
                }
            }

            var before = Js[Math.Max(0, m.Index - 80)..m.Index];
            var receiver = ReceiverBefore().Match(before);
            sites.Add(new Site(m.Groups["name"].Value, bubbles, cancelable, receiver.Success ? receiver.Groups["receiver"].Value : null, keys));
        }

        return sites;
    }

    private static List<Row> Table()
    {
        var start = Adopting.IndexOf("## 3b. Events the script sends", StringComparison.Ordinal);
        Assert.True(start >= 0, "ADOPTING.md has no section 3b, Events the script sends");
        var end = Adopting.IndexOf("\n## ", start + 10, StringComparison.Ordinal);
        var section = Adopting[start..(end < 0 ? Adopting.Length : end)];
        var rows = new List<Row>();
        foreach (var line in section.Split('\n').Where(l => l.StartsWith("| `mat:", StringComparison.Ordinal)))
        {
            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            Assert.True(cells.Length == 6, $"a row of the table of events has {cells.Length} cells, not 6: {line}");
            Assert.Contains(cells[4], new[] { "yes", "no" });
            Assert.Contains(cells[5], new[] { "yes", "no" });
            rows.Add(new Row(cells[0].Trim('`'), cells[1], cells[3], cells[4] == "yes", cells[5] == "yes"));
        }

        return rows;
    }

    [Fact]
    public void The_script_sends_a_number_of_events_that_follow_one_way_of_naming()
    {
        var names = Sites().Select(s => s.Name).Distinct().ToList();
        Assert.True(names.Count >= 23, string.Join(", ", names));
        Assert.All(names, n => Assert.Matches(@"^mat:[a-z]+(-[a-z]+)*$", n));
        // no event is sent in a way that this test cannot see: every name that is written in a string of the code is one of the sites (the header is documentation)
        var code = Js[Header.Length..];
        var written = EventName().Matches(code).Select(m => m.Value).Concat(Regex.Matches(code, @"['""](mat:[a-z]+)['""]").Select(m => m.Groups[1].Value)).Distinct().Except(names).ToList();
        Assert.True(written.Count == 0, "the script mentions an event that is not sent by `new CustomEvent('...')`, which the documentation test cannot see: " + string.Join(", ", written));
    }

    [Fact]
    public void Every_event_the_script_sends_is_a_row_of_the_table_in_ADOPTING_and_every_row_is_an_event()
    {
        var sent = Sites().Select(s => s.Name).Distinct().Order().ToList();
        var documented = Table().Select(r => r.Name).Order().ToList();
        Assert.True(documented.Count == documented.Distinct().Count(), "an event is twice in the table of ADOPTING.md");
        Assert.True(sent.SequenceEqual(documented), $"sent but not in ADOPTING.md: {string.Join(", ", sent.Except(documented))}; in ADOPTING.md but never sent: {string.Join(", ", documented.Except(sent))}");
    }

    [Fact]
    public void Every_event_the_script_sends_is_in_its_header_and_the_header_names_no_other()
    {
        var sent = Sites().Select(s => s.Name).Distinct().Order().ToList();
        // the Events block is the end of the header (the attributes above mention events in their own lines)
        var block = Header[Header.IndexOf("   Events (CustomEvents", StringComparison.Ordinal)..];
        var lines = HeaderEventLine().Matches(block).Select(m => m.Groups["name"].Value).ToList();
        Assert.True(lines.Count == lines.Distinct().Count(), "an event is twice in the Events block of the header of matui.js");
        Assert.True(sent.SequenceEqual(lines.Order()), $"sent but not in the Events block of the header: {string.Join(", ", sent.Except(lines))}; there but never sent: {string.Join(", ", lines.Except(sent))}");
        var named = EventName().Matches(Header).Select(m => m.Value).Distinct().Except(sent).ToList();
        Assert.True(named.Count == 0, "the header names an event that is never sent: " + string.Join(", ", named));
    }

    [Fact]
    public void The_table_says_whether_an_event_bubbles_and_whether_it_can_be_cancelled_as_the_code_does()
    {
        var sites = Sites().GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var row in Table())
        {
            var found = sites[row.Name];
            Assert.True(found.All(s => s.Bubbles == found[0].Bubbles && s.Cancelable == found[0].Cancelable), $"{row.Name} is sent with other options in one place than in another");
            Assert.True(row.Bubbles == found[0].Bubbles, $"{row.Name}: the table says bubbles {(row.Bubbles ? "yes" : "no")}, the code {(found[0].Bubbles ? "yes" : "no")}");
            Assert.True(row.Cancelable == found[0].Cancelable, $"{row.Name}: the table says cancelable {(row.Cancelable ? "yes" : "no")}, the code {(found[0].Cancelable ? "yes" : "no")}");
        }
    }

    [Fact]
    public void An_event_that_does_not_bubble_is_the_one_sent_on_the_root_element_and_the_table_says_so()
    {
        var sites = Sites().GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var row in Table())
        {
            var onRoot = sites[row.Name].Where(s => s.Receiver is not null).All(s => s.Receiver == "root");
            var anywhereElse = sites[row.Name].Any(s => s.Receiver is not null and not "root");
            if (row.SentOn == "`<html>`")
            {
                Assert.True(onRoot && !row.Bubbles, $"{row.Name} is said to be sent on <html>: it is sent on {string.Join(", ", sites[row.Name].Select(s => s.Receiver))} and bubbles {row.Bubbles}");
            }
            else
            {
                Assert.False(sites[row.Name].Any(s => s.Receiver == "root"), $"{row.Name} is sent on the root element but the table says {row.SentOn}");
                Assert.True(anywhereElse || sites[row.Name].All(s => s.Receiver is null), $"{row.Name}: where it is sent could not be read");
                Assert.True(row.Bubbles, $"{row.Name} is not sent on <html> and does not bubble: nobody could hear it");
            }
        }

        // what the text above the table says: the four, and the three
        var section = Adopting[Adopting.IndexOf("## 3b. Events the script sends", StringComparison.Ordinal)..];
        var rootOnes = Table().Where(r => r.SentOn == "`<html>`").Select(r => r.Name).Order().ToList();
        Assert.Equal(["mat:accent", "mat:navset", "mat:scheme", "mat:theme"], rootOnes);
        var line = section.Split('\n').Single(l => l.StartsWith("- **On `<html>`, not bubbling:**", StringComparison.Ordinal));
        Assert.All(rootOnes, n => Assert.Contains(n, line));
        var cancelable = Table().Where(r => r.Cancelable).Select(r => r.Name).Order().ToList();
        Assert.Equal(["mat:otp-complete", "mat:schedule-select", "mat:search-select"], cancelable);
        var cancel = section.Split('\n').Single(l => l.StartsWith("- **Cancelable:**", StringComparison.Ordinal));
        Assert.All(cancelable, n => Assert.Contains(n, cancel));
    }

    [Fact]
    public void Every_key_of_a_detail_that_the_script_writes_is_named_in_the_row_of_its_event()
    {
        var rows = Table().ToDictionary(r => r.Name);
        var checkedKeys = 0;
        foreach (var site in Sites().Where(s => s.DetailKeys is not null))
        {
            foreach (var key in site.DetailKeys!)
            {
                Assert.True(Regex.IsMatch(rows[site.Name].Detail, $@"`{Regex.Escape(key)}`"), $"{site.Name}: the detail has `{key}` in the code and the table does not name it: {rows[site.Name].Detail}");
                checkedKeys++;
            }
        }

        Assert.True(checkedKeys >= 50, $"only {checkedKeys} keys of a detail were found: the parser lost its way");
        // and the other way round: a key that the table names is one the code sends (the ones that are not written out as a literal are checked by hand: schedule-select sends the event of the app)
        foreach (var row in Table())
        {
            var literals = Sites().Where(s => s.Name == row.Name && s.DetailKeys is not null).SelectMany(s => s.DetailKeys!).ToHashSet();
            if (literals.Count == 0)
            {
                continue;
            }

            foreach (Match named in Regex.Matches(row.Detail.Split(':')[0], @"`([a-z]+)`"))
            {
                Assert.True(literals.Contains(named.Groups[1].Value), $"{row.Name}: the table names `{named.Groups[1].Value}` as a key of the detail, the code has {string.Join(", ", literals)}");
            }
        }
    }

    [Fact]
    public void The_header_and_the_guide_say_where_the_list_is_kept_and_that_a_new_event_goes_into_it()
    {
        Assert.Contains("section 3b", Header);
        Assert.Contains("a test holds both to the code", Header);
        Assert.Contains("a new one is added to this table", Adopting);
    }
}
