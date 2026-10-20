using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Pages.Foundations;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Services;

/// <summary>
/// The search of the site (docs/ARCHITECTURE.md, "Search"): rules, components, tokens, icons, the pages of the menu, the apps and the patterns, found by keywords. It is built in memory from what
/// <see cref="DesignData"/> merges (the built-in data and what the Studio saved) and rebuilt when <see cref="DesignData.Version"/> moves; a query reads a snapshot, so it never waits for a
/// rebuild and never sees half of one. The Studio's own content, the changelog and the documents are not searched.
/// <para>
/// A document has fields with weights (title, id, keywords, code, group, references, text). A word of the query is looked up in the vocabulary of all documents: the same word, a word it starts,
/// a word that holds it, and, for a word nothing starts, a word one or two letters off (a typo); and in the synonyms of <see cref="SearchSynonyms"/>. Every word of the query has to be found in a
/// document (AND), each with the score of its best place; the exact id of a rule, the exact title and the start of a title add a bonus and the kinds are weighted.
/// </para>
/// </summary>
public sealed partial class SearchIndex
{
    /// <summary>The longest query that is looked at: the rest is cut off.</summary>
    public const int MaxQueryLength = 100;

    private const int MaxTerms = 8;
    private const int MaxCached = 1000;

    private enum Field : byte
    {
        Title,
        Id,
        Keywords,
        Code,
        Group,
        Refs,
        Body,
    }

    // title, id, keywords, code (classes, attributes, values), group, references to rules, text
    private static readonly double[] Weights = [10, 9, 7, 6, 3, 2, 1.5];

    // how much a kind counts when two things match equally well
    private static readonly Dictionary<SearchKind, double> KindWeight = new()
    {
        [SearchKind.Component] = 1.2,
        [SearchKind.Page] = 1.25,
        [SearchKind.App] = 1.05,
        [SearchKind.Rule] = 1.0,
        [SearchKind.Pattern] = 1.0,
        [SearchKind.Token] = 0.85,
        [SearchKind.Icon] = 0.7,
    };

    // an id as people write it: "form-15", "FORM 15", "form15" (an area with a digit in it, A11Y, needs a separator)
    [GeneratedRegex(@"^([a-z][a-z0-9]{1,5})[\s_-]+(\d{1,2})$|^([a-z]{3,6})(\d{1,2})$")]
    private static partial Regex RuleIdPattern();

    // little words (English and German, folded) that a longer query may leave out: "dark mode" finds what is dark whether or not it says mode
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "of", "for", "to", "in", "on", "and", "or", "with", "how", "do", "does", "is", "are", "i", "my", "mode", "modus",
        "der", "die", "das", "ein", "eine", "und", "oder", "mit", "fur", "von", "zu", "im", "wie", "ich", "mein",
    };

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{2,5}-\d{2}\b")]
    private static partial Regex RuleReference();

    private readonly DesignData _design;
    private readonly ToolkitFiles _toolkit;
    private readonly IconLibrary _icons;
    private readonly object _gate = new();
    private volatile Snapshot? _snapshot;

    public SearchIndex(DesignData design, ToolkitFiles toolkit, IconLibrary icons)
    {
        _design = design;
        _toolkit = toolkit;
        _icons = icons;
    }

    private readonly record struct Posting(int Doc, byte Field, byte Position);

    /// <param name="Doc">What is shown and where it leads.</param>
    /// <param name="ScoreTitle">The title that is searched when it is not the shown one (a rule is shown as <c>FORM-21 File upload</c> and searched as <c>File upload</c>, its id being a field of its own).</param>
    private sealed record Entry(SearchDoc Doc, string? ScoreTitle = null, string? Id = null, string? Keywords = null, string? Code = null, string? Group = null, string? Refs = null, string? Body = null);

    private sealed class Snapshot
    {
        public required long Version { get; init; }

        public required SearchDoc[] Docs { get; init; }

        /// <summary>The sentences of each document (title, keywords, text) as folded words, to find a phrase that was typed as it is written.</summary>
        public required string[] Phrases { get; init; }

        /// <summary>The words of the title of each document, folded and joined with a space.</summary>
        public required string[] TitleKeys { get; init; }

        /// <summary>The same without the plurals (<see cref="SearchText.Stem"/>), to tell that Buttons is the title for "button".</summary>
        public required string[] TitleStems { get; init; }

        public required string[] IdStems { get; init; }

        public required string[][] TitleWords { get; init; }

        public required double[] TitleNorm { get; init; }

        /// <summary>The id of each document as it is compared (a rule's <c>form-15</c>, a component's <c>file-upload</c>), folded.</summary>
        public required string[] IdKeys { get; init; }

        /// <summary>Every word of every document, folded, in ordinal order.</summary>
        public required string[] Words { get; init; }

        public required Posting[][] Postings { get; init; }

        public required Dictionary<string, int> WordIndex { get; init; }

        public required Dictionary<string, int> RuleIds { get; init; }

        public ConcurrentDictionary<string, SearchResults> Cache { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>The number of the data this index was built from: it is <see cref="DesignData.Version"/> of the moment, and a query on an older one builds a new snapshot first.</summary>
    public long Version => Current.Version;

    /// <summary>How many documents there are of each kind (for the tests and the page).</summary>
    public IReadOnlyDictionary<SearchKind, int> Counts => Current.Docs.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Every document, in the order they were added.</summary>
    public IReadOnlyList<SearchDoc> Documents => Current.Docs;

    /// <summary>How many different words the snapshot knows.</summary>
    public int VocabularySize => Current.Words.Length;

    private Snapshot Current
    {
        get
        {
            var snapshot = _snapshot;
            var version = _design.Version;
            if (snapshot is not null && snapshot.Version == version)
            {
                return snapshot;
            }

            lock (_gate)
            {
                snapshot = _snapshot;
                if (snapshot is null || snapshot.Version != version)
                {
                    _snapshot = snapshot = Build(version);
                }

                return snapshot;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------ building

    private Snapshot Build(long version)
    {
        var entries = new List<Entry>();
        AddRules(entries);
        AddComponents(entries);
        AddTokens(entries);
        AddIcons(entries);
        AddPages(entries);
        AddApps(entries);
        AddPatterns(entries);

        var docs = new SearchDoc[entries.Count];
        var phrases = new string[entries.Count];
        var titleKeys = new string[entries.Count];
        var titleStems = new string[entries.Count];
        var idStems = new string[entries.Count];
        var titleWords = new string[entries.Count][];
        var titleNorm = new double[entries.Count];
        var idKeys = new string[entries.Count];
        var postings = new Dictionary<string, List<Posting>>(StringComparer.Ordinal);
        var ruleIds = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var d = 0; d < entries.Count; d++)
        {
            var entry = entries[d];
            docs[d] = entry.Doc;
            var scoreTitle = entry.ScoreTitle ?? entry.Doc.Title;
            var title = SearchText.Words(SearchText.Fold(scoreTitle));
            titleWords[d] = [.. title];
            titleKeys[d] = string.Join(' ', title);
            titleStems[d] = string.Join(' ', title.Select(SearchText.Stem));
            titleNorm[d] = 1.0 / (1.0 + 0.1 * Math.Max(0, title.Count - 1));
            var id = SearchText.Words(SearchText.Fold(entry.Doc.Id));
            idKeys[d] = string.Join(' ', id);
            idStems[d] = string.Join(' ', id.Select(SearchText.Stem));
            if (entry.Doc.Kind == SearchKind.Rule)
            {
                ruleIds[SearchText.Fold(entry.Doc.Id)] = d;
            }

            string?[] texts = [scoreTitle, entry.Id, entry.Keywords, entry.Code, entry.Group, entry.Refs, entry.Body];
            var phrase = new StringBuilder();
            for (var f = 0; f < texts.Length; f++)
            {
                if (string.IsNullOrWhiteSpace(texts[f]))
                {
                    continue;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var words = SearchText.Words(SearchText.Fold(texts[f]));
                // the words of the text that people write sentences with (not the codes), one field never runs into the next: " | " stands between them
                if (f is (int)Field.Title or (int)Field.Keywords or (int)Field.Body)
                {
                    phrase.Append(string.Join(' ', words)).Append(" | ");
                }

                for (var position = 0; position < words.Count; position++)
                {
                    if (!seen.Add(words[position]))
                    {
                        continue;
                    }

                    if (!postings.TryGetValue(words[position], out var list))
                    {
                        postings[words[position]] = list = [];
                    }

                    list.Add(new Posting(d, (byte)f, (byte)Math.Min(position, 255)));
                }
            }

            phrases[d] = phrase.ToString();
        }

        var ordered = postings.Keys.Order(StringComparer.Ordinal).ToArray();
        return new Snapshot
        {
            Version = version,
            Docs = docs,
            Phrases = phrases,
            TitleKeys = titleKeys,
            TitleStems = titleStems,
            IdStems = idStems,
            TitleWords = titleWords,
            TitleNorm = titleNorm,
            IdKeys = idKeys,
            Words = ordered,
            Postings = ordered.Select(w => postings[w].ToArray()).ToArray(),
            WordIndex = ordered.Select((w, i) => (w, i)).ToDictionary(x => x.w, x => x.i, StringComparer.Ordinal),
            RuleIds = ruleIds,
        };
    }

    /// <summary>The first sentence of a text without its markup, cut at a word when it is longer than <paramref name="max"/>.</summary>
    internal static string First(string? text, int max)
    {
        var plain = Regex.Replace((text ?? string.Empty).Replace("`", string.Empty, StringComparison.Ordinal), @"\s+", " ").Trim();
        var stop = plain.IndexOf(". ", StringComparison.Ordinal);
        if (stop > 0 && stop < max)
        {
            return plain[..(stop + 1)];
        }

        if (plain.Length <= max)
        {
            return plain;
        }

        var cut = plain.LastIndexOf(' ', max);
        return plain[..(cut > max / 2 ? cut : max)].TrimEnd(',', ';', ':', '.', ' ') + "…";
    }

    private static string Plain(string? text) => (text ?? string.Empty).Replace("`", " ", StringComparison.Ordinal);

    /// <summary>A token without the prefix of the toolkit, which every one of them has: <c>--mat-accent</c> is looked for as "accent" (its whole name is its id).</summary>
    private static string Bare(string name) => name.StartsWith("--mat-", StringComparison.Ordinal) ? name[6..] : name.TrimStart('-');

    /// <summary>The words people copy from a text: what is written between backticks (a class, an attribute, a property).</summary>
    private static string CodeOf(params string?[] texts) => string.Join(' ', texts.Where(t => t is not null).SelectMany(t => CodeSpan().Matches(t!).Select(m => m.Groups[1].Value)));

    private void AddRules(List<Entry> entries)
    {
        foreach (var rule in _design.Rules)
        {
            var area = _design.Areas.FirstOrDefault(a => a.Id == rule.Area);
            var areaName = area?.Name ?? rule.Area;
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Rule, rule.Id, $"{rule.Id} {rule.Title}", $"{areaName} · {rule.Level} · {First(rule.Statement, 100)}", SearchLinks.Rule(rule.Id), SearchKinds.Icon(SearchKind.Rule), areaName),
                ScoreTitle: rule.Title,
                Id: rule.Id,
                Keywords: rule.Level.ToString(),
                Code: CodeOf(rule.Statement, rule.Do, rule.Dont),
                Group: $"{areaName} {rule.Area}",
                Refs: string.Join(' ', RuleReference().Matches(rule.Statement + " " + rule.Why + " " + rule.Do).Select(m => m.Value).Where(v => v != rule.Id).Distinct()),
                Body: Plain(rule.Statement) + " " + Plain(rule.Why) + " " + Plain(rule.Do) + " " + Plain(rule.Dont)));
        }
    }

    private void AddComponents(List<Entry> entries)
    {
        var properties = _toolkit.Properties;
        foreach (var doc in _design.Components)
        {
            var own = ComponentProperties.ForDoc(properties, doc.Id);
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Component, doc.Id, doc.Name, $"{doc.Group} · {First(doc.Summary, 100)}", SearchLinks.Component(doc.Id), doc.Icon, doc.Group),
                Id: doc.Id,
                Code: string.Join(' ', ComponentApi.Classes(doc).Concat(ComponentApi.Attributes(doc)).Concat(own.Select(p => p.Name))),
                Group: doc.Group,
                Refs: string.Join(' ', doc.Rules),
                Body: Plain(doc.Summary) + " " + string.Join(' ', doc.Behaviour.Select(Plain)) + " " + string.Join(' ', doc.Examples.Select(e => e.Title)) + " " + string.Join(' ', own.Select(p => p.Description))));
        }
    }

    private void AddTokens(List<Entry> entries)
    {
        foreach (var token in _toolkit.Tokens)
        {
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Token, token.Name, token.Name, $"{token.Group} · {First(token.Description, 90)}", SearchLinks.Token(token.Name), SearchKinds.Icon(SearchKind.Token), token.Group),
                ScoreTitle: Bare(token.Name),
                Id: token.Name,
                Code: token.Light + " " + token.Dark,
                Group: token.Group,
                Body: token.Description));
        }

        foreach (var property in _toolkit.Properties)
        {
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Token, property.Name, property.Name, $"Component property · {First(property.Description, 90)}", SearchLinks.Token(property.Name), SearchKinds.Icon(SearchKind.Token), "Component properties"),
                ScoreTitle: Bare(property.Name),
                Id: property.Name,
                Keywords: "component property shape",
                Code: property.Fallback,
                Group: "Component properties " + property.Component,
                Body: property.Description));
        }
    }

    private void AddIcons(List<Entry> entries)
    {
        var sprite = _toolkit.IconNames.ToHashSet(StringComparer.Ordinal);
        foreach (var name in _toolkit.IconNames)
        {
            var group = IconsModel.GroupOf(name);
            var library = _icons.Find(name);
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Icon, name, name, $"Icon in the sprite · {group}", SearchLinks.Icon(name), name, group),
                Keywords: library is null ? "icon" : "icon " + string.Join(' ', library.Tags),
                Group: group + " " + (library is null ? string.Empty : string.Join(' ', library.Categories.Select(c => _icons.Category(c)?.Title ?? c)))));
        }

        foreach (var icon in _icons.Icons.Where(i => !sprite.Contains(i.Name)))
        {
            var categories = icon.Categories.Select(c => _icons.Category(c)?.Title ?? c).ToList();
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Icon, icon.Name, icon.Name, $"Icon library · {string.Join(", ", categories.Take(2))}", SearchLinks.IconLibrary(icon.Name), "image", categories.FirstOrDefault() ?? "Icon library"),
                Keywords: "icon library " + string.Join(' ', icon.Tags),
                Group: string.Join(' ', categories)));
        }
    }

    private void AddPages(List<Entry> entries)
    {
        var apps = _design.Tenant.Apps;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in Navigation.For(_design.NavFacts, studio: false))
        {
            foreach (var group in set.Groups)
            {
                foreach (var item in group.Items.SelectMany(i => i.IsParent ? i.Children! : [i]).Where(i => i.Href is not null))
                {
                    if (!seen.Add(item.Href!))
                    {
                        continue;
                    }

                    var (text, keywords) = SearchPages.Describe(item.Href!, apps);
                    var area = group.Title ?? set.Title;
                    entries.Add(new Entry(
                        new SearchDoc(SearchKind.Page, item.Href!, item.Title, string.IsNullOrEmpty(text) ? area : $"{area} · {text}", item.Href!, item.Icon ?? SearchKinds.Icon(SearchKind.Page), area),
                        Keywords: keywords,
                        Group: area + " " + set.Title,
                        Body: text));
                }
            }
        }
    }

    private void AddApps(List<Entry> entries)
    {
        foreach (var app in _design.Apps)
        {
            entries.Add(new Entry(
                new SearchDoc(SearchKind.App, app.Id, app.Name, string.IsNullOrWhiteSpace(app.Tagline) ? app.Category : app.Tagline, SearchLinks.App(app.Id), app.Glyph, app.Category),
                Id: app.Id,
                Keywords: "app accent " + app.Accent,
                Group: app.Category,
                Body: app.Tagline + " " + app.Notes));
        }
    }

    private void AddPatterns(List<Entry> entries)
    {
        foreach (var pattern in _design.Patterns)
        {
            entries.Add(new Entry(
                new SearchDoc(SearchKind.Pattern, pattern.Id, pattern.Name, $"{pattern.Kind} · {First(pattern.Summary, 100)}", SearchLinks.Pattern(pattern.Id), pattern.Icon, pattern.Kind),
                Id: pattern.Id,
                Keywords: "pattern page type",
                Group: pattern.Kind,
                Refs: string.Join(' ', pattern.Rules),
                Body: pattern.Summary + " " + string.Join(' ', pattern.Parts.Select(p => p.Title + " " + p.Text))));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------ asking

    /// <summary>A query as it is looked at: trimmed, the white space made one space, cut at <see cref="MaxQueryLength"/>.</summary>
    public static string Normalise(string? query)
    {
        var text = Regex.Replace(query ?? string.Empty, @"\s+", " ").Trim();
        return text.Length > MaxQueryLength ? text[..MaxQueryLength].TrimEnd() : text;
    }

    /// <summary>
    /// Every document that matches all the words of <paramref name="query"/>, best first. The answer for a text is kept until the data changes. A text without a letter or a digit has no
    /// answer; an empty text neither.
    /// </summary>
    public SearchResults Query(string? query, bool useCache = true)
    {
        var text = Normalise(query);
        var terms = SearchText.Terms(text).Distinct(StringComparer.Ordinal).Take(MaxTerms).ToList();
        if (terms.Count == 0)
        {
            return SearchResults.Empty;
        }

        var snapshot = Current;
        var key = string.Join(' ', terms) + "|" + SearchText.Fold(text);
        if (useCache && snapshot.Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var results = Run(snapshot, text, terms);
        if (useCache)
        {
            if (snapshot.Cache.Count >= MaxCached)
            {
                snapshot.Cache.Clear();
            }

            snapshot.Cache[key] = results;
        }

        return results;
    }

    /// <summary>The same as <see cref="Query"/> for the popup: the best <paramref name="limit"/> hits, in groups.</summary>
    public IReadOnlyList<SearchGroup> Suggest(string? query, int limit = 8, SearchKind? kind = null) => Query(query).Top(Math.Clamp(limit, 1, 50), kind);

    private static SearchResults Run(Snapshot s, string text, List<string> terms)
    {
        var count = s.Docs.Length;
        var perTerm = new double[terms.Count][];
        for (var t = 0; t < terms.Count; t++)
        {
            var words = new Dictionary<int, double>();
            WordsFor(s, terms[t], words);
            var scores = new double[count];
            foreach (var (word, factor) in words)
            {
                foreach (var p in s.Postings[word])
                {
                    var value = Weights[p.Field] * factor;
                    if (p.Field == (byte)Field.Title)
                    {
                        value *= s.TitleNorm[p.Doc] * (p.Position == 0 ? 1.15 : 1.0);
                    }

                    if (value > scores[p.Doc])
                    {
                        scores[p.Doc] = value;
                    }
                }
            }

            perTerm[t] = scores;
        }

        // words that carry nothing ("how do I", "dark mode") need not be found, but count when they are; a query of nothing else has to find them
        var optional = terms.Select(Fillers.Contains).ToArray();
        if (optional.All(o => o))
        {
            optional = new bool[terms.Count];
        }

        var queryKey = string.Join(' ', terms);
        var queryStem = string.Join(' ', terms.Select(SearchText.Stem));
        var ruleId = RuleIdOf(s, SearchText.Fold(text));
        // a word that a thing is named by, besides the one that was typed: the page called Switches is the page for "toggle" (only for a query of one word)
        var named = terms.Count == 1
            ? SearchSynonyms.Match(terms[0]).Where(m => m.Agreement <= SearchSynonyms.Agreement.Start).SelectMany(m => m.Group.Folded).Where(w => w != terms[0]).Select(SearchText.Stem).ToHashSet(StringComparer.Ordinal)
            : [];
        var hits = new List<SearchHit>();
        for (var d = 0; d < count; d++)
        {
            var total = 0.0;
            var all = true;
            for (var t = 0; t < perTerm.Length; t++)
            {
                if (perTerm[t][d] <= 0)
                {
                    if (optional[t])
                    {
                        continue;
                    }

                    all = false;
                    break;
                }

                total += perTerm[t][d];
            }

            if (!all)
            {
                continue;
            }

            var doc = s.Docs[d];
            // what is bought for being named so is worth less for an icon or a token, which are looked up, than for a thing that is read
            total = (total + Bonus(s, d, queryKey, queryStem, terms, named) * BonusWeight.GetValueOrDefault(doc.Kind, 1.0)) * KindWeight.GetValueOrDefault(doc.Kind, 1.0);
            hits.Add(new SearchHit(doc, Math.Round(d == ruleId ? total + 1000 : total, 3)));
        }

        // the rule whose id was typed is the answer even when another word of the query was not found in it
        if (ruleId >= 0 && !hits.Any(h => ReferenceEquals(h.Doc, s.Docs[ruleId])))
        {
            hits.Add(new SearchHit(s.Docs[ruleId], 1000));
        }

        hits.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            var byLength = a.Doc.Title.Length.CompareTo(b.Doc.Title.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a.Doc.Id, b.Doc.Id);
        });
        return new SearchResults(text, terms, hits);
    }

    /// <summary>The index of the rule that a text names exactly (<c>form-15</c>, <c>FORM 15</c>, <c>form15</c>), or -1.</summary>
    private static int RuleIdOf(Snapshot s, string folded)
    {
        var match = RuleIdPattern().Match(folded.Trim());
        if (!match.Success)
        {
            return -1;
        }

        var area = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
        var number = int.Parse(match.Groups[1].Success ? match.Groups[2].Value : match.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        return s.RuleIds.TryGetValue($"{area}-{number:00}", out var doc) ? doc : -1;
    }

    /// <summary>What a document gains for being the thing that the query names: its title is the query, starts with it or holds it (as words), its id is the query, a phrase of its text is the query.</summary>
    private static double Bonus(Snapshot s, int doc, string queryKey, string queryStem, List<string> terms, HashSet<string> named)
    {
        var bonus = 0.0;

        // the title as it is, or without plurals: Buttons is the title that "button" and "buttons" name; a query starts at a word of the title.
        // A title that starts with the words as typed (Switches, checks and segments for "switch") is more than one that starts with the letters of a longer word (Menu switcher is not for "switch").
        var title = s.TitleKeys[doc];
        var stem = s.TitleStems[doc];
        var byTitle = 0.0;
        if (title == queryKey || stem == queryStem)
        {
            byTitle = 200;
        }
        else if (StartsAsWords(stem, queryStem) || StartsAsWords(title, queryKey))
        {
            byTitle = 100;
        }
        else if (title.StartsWith(queryKey, StringComparison.Ordinal) || stem.StartsWith(queryStem, StringComparison.Ordinal))
        {
            byTitle = 80;
        }
        else if ((" " + title).Contains(" " + queryKey, StringComparison.Ordinal) || (" " + stem).Contains(" " + queryStem, StringComparison.Ordinal))
        {
            byTitle = 35;
        }

        bonus += byTitle;

        // the id is the name of the thing in the code: the whole of it, or the start of it while it is typed (not for a rule, whose id is a field of its own)
        if (s.Docs[doc].Kind != SearchKind.Rule)
        {
            if (s.IdKeys[doc] == queryKey || s.IdStems[doc] == queryStem)
            {
                bonus += 120;
            }
            else if (s.IdKeys[doc].StartsWith(queryKey, StringComparison.Ordinal))
            {
                bonus += 50;
            }
        }

        if (terms.Count > 1)
        {
            if (terms.All(t => s.TitleWords[doc].Any(w => w.StartsWith(t, StringComparison.Ordinal))))
            {
                bonus += 20;
            }

            // the words as they were typed, one after the other somewhere in the text (not for a query that has little words in it: they are not what a text is found by)
            if (!terms.Any(Fillers.Contains) && s.Phrases[doc].Contains(queryKey, StringComparison.Ordinal))
            {
                bonus += 8;
            }
        }
        else if (named.Count > 0 && (s.Docs[doc].Kind is SearchKind.Component or SearchKind.Page or SearchKind.Pattern) && s.TitleWords[doc].Any(w => named.Contains(SearchText.Stem(w))))
        {
            // less than a title that holds the word itself (35): the page called Spinners is not the page called Number stepper for "stepper"
            bonus += 25;
        }

        return bonus;
    }

    /// <summary>Whether <paramref name="text"/> starts with the words of <paramref name="words"/>, the last of them whole.</summary>
    private static bool StartsAsWords(string text, string words)
        => text.StartsWith(words, StringComparison.Ordinal) && (text.Length == words.Length || text[words.Length] == ' ');

    // what the bonuses of a kind are worth: an icon called toggle is found by its name, but it is not the control that people mean by it
    private static readonly Dictionary<SearchKind, double> BonusWeight = new()
    {
        [SearchKind.Icon] = 0.1,
        [SearchKind.Token] = 0.35,
    };

    /// <summary>
    /// The words of the vocabulary that a word of the query stands for, each with how well it does: the same word 1, a word it starts a little less (the closer in length, the better), a word that
    /// holds it less, a word a letter or two off less again (only for a word that nothing starts: a typo), and the words of the synonyms.
    /// </summary>
    private static void WordsFor(Snapshot s, string term, Dictionary<int, double> into)
    {
        void Put(int word, double factor)
        {
            if (!into.TryGetValue(word, out var old) || factor > old)
            {
                into[word] = factor;
            }
        }

        var exact = s.WordIndex.TryGetValue(term, out var exactWord);
        if (exact)
        {
            Put(exactWord, 1.0);
        }

        // the singular and the plural of the word are almost the word itself
        var known = exact;
        foreach (var variant in new[] { term + "s", term + "es", SearchText.Stem(term) })
        {
            if (variant != term && s.WordIndex.TryGetValue(variant, out var near))
            {
                Put(near, 0.95);
                known = true;
            }
        }

        // the 5 of "FORM 5" is the 05 of FORM-05
        if (term.Length == 1 && char.IsDigit(term[0]) && s.WordIndex.TryGetValue("0" + term, out var padded))
        {
            Put(padded, 0.9);
            known = true;
        }

        var starts = 0;
        for (var i = LowerBound(s.Words, term); i < s.Words.Length && s.Words[i].StartsWith(term, StringComparison.Ordinal); i++)
        {
            if (exact && i == exactWord)
            {
                continue;
            }

            starts++;
            Put(i, 0.8 - Math.Min(0.3, 0.02 * (s.Words[i].Length - term.Length)));
        }

        if (term.Length >= 3)
        {
            for (var i = 0; i < s.Words.Length; i++)
            {
                var word = s.Words[i];
                if (word.Length > term.Length && !word.StartsWith(term, StringComparison.Ordinal) && word.Contains(term, StringComparison.Ordinal))
                {
                    Put(i, 0.35);
                }
            }
        }

        // a typo: only when nothing is the word or starts with it, and only for words long enough that one letter off is still that word
        if (!known && starts == 0 && term.Length >= 5 && term.Length <= SearchText.FuzzyLimit)
        {
            var max = term.Length >= 9 ? 2 : 1;
            for (var i = 0; i < s.Words.Length; i++)
            {
                var word = s.Words[i];
                // the word, or the word without its plural: "buton" is a typo of "buttons"
                var distance = Math.Abs(word.Length - term.Length) <= max ? SearchText.Distance(term, word, max) : max + 1;
                if (distance > max && word.Length > 4 && word.EndsWith('s'))
                {
                    distance = SearchText.Distance(term, SearchText.Stem(word), max);
                }

                if (distance <= max)
                {
                    Put(i, distance == 1 ? 0.5 : 0.42);
                    continue;
                }

                // the start of a longer word with a typo in it: "dropdwo" for "dropdown"
                if (word.Length > term.Length && SearchText.Distance(term, word[..term.Length], max) <= max)
                {
                    Put(i, 0.38);
                }
            }
        }

        foreach (var (group, agreement) in SearchSynonyms.Match(term))
        {
            var strength = agreement switch
            {
                SearchSynonyms.Agreement.Exact => 0.7,
                SearchSynonyms.Agreement.Start => 0.6,
                SearchSynonyms.Agreement.Compound => 0.5,
                _ => 0.4,
            };
            foreach (var other in group.Folded.Where(o => o != term))
            {
                // the words of a group are curated: the singular and the plural of one count the same
                foreach (var variant in new[] { other, other + "s", other + "es" })
                {
                    if (s.WordIndex.TryGetValue(variant, out var word))
                    {
                        Put(word, strength);
                    }
                }
            }
        }
    }

    private static int LowerBound(string[] words, string prefix)
    {
        int low = 0, high = words.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (string.CompareOrdinal(words[middle], prefix) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
