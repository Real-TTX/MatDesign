using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace MatDesign.Web.Services;

/// <summary>
/// Wraps the part of a text that a word of the query matches in <c>&lt;mark class="mat-search__hit"&gt;</c>, on the server (the page of the results): the text is encoded piece by piece, so what a title says is never
/// taken for markup, and a word is found as it was typed, without case and accents, like the script of the toolkit does for the popup.
/// </summary>
public static class SearchHighlight
{
    /// <summary>The places of <paramref name="text"/> that a word of <paramref name="terms"/> (folded) matches, as [from, to) in the text, merged where they touch.</summary>
    public static IReadOnlyList<(int From, int To)> Ranges(string text, IEnumerable<string> terms)
    {
        var map = new List<int>();
        var folded = SearchText.Fold(text, map);
        var marks = new List<(int From, int To)>();
        foreach (var term in terms.Where(t => t.Length > 0))
        {
            for (var at = folded.IndexOf(term, StringComparison.Ordinal); at >= 0; at = folded.IndexOf(term, at + term.Length, StringComparison.Ordinal))
            {
                var last = map[at + term.Length - 1];
                var end = last + (last + 1 < text.Length && char.IsLowSurrogate(text[last + 1]) ? 2 : 1);
                marks.Add((map[at], end));
            }
        }

        var merged = new List<(int From, int To)>();
        foreach (var mark in marks.OrderBy(m => m.From))
        {
            if (merged.Count > 0 && mark.From <= merged[^1].To)
            {
                merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, mark.To));
            }
            else
            {
                merged.Add(mark);
            }
        }

        return merged;
    }

    /// <summary>The text as HTML: every piece encoded, the matching pieces in a <c>mark</c>.</summary>
    public static IHtmlContent Mark(string text, IEnumerable<string> terms)
    {
        var encoder = HtmlEncoder.Default;
        var sb = new StringBuilder();
        var at = 0;
        foreach (var (from, to) in Ranges(text, terms))
        {
            sb.Append(encoder.Encode(text[at..from]));
            sb.Append("<mark class=\"mat-search__hit\">").Append(encoder.Encode(text[from..to])).Append("</mark>");
            at = to;
        }

        sb.Append(encoder.Encode(text[at..]));
        return new HtmlString(sb.ToString());
    }
}
