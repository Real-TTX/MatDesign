using System.Text;

namespace MatDesign.Web.Services;

/// <summary>
/// What the search does to text before it compares it (docs/ARCHITECTURE.md, "Search"): it folds case, accents and umlauts away (ä is a, ß is ss), cuts the text into words and measures how far two
/// words are from each other. The same folding is in the script of the toolkit (<c>srFold</c> in matui.js), which wraps the part of a title that matches, so a title is highlighted
/// where the server found it.
/// </summary>
public static class SearchText
{
    /// <summary>The longest word that is compared by its distance: a longer one is a hash or a path, and a typo in it means nothing.</summary>
    public const int FuzzyLimit = 40;

    [ThreadStatic]
    private static int[]? _rows;

    // The letters with an accent, by the letter they are folded to. A table and not string.Normalize: the site runs with the invariant globalization (no ICU in the image), where a
    // normalization form does nothing for such letters, so a search that relied on it would find "häkchen" on a developer PC and not in the container.
    private static readonly Dictionary<char, string> Folds = BuildFolds();

    private static Dictionary<char, string> BuildFolds()
    {
        var map = new Dictionary<char, string>();
        void Add(string letters, string to)
        {
            foreach (var letter in letters)
            {
                map[letter] = to;
            }
        }

        Add("àáâãäåāăąǎ", "a");
        Add("çćĉċč", "c");
        Add("ďđð", "d");
        Add("èéêëēĕėęě", "e");
        Add("ĝğġģ", "g");
        Add("ĥħ", "h");
        Add("ìíîïĩīĭįıǐ", "i");
        Add("ĵ", "j");
        Add("ķ", "k");
        Add("ĺļľŀł", "l");
        Add("ñńņňŉ", "n");
        Add("òóôõöøōŏőǒ", "o");
        Add("ŕŗř", "r");
        Add("śŝşš", "s");
        Add("ţťŧ", "t");
        Add("ùúûüũūŭůűųǔ", "u");
        Add("ŵ", "w");
        Add("ýÿŷ", "y");
        Add("źżž", "z");
        Add("æ", "ae");
        Add("œ", "oe");
        Add("ß", "ss");
        Add("þ", "th");
        return map;
    }

    /// <summary>Lower case without accents, the ß as ss: "Häkchen" and "hakchen" are the same word.</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            // an accent of its own (the text came decomposed: a and a combining diaeresis) is dropped
            if (c is >= '̀' and <= 'ͯ')
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (Folds.TryGetValue(lower, out var folded))
            {
                sb.Append(folded);
            }
            else
            {
                sb.Append(lower);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// The same folding, character by character, with a map: <paramref name="map"/> gets, for every character of the folded text, the index in <paramref name="text"/> that it came from, so that
    /// a place in the folded text can be shown in the original (the highlight of a title).
    /// </summary>
    public static string Fold(string text, List<int> map)
    {
        var sb = new StringBuilder(text.Length);
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var folded = Fold(rune.ToString());
            sb.Append(folded);
            for (var i = 0; i < folded.Length; i++)
            {
                map.Add(index);
            }

            index += rune.Utf16SequenceLength;
        }

        return sb.ToString();
    }

    /// <summary>
    /// A word without its plural: "buttons" is "button", "boxes" is "box", "status" and "class" stay what they are. Only for the comparison of a title with a query, so that the page called Buttons is
    /// the page for "button" as well as for "buttons"; nothing is shown in this form.
    /// </summary>
    public static string Stem(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
        {
            return word[..^3] + "y";
        }

        if (word.Length > 4 && (word.EndsWith("ches", StringComparison.Ordinal) || word.EndsWith("shes", StringComparison.Ordinal) || word.EndsWith("xes", StringComparison.Ordinal) || word.EndsWith("sses", StringComparison.Ordinal)))
        {
            return word[..^2];
        }

        return word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) && !word.EndsWith("us", StringComparison.Ordinal) && !word.EndsWith("is", StringComparison.Ordinal) ? word[..^1] : word;
    }

    /// <summary>True for a character that belongs to a word: a letter or a digit (after folding).</summary>
    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

    /// <summary>The words of an already folded text, in order, with their repeats.</summary>
    public static IReadOnlyList<string> Words(string folded)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= folded.Length; i++)
        {
            var inside = i < folded.Length && IsWordChar(folded[i]);
            if (inside && start < 0)
            {
                start = i;
            }
            else if (!inside && start >= 0)
            {
                words.Add(folded[start..i]);
                start = -1;
            }
        }

        return words;
    }

    /// <summary>The words of a text as typed: folded and cut. Empty for a text without a letter or a digit.</summary>
    public static IReadOnlyList<string> Terms(string? query) => Words(Fold(query));

    /// <summary>
    /// The distance of two words by the optimal string alignment: an insertion, a deletion, a replaced letter and two neighbouring letters that were swapped count one each ("dropdwon" is one
    /// away from "dropdown"). It stops as soon as the distance is more than <paramref name="max"/> and then says <c>max + 1</c>.
    /// </summary>
    public static int Distance(string a, string b, int max)
    {
        var n = a.Length;
        var m = b.Length;
        if (Math.Abs(n - m) > max)
        {
            return max + 1;
        }

        if (n > FuzzyLimit || m > FuzzyLimit)
        {
            return a == b ? 0 : max + 1;
        }

        var width = m + 1;
        var rows = _rows ??= new int[3 * (FuzzyLimit + 1)];
        var two = 0;
        var one = width;
        var now = 2 * width;
        for (var j = 0; j <= m; j++)
        {
            rows[one + j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            rows[now] = i;
            var smallest = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(rows[one + j] + 1, rows[now + j - 1] + 1), rows[one + j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, rows[two + j - 2] + 1);
                }

                rows[now + j] = value;
                smallest = Math.Min(smallest, value);
            }

            if (smallest > max)
            {
                return max + 1;
            }

            (two, one, now) = (one, now, two);
        }

        var distance = rows[one + m];
        return distance > max ? max + 1 : distance;
    }
}
