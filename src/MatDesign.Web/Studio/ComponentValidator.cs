using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Web.Studio;

/// <summary>
/// What a component of the documentation has to be, checked before the Studio saves it: an id and a name nobody else has, a group, a summary, rules that exist, and examples
/// whose markup holds to the contract of <see cref="MarkupChecks"/> (the same code that holds the built-in examples in the unit tests). The examples of all components share
/// one page, so an id inside an example must not be one of another component or of the page.
/// </summary>
public static partial class ComponentValidator
{
    private static readonly MarkupFacts NoFacts = new(new HashSet<string>(), new HashSet<string>());

    /// <summary>Ids the page makes itself: the sections, headings and sources of the components, the menu, the index.</summary>
    private static readonly string[] ReservedPrefixes = ["group-", "h-", "src-", "ex-", "toc-", "nav-", "mat-", "user-"];

    /// <summary>"main" is the element of the content; "all" is the page /Components/All, which an address of a component (/Components/{id}) must not shadow.</summary>
    private static readonly string[] ReservedIds = ["main", "all"];

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,39}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^[a-z0-9-]{1,40}$")]
    private static partial Regex IconPattern();

    /// <summary>The name of a field of an example, as the form and the problems spell it: <c>Examples[2].Html</c>.</summary>
    public static string ExampleField(int index, string field) => $"Examples[{index}].{field}";

    /// <summary>The component as it is stored: trimmed, rules in capitals and each once, empty lines and empty examples dropped.</summary>
    public static ComponentDoc Normalize(ComponentDoc doc)
    {
        static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        var examples = (doc.Examples ?? [])
            .Select(e => e with { Title = (e.Title ?? string.Empty).Trim(), Html = (e.Html ?? string.Empty).Trim(), Note = Optional(e.Note), Js = Optional(e.Js), Demo = Optional(e.Demo), Frame = Optional(e.Frame) })
            .Where(e => e.Title.Length > 0 || e.Html.Length > 0 || e.Js is not null)
            .ToList();
        return doc with
        {
            Id = (doc.Id ?? string.Empty).Trim().ToLowerInvariant(),
            Name = (doc.Name ?? string.Empty).Trim(),
            Group = (doc.Group ?? string.Empty).Trim(),
            Icon = string.IsNullOrWhiteSpace(doc.Icon) ? "toggle" : doc.Icon.Trim(),
            Summary = (doc.Summary ?? string.Empty).Trim(),
            Rules = (doc.Rules ?? []).Select(r => r.Trim().ToUpperInvariant()).Where(r => r.Length > 0).Distinct().ToList(),
            Behaviour = (doc.Behaviour ?? []).Select(b => b.Trim()).Where(b => b.Length > 0).ToList(),
            Examples = examples,
        };
    }

    /// <summary>
    /// Everything that is wrong with <paramref name="doc"/>, which must be normalised. <paramref name="others"/> are the other components of the documentation: all of them for a
    /// new one, all but the one being edited otherwise.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(ComponentDoc doc, IEnumerable<ComponentDoc> others, Func<string, bool> ruleExists, MarkupFacts facts)
    {
        var rest = others.ToList();
        var problems = new List<Problem>();

        if (!IdPattern().IsMatch(doc.Id))
        {
            problems.Add(new("Id", "Use 2 to 40 lower-case letters, digits and dashes, starting with a letter, like date-picker."));
        }
        else if (ReservedIds.Contains(doc.Id) || ReservedPrefixes.Any(p => doc.Id.StartsWith(p, StringComparison.Ordinal)))
        {
            problems.Add(new("Id", "The page uses this id itself: start the id with something else."));
        }
        else if (rest.Any(o => o.Id == doc.Id))
        {
            problems.Add(new("Id", "A component with this id exists."));
        }

        if (doc.Name.Length is < 2 or > 60)
        {
            problems.Add(new("Name", "Name the component in 2 to 60 characters."));
        }
        else if (rest.Any(o => o.Name.Equals(doc.Name, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new("Name", "Another component has this name."));
        }

        if (!ComponentCatalog.Groups.Contains(doc.Group))
        {
            problems.Add(new("Group", "Choose one of the groups."));
        }

        if (!IconPattern().IsMatch(doc.Icon) || !facts.Icons.Contains(doc.Icon))
        {
            problems.Add(new("Icon", "The icon is not in the sprite."));
        }

        if (doc.Summary.Length is < 10 or > 300)
        {
            problems.Add(new("Summary", "Say in 10 to 300 characters what the component is for."));
        }

        if (doc.Rules.Count == 0)
        {
            problems.Add(new("Rules", "Name at least one rule that the component follows, like BUTTON-01."));
        }
        else if (doc.Rules.Where(r => !ruleExists(r)).ToList() is { Count: > 0 } unknown)
        {
            problems.Add(new("Rules", $"The rulebook has no rule {string.Join(", ", unknown)}."));
        }

        if (doc.Behaviour.Count > 12 || doc.Behaviour.Any(b => b.Length > 300))
        {
            problems.Add(new("Behaviour", "Up to 12 lines of up to 300 characters."));
        }

        if (doc.Examples.Count is 0 or > 12)
        {
            problems.Add(new("Examples", doc.Examples.Count == 0 ? "A component needs at least one example." : "Up to 12 examples."));
        }

        for (var i = 0; i < doc.Examples.Count; i++)
        {
            var example = doc.Examples[i];
            if (example.Title.Length is < 2 or > 80)
            {
                problems.Add(new(ExampleField(i, "Title"), $"Example {i + 1}: give it a title of 2 to 80 characters."));
            }

            if (example.Html.Length == 0 && example.Js is null)
            {
                problems.Add(new(ExampleField(i, "Html"), $"Example {i + 1}: write the markup."));
            }
            else if (example.Html.Length > 20000)
            {
                problems.Add(new(ExampleField(i, "Html"), $"Example {i + 1}: the markup is longer than 20000 characters."));
            }

            if (example.Note is { Length: > 400 })
            {
                problems.Add(new(ExampleField(i, "Note"), $"Example {i + 1}: keep the note to 400 characters."));
            }
        }

        foreach (var problem in MarkupChecks.Check(doc.Examples.Select(e => e.Html).ToList(), facts))
        {
            var field = problem.Example >= 0 ? ExampleField(problem.Example, "Html") : "Examples";
            var where = problem.Example >= 0 ? $"Example {problem.Example + 1}: " : string.Empty;
            problems.Add(new(field, where + problem.Message));
        }

        problems.AddRange(IdsOfOthers(doc, rest));
        return problems;
    }

    /// <summary>
    /// An id inside an example that the page or another component already has, or that the page may use itself. Only an example that is drawn live puts its ids on the page:
    /// one that is shown as markup only (a dialog that is described, not opened) can use any id.
    /// </summary>
    private static IEnumerable<Problem> IdsOfOthers(ComponentDoc doc, IReadOnlyList<ComponentDoc> rest)
    {
        var taken = new Dictionary<string, string>();
        foreach (var other in rest)
        {
            taken.TryAdd(other.Id, other.Name);
            foreach (var id in other.Examples.Where(e => e.Live && e.Html.Length > 0).SelectMany(e => MarkupChecks.IdsIn(e.Html)))
            {
                taken.TryAdd(id, other.Name);
            }
        }

        for (var i = 0; i < doc.Examples.Count; i++)
        {
            if (!doc.Examples[i].Live)
            {
                continue;
            }

            foreach (var id in MarkupChecks.IdsIn(doc.Examples[i].Html).Distinct())
            {
                if (ReservedIds.Contains(id) || ReservedPrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal)))
                {
                    yield return new(ExampleField(i, "Html"), $"Example {i + 1}: the id '{id}' is one the page uses itself.");
                }
                else if (id == doc.Id)
                {
                    yield return new(ExampleField(i, "Html"), $"Example {i + 1}: the id '{id}' is the id of the section of this component.");
                }
                else if (taken.TryGetValue(id, out var owner))
                {
                    yield return new(ExampleField(i, "Html"), $"Example {i + 1}: the id '{id}' is already used by {owner}; all examples share one page.");
                }
            }
        }
    }

    /// <summary>
    /// True when the component can be shown without harm: an id and a group of the page, a name, an example, and nothing in the markup that must never be rendered (an inline
    /// style, a handler, a script, a frame, an address of another site). Checked again for what is read from the disk, which a person can edit by hand.
    /// </summary>
    public static bool IsSafe(ComponentDoc doc)
        => IdPattern().IsMatch(doc.Id) && !ReservedIds.Contains(doc.Id) && !ReservedPrefixes.Any(p => doc.Id.StartsWith(p, StringComparison.Ordinal))
            && ComponentCatalog.Groups.Contains(doc.Group) && IconPattern().IsMatch(doc.Icon) && !string.IsNullOrWhiteSpace(doc.Name) && doc.Examples.Count > 0
            && !MarkupChecks.Check(doc.Examples.Select(e => e.Html).ToList(), NoFacts).Any(p => MarkupChecks.Dangerous.Contains(p.Check));
}
