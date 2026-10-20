using System.Globalization;
using MatDesign.Core;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Services;

/// <summary>
/// <c>GET /api/rules/changes</c>: the changelog of the rulebook as data, the newest line first, for a tool or an agent that wants to know what changed in the rules since it last looked.
/// <c>?rule=FORM-15</c>, <c>?kind=Changed</c>, <c>?area=FORM</c> and <c>?since=2026-10-15</c> (that day and after) narrow it down; a value that is not understood is a 400, never an empty list.
/// </summary>
public static class RuleChangesApi
{
    /// <summary>The answer for the query: a list of <c>{ date, rule, area, title, kind, note, hash }</c> (title is null for a rule that is gone).</summary>
    public static IResult Query(DesignData design, string? rule, string? kind, string? area, string? since)
    {
        RuleChangeKind? wanted = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<RuleChangeKind>(kind.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Results.BadRequest(new { error = "kind is Added, Changed, Removed or Moved." });
            }

            wanted = parsed;
        }

        DateOnly? from = null;
        if (!string.IsNullOrWhiteSpace(since))
        {
            if (!DateOnly.TryParseExact(since.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return Results.BadRequest(new { error = "since is a day like 2026-10-15." });
            }

            from = day;
        }

        var areaId = string.IsNullOrWhiteSpace(area) ? null : area.Trim().ToUpperInvariant();
        if (areaId is not null && RuleBook.Areas.All(a => a.Id != areaId))
        {
            return Results.BadRequest(new { error = "area is one of " + string.Join(", ", RuleBook.Areas.Select(a => a.Id)) + "." });
        }

        var ruleId = string.IsNullOrWhiteSpace(rule) ? null : rule.Trim();
        var places = design.Rules.Select((r, i) => (r.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        // newest first; on one day the rules stand in the order of the book, and the lines of one rule the newest first (as the page of the rule shows them)
        var lines = design.RuleChanges
            .Select((change, index) => (Change: change, Index: index))
            .Where(x => ruleId is null || x.Change.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase))
            .Where(x => wanted is null || x.Change.Kind == wanted)
            .Where(x => areaId is null || x.Change.RuleId.StartsWith(areaId + "-", StringComparison.Ordinal))
            .Where(x => from is null || x.Change.Date >= from)
            .OrderByDescending(x => x.Change.Date)
            .ThenBy(x => places.GetValueOrDefault(x.Change.RuleId, int.MaxValue))
            .ThenByDescending(x => x.Index)
            .Select(x => new
            {
                date = x.Change.DateText,
                rule = x.Change.RuleId,
                area = x.Change.RuleId[..x.Change.RuleId.LastIndexOf('-')],
                title = design.Rule(x.Change.RuleId)?.Title,
                kind = x.Change.Kind.ToString(),
                note = x.Change.Note,
                hash = x.Change.Hash,
            });
        return Results.Json(lines.ToList());
    }
}
