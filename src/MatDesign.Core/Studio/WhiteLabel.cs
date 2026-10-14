using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// What a company asks of the white label build (docs/PRODUCT.md, stage 15): the class prefix that replaces <c>mat-</c> and the product name that replaces <c>MatUi</c>. The Studio keeps exactly
/// one, the item <see cref="ItemId"/> of the kind <see cref="Kind"/>. The prefix <c>mat</c> and the name <c>MatUi</c> are the toolkit's own: a setting that keeps both renames nothing.
/// </summary>
public sealed record WhiteLabelSetting(string Prefix, string Name)
{
    /// <summary>The kind of the Studio item that holds the setting.</summary>
    public const string Kind = "whitelabel";

    /// <summary>The id of the one item of that kind.</summary>
    public const string ItemId = "whitelabel";

    /// <summary>The prefix of the toolkit as it is written in the repository.</summary>
    public const string SourcePrefix = "mat";

    /// <summary>The name of the toolkit as it is written in the repository.</summary>
    public const string SourceName = "MatUi";

    /// <summary>The toolkit as it is: nothing is renamed.</summary>
    public static WhiteLabelSetting None { get; } = new(SourcePrefix, SourceName);

    /// <summary>True when the class prefix is renamed (anything but <c>mat</c>).</summary>
    [JsonIgnore]
    public bool RenamesPrefix => Prefix != SourcePrefix;

    /// <summary>True when the product name is renamed (anything but <c>MatUi</c>).</summary>
    [JsonIgnore]
    public bool RenamesName => Name != SourceName;

    /// <summary>True when the setting changes nothing.</summary>
    [JsonIgnore]
    public bool IsNoOp => !RenamesPrefix && !RenamesName;

    /// <summary>The name in lower case: the stem of the file names (<c>acmeui.css</c>, <c>acmeui-icons.svg</c>).</summary>
    [JsonIgnore]
    public string FileStem => Name.ToLowerInvariant();
}

/// <summary>What a rename did: the new text and how many replacements each rule made.</summary>
public sealed record RenameResult(string Text, IReadOnlyDictionary<string, int> Counts)
{
    public int Total => Counts.Values.Sum();
}

/// <summary>One kind of name the build renames, with an example of before and after for the page and the tests.</summary>
public sealed record WhiteLabelRule(string Id, string Name, string What, string Before, string After);

/// <summary>
/// The renamer of the white label build: gives a text of the toolkit (a stylesheet, the script, the sprite, a page, a JSON file) the prefix and the name of a company. It works on <b>token boundaries</b>:
/// a name is renamed only when no letter, digit, <c>_</c> or <c>-</c> stands in front of it (<c>format-</c>, <c>automat-</c>, <c>thermat-</c> stay), and an address with a scheme
/// (<c>https://...</c>) and the name of the repository's own tool <c>matui-check.js</c> are never touched. The rules, all in one pass over the text (what a rule writes is not looked at again, so a name can never be renamed twice):
/// <list type="bullet">
/// <item><c>class</c>: <c>mat-xxx</c> (classes, ids, animation names, the keys of the storage, the ids of the sprite <c>mat-i-</c> and <c>mat-il-</c>, the bare <c>"mat-"</c> of a selector).</item>
/// <item><c>property</c>: <c>--mat-xxx</c>. <c>data</c>: <c>data-mat-xxx</c> attributes. <c>event</c>: <c>mat:xxx</c>. <c>expando</c>: <c>_matXxx</c>, the private properties the script puts on elements.</item>
/// <item><c>name</c>: <c>MatUi</c> (the global <c>window.MatUi</c>, comments, the first line of the stylesheet). <c>file</c>: <c>matui</c> in a file name (<c>matui.css</c>, <c>matui-icons.svg</c>) becomes the name in lower case.</item>
/// </list>
/// The source of the toolkit is never edited for a brand: this runs on the copy that a release carries. It is pure (no files, no state) and idempotent.
/// </summary>
public static partial class WhiteLabel
{
    /// <summary>The prefixes that would collide with something that is not ours: attributes and state classes the toolkit and the browser know.</summary>
    public static readonly IReadOnlyList<string> ReservedPrefixes = ["data", "aria", "js", "is", "has", "on", "ng", "v", "x"];

    public const int MaxPrefixLength = 8;
    public const int MinPrefixLength = 2;
    public const int MinNameLength = 3;
    public const int MaxNameLength = 24;

    // One pass over the text. The first alternatives only protect what must not change; the others are the rules. The lookbehinds are the token boundaries.
    [GeneratedRegex(
        @"(?<url>\b[A-Za-z][A-Za-z0-9+.\-]*://[^\s""'<>`)\]}]*)"
        + @"|(?<keep>(?<![A-Za-z0-9_$-])matui-check(?![A-Za-z0-9_]))"
        + @"|(?<property>(?<![A-Za-z0-9_$-])--mat-)"
        + @"|(?<data>(?<![A-Za-z0-9_$-])data-mat-)"
        + @"|(?<class>(?<![A-Za-z0-9_$-])mat-)"
        + @"|(?<event>(?<![A-Za-z0-9_$-])mat:(?=[a-z]))"
        + @"|(?<expando>(?<![A-Za-z0-9_$])_mat(?=[A-Z]))"
        + @"|(?<name>(?<![A-Za-z0-9_$])MatUi(?![A-Za-z0-9_]))"
        + @"|(?<file>(?<![A-Za-z0-9_$-])matui(?![A-Za-z0-9_]))",
        RegexOptions.CultureInvariant)]
    private static partial Regex Tokens();

    [GeneratedRegex(@"^[a-z][a-z0-9]*$")]
    private static partial Regex PrefixPattern();

    [GeneratedRegex(@"^[A-Z][A-Za-z]*$")]
    private static partial Regex NamePattern();

    private static readonly string[] RuleGroups = ["class", "property", "data", "event", "expando", "name", "file"];

    /// <summary>The rules of the build, in the order of the page: what each one renames, with an example.</summary>
    public static IReadOnlyList<WhiteLabelRule> Rules(WhiteLabelSetting s) =>
    [
        new("class", "Classes, ids, storage keys, sprite ids", "Every mat- name at a token boundary: classes, ids, animation names, the keys of localStorage, the ids of the icon sprite (mat-i-, mat-il-).", "mat-btn mat-btn--primary", $"{s.Prefix}-btn {s.Prefix}-btn--primary"),
        new("property", "Custom properties", "Every --mat- property: the tokens and the properties of the components.", "--mat-radius", $"--{s.Prefix}-radius"),
        new("data", "Data attributes", "Only the attributes that start with data-mat-.", "data-mat-sprite", $"data-{s.Prefix}-sprite"),
        new("event", "Events", "The events of the script, mat:xxx.", "mat:theme", $"{s.Prefix}:theme"),
        new("expando", "Private properties of the script", "The _matXxx properties the script puts on elements.", "_matPicker", $"_{s.Prefix}Picker"),
        new("name", "Product name", "The global of the script, the comments, the first line of the stylesheet, the README.", "window.MatUi", $"window.{s.Name}"),
        new("file", "File names", "The name of every file, and every place that names it (the manifest, the README, the page of the styleguide): the name in lower case.", "matui.css, matui-icons.svg", $"{s.FileStem}.css, {s.FileStem}-icons.svg"),
    ];

    /// <summary>What a setting turns into, as a table of before and after: the rules, and the other names that follow (the archive, the manifest, the bundle).</summary>
    public static IReadOnlyList<(string Before, string After)> Examples(WhiteLabelSetting s) =>
    [
        ("mat-btn mat-btn--primary", Rename("mat-btn mat-btn--primary", s)),
        ("--mat-radius", Rename("--mat-radius", s)),
        ("data-mat-sprite", Rename("data-mat-sprite", s)),
        ("mat:theme", Rename("mat:theme", s)),
        ("localStorage 'mat-theme'", Rename("localStorage 'mat-theme'", s)),
        ("#mat-i-check", Rename("#mat-i-check", s)),
        ("window.MatUi", Rename("window.MatUi", s)),
        ("matui.css", Rename("matui.css", s)),
        ("matui.boot.js", Rename("matui.boot.js", s)),
        ("matui-icons.svg", Rename("matui-icons.svg", s)),
        ("matui.manifest.json", Rename("matui.manifest.json", s)),
        ("matui-0.1.0.zip", Rename("matui-0.1.0.zip", s)),
    ];

    /// <summary>Everything that is wrong with a setting; empty when it can be used. A setting that keeps the prefix mat and the name MatUi is valid and renames nothing.</summary>
    public static IReadOnlyList<Problem> Validate(WhiteLabelSetting setting)
    {
        var problems = new List<Problem>();
        var prefix = setting.Prefix ?? string.Empty;
        var name = setting.Name ?? string.Empty;

        if (prefix.Length < MinPrefixLength || prefix.Length > MaxPrefixLength)
        {
            problems.Add(new("Prefix", $"A prefix is {MinPrefixLength} to {MaxPrefixLength} characters."));
        }
        else if (!PrefixPattern().IsMatch(prefix))
        {
            problems.Add(new("Prefix", "A prefix is lower-case letters and digits and starts with a letter, like acme."));
        }
        else if (ReservedPrefixes.Contains(prefix, StringComparer.Ordinal))
        {
            problems.Add(new("Prefix", $"{prefix} is taken: it is the start of attributes and state classes the browser and the toolkit use (data-, aria-, is-, has-, ...)."));
        }
        else if (prefix.StartsWith("xml", StringComparison.Ordinal))
        {
            problems.Add(new("Prefix", "A prefix cannot start with xml: that is reserved for XML."));
        }
        else if (prefix == "matui")
        {
            problems.Add(new("Prefix", "matui is the lower-case name of the toolkit: choose your own prefix."));
        }

        if (name.Length < MinNameLength || name.Length > MaxNameLength)
        {
            problems.Add(new("Name", $"A name is {MinNameLength} to {MaxNameLength} letters."));
        }
        else if (!NamePattern().IsMatch(name))
        {
            problems.Add(new("Name", "A name is letters only and starts with a capital letter, like AcmeUi."));
        }
        else if (name == WhiteLabelSetting.SourceName && prefix != WhiteLabelSetting.SourcePrefix)
        {
            problems.Add(new("Name", "MatUi is the name of the toolkit itself: give the product your own name (or keep the prefix mat, which renames nothing)."));
        }
        else if (name != WhiteLabelSetting.SourceName && (name.Equals(WhiteLabelSetting.SourceName, StringComparison.OrdinalIgnoreCase) || name.Equals("Mat", StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new("Name", "That name only differs from the toolkit's own in its capitals, or is its first three letters: it would be taken for it."));
        }

        return problems;
    }

    /// <summary>The setting as it is stored: trimmed, the prefix in lower case; an empty text becomes the toolkit's own, which renames nothing.</summary>
    public static WhiteLabelSetting Normalize(WhiteLabelSetting setting)
        => new(string.IsNullOrWhiteSpace(setting.Prefix) ? WhiteLabelSetting.SourcePrefix : setting.Prefix.Trim().ToLowerInvariant(),
               string.IsNullOrWhiteSpace(setting.Name) ? WhiteLabelSetting.SourceName : setting.Name.Trim());

    /// <summary>True when the setting is valid; used again for an item read from the disk, where a person could have edited it into something a stylesheet cannot carry.</summary>
    public static bool IsSafe(WhiteLabelSetting setting) => Validate(setting).Count == 0;

    /// <summary>The text with the prefix and the name of <paramref name="setting"/>.</summary>
    public static string Rename(string text, WhiteLabelSetting setting) => RenameCounted(text, setting).Text;

    /// <summary>The text with the prefix and the name of <paramref name="setting"/>, and how often each rule applied.</summary>
    public static RenameResult RenameCounted(string text, WhiteLabelSetting setting)
    {
        var counts = RuleGroups.ToDictionary(g => g, _ => 0, StringComparer.Ordinal);
        if (setting.IsNoOp || text.Length == 0)
        {
            return new RenameResult(text, counts);
        }

        var prefix = setting.Prefix;
        var stem = setting.FileStem;
        var result = Tokens().Replace(text, match =>
        {
            string? Rule(string group) => match.Groups[group].Success ? group : null;
            var group = Rule("property") ?? Rule("data") ?? Rule("class") ?? Rule("event") ?? Rule("expando") ?? Rule("name") ?? Rule("file");
            if (group is null)
            {
                return match.Value;
            }

            if (group is "property" or "data" or "class" or "event" or "expando")
            {
                if (!setting.RenamesPrefix)
                {
                    return match.Value;
                }

                counts[group]++;
                return group switch
                {
                    "property" => $"--{prefix}-",
                    "data" => $"data-{prefix}-",
                    "class" => $"{prefix}-",
                    "event" => $"{prefix}:",
                    _ => $"_{prefix}",
                };
            }

            if (!setting.RenamesName)
            {
                return match.Value;
            }

            counts[group]++;
            return group == "name" ? setting.Name : stem;
        });
        return new RenameResult(result, counts);
    }

    /// <summary>The name of a file or a path in a release: the same rules, so <c>css/matui.tokens.css</c> becomes <c>css/acmeui.tokens.css</c>.</summary>
    public static string RenamePath(string path, WhiteLabelSetting setting) => Rename(path, setting);

    /// <summary>
    /// A JSON text with every string and every property name renamed. JSON is not renamed as text: the serializer writes a quote as <c>"</c>, and the name that follows it would then
    /// stand behind a digit and look like the middle of a word. The values are renamed as the strings they are, and the document is written again with <paramref name="options"/>.
    /// </summary>
    public static RenameResult RenameJson(string json, WhiteLabelSetting setting, JsonSerializerOptions options)
    {
        var counts = RuleGroups.ToDictionary(g => g, _ => 0, StringComparer.Ordinal);
        if (setting.IsNoOp)
        {
            return new RenameResult(json, counts);
        }

        string One(string text)
        {
            var renamed = RenameCounted(text, setting);
            foreach (var pair in renamed.Counts)
            {
                counts[pair.Key] += pair.Value;
            }

            return renamed.Text;
        }

        JsonNode? Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    var copy = new JsonObject();
                    foreach (var pair in obj.ToList())
                    {
                        copy[One(pair.Key)] = Walk(pair.Value is null ? null : pair.Value.DeepClone());
                    }

                    return copy;
                case JsonArray array:
                    var items = new JsonArray();
                    foreach (var item in array)
                    {
                        items.Add(Walk(item?.DeepClone()));
                    }

                    return items;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    return JsonValue.Create(One(text));
                default:
                    return node?.DeepClone();
            }
        }

        var root = Walk(JsonNode.Parse(json));
        return new RenameResult(root is null ? "null" : root.ToJsonString(options), counts);
    }

    /// <summary>
    /// The places in an already renamed text where a name of the toolkit is still standing: what the white label checks look for. Addresses and the tool <c>matui-check.js</c> do not count. Only the rules that
    /// <paramref name="setting"/> applies are looked at (a prefix that stays <c>mat</c> leaves every mat- name alone).
    /// </summary>
    public static IReadOnlyList<(int Line, string Rule, string Text)> Leftovers(string text, WhiteLabelSetting setting)
    {
        var found = new List<(int, string, string)>();
        if (setting.IsNoOp)
        {
            return found;
        }

        foreach (Match match in Tokens().Matches(text))
        {
            var group = RuleGroups.FirstOrDefault(g => match.Groups[g].Success);
            if (group is null || (!setting.RenamesPrefix && group is "property" or "data" or "class" or "event" or "expando") || (!setting.RenamesName && group is "name" or "file"))
            {
                continue;
            }

            var line = text.AsSpan(0, match.Index).Count('\n') + 1;
            var start = Math.Max(0, match.Index - 20);
            var snippet = text.Substring(start, Math.Min(text.Length - start, 60)).Replace('\n', ' ').Replace('\r', ' ');
            found.Add((line, group, snippet));
        }

        return found;
    }

    /// <summary>The name of a class, a property or an id as it stands in the toolkit, in the renamed form (what a name of the source becomes).</summary>
    public static string RenameToken(string token, WhiteLabelSetting setting) => Rename(token, setting);

    /// <summary>The names of a file in a release that belong to the toolkit: the bundle, the script, the boot script, the sprite, the parts, the archive, the manifest.</summary>
    public static IEnumerable<string> ToolkitFileNames(string stem, string version)
    {
        yield return $"{stem}.css";
        yield return $"{stem}.js";
        yield return $"{stem}.boot.js";
        yield return $"{stem}-icons.svg";
        yield return $"{stem}.manifest.json";
        yield return $"{stem}-{version}.zip";
    }

    /// <summary>The text of a rule for a person: the examples joined, used in the log of a run.</summary>
    public static string Describe(WhiteLabelSetting setting, IReadOnlyDictionary<string, int> counts)
    {
        var sb = new StringBuilder();
        sb.Append(setting.RenamesPrefix ? $"prefix mat- to {setting.Prefix}-" : "prefix kept");
        sb.Append(", ");
        sb.Append(setting.RenamesName ? $"name MatUi to {setting.Name}" : "name kept");
        sb.Append(": ");
        sb.Append(string.Join(", ", RuleGroups.Where(g => counts.GetValueOrDefault(g) > 0).Select(g => $"{counts[g]} {g}")));
        return sb.ToString();
    }
}
