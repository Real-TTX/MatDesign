using MatDesign.Core;

namespace MatDesign.Web.Services;

/// <summary>
/// The word map (<see cref="TenantWords"/>) for the catalogues of this project: the documentation of a component and a pattern. The text of an element and the attributes that are read are mapped, never an id, a class, an address
/// or a script; a component that has none of the words of the family is returned as it is (the same object).
/// </summary>
public static class CatalogWords
{
    /// <summary>A component as a company reads it: the name, the summary, the notes, the titles and notes of the examples and the text of their markup.</summary>
    public static ComponentDoc OfComponent(this TenantWords words, ComponentDoc doc)
    {
        if (words.IsIdentity)
        {
            return doc;
        }

        var examples = doc.Examples.Select(e => e with { Title = words.Text(e.Title), Note = words.Text(e.Note), Html = words.Markup(e.Html) }).ToList();
        return doc with { Name = words.Text(doc.Name), Summary = words.Text(doc.Summary), Behaviour = words.Texts(doc.Behaviour), Examples = examples };
    }

    /// <summary>A pattern as a company reads it.</summary>
    public static PatternDoc OfPattern(this TenantWords words, PatternDoc pattern)
        => words.IsIdentity
            ? pattern
            : pattern with
            {
                Name = words.Text(pattern.Name),
                Summary = words.Text(pattern.Summary),
                Parts = pattern.Parts.Select(p => p with { Title = words.Text(p.Title), Text = words.Text(p.Text) }).ToList(),
            };
}
