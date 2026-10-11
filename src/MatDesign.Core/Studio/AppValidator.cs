using System.Globalization;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>Something that is wrong with a field of an item, in words that say how to fix it.</summary>
public sealed record Problem(string Field, string Message);

/// <summary>
/// What an app of the family has to be, checked before the Studio saves it: the same facts the unit tests hold for the apps that are built in
/// (a name and an id nobody else has, a glyph of the sprite, a port that is free, an accent that reaches AA in light and dark with its text).
/// </summary>
public static partial class AppValidator
{
    [GeneratedRegex(@"^[a-z]{2,24}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^#[0-9a-f]{6}$")]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,60}$")]
    private static partial Regex RepoPattern();

    [GeneratedRegex(@"^[a-z0-9-]{1,40}$")]
    private static partial Regex GlyphPattern();

    /// <summary>
    /// True when the app can be written into a stylesheet and a page without harm: an id of letters, colours that are colours, a glyph that is a name. The Studio
    /// checks this again when it reads an item from the disk, because the volume is a place a person can edit by hand.
    /// </summary>
    public static bool IsSafe(MatApp app)
        => IdPattern().IsMatch(app.Id) && HexPattern().IsMatch(app.Accent) && GlyphPattern().IsMatch(app.Glyph)
            && (app.OnAccent is null || HexPattern().IsMatch(app.OnAccent)) && (app.AccentText is null || HexPattern().IsMatch(app.AccentText))
            && !string.IsNullOrWhiteSpace(app.Name);

    /// <summary>The app as it is stored: trimmed, colours in lower case, an empty optional value as null, the repository named like the app when it was left empty.</summary>
    public static MatApp Normalize(MatApp app)
    {
        static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        static string? Colour(string? text) => Optional(text)?.ToLowerInvariant();

        var name = (app.Name ?? string.Empty).Trim();
        return app with
        {
            Id = (app.Id ?? string.Empty).Trim().ToLowerInvariant(),
            Name = name,
            Tagline = (app.Tagline ?? string.Empty).Trim(),
            Category = (app.Category ?? string.Empty).Trim(),
            Accent = (app.Accent ?? string.Empty).Trim().ToLowerInvariant(),
            Glyph = (app.Glyph ?? string.Empty).Trim(),
            Repo = Optional(app.Repo) ?? name,
            FoundAccent = Colour(app.FoundAccent),
            LogoColor = Colour(app.LogoColor),
            OnAccent = Colour(app.OnAccent),
            AccentText = Colour(app.AccentText),
            CssPrefix = (app.CssPrefix ?? string.Empty).Trim(),
            TokenScheme = (app.TokenScheme ?? string.Empty).Trim(),
            Notes = (app.Notes ?? string.Empty).Trim(),
        };
    }

    /// <summary>
    /// Everything that is wrong with <paramref name="app"/>, which must be normalised. <paramref name="others"/> are the other apps of the family: all of them
    /// for a new app, all but the one being edited otherwise.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(MatApp app, IEnumerable<MatApp> others, IReadOnlyCollection<string> glyphs)
    {
        var rest = others.ToList();
        var problems = new List<Problem>();

        if (!IdPattern().IsMatch(app.Id))
        {
            problems.Add(new("Id", "Use 2 to 24 lower-case letters, like the other apps (matmail)."));
        }
        else if (rest.Any(o => o.Id.Equals(app.Id, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new("Id", "An app with this id exists."));
        }

        if (app.Name.Length is < 2 or > 40)
        {
            problems.Add(new("Name", "Name the app in 2 to 40 characters, written as it is in its menu (IDENT-06)."));
        }
        else if (rest.Any(o => o.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new("Name", "Another app has this name."));
        }

        if (app.Tagline.Length is < 3 or > 90)
        {
            problems.Add(new("Tagline", "Say in 3 to 90 characters what the app is for."));
        }

        if (!AppRegistry.Categories.Contains(app.Category))
        {
            problems.Add(new("Category", "Choose one of the categories."));
        }

        if (!glyphs.Contains(app.Glyph))
        {
            problems.Add(new("Glyph", "Choose an icon of the sprite."));
        }

        if (!RepoPattern().IsMatch(app.Repo))
        {
            problems.Add(new("Repo", "The name of the repository: letters, digits, dot, dash and underscore."));
        }

        if (app.Port is { } port)
        {
            if (port is < 1 or > 65535)
            {
                problems.Add(new("Port", "A port is a number from 1 to 65535."));
            }
            else if (rest.FirstOrDefault(o => o.Port == port) is { } taken)
            {
                problems.Add(new("Port", $"{taken.Name} already uses port {port}."));
            }
        }

        if (app.Notes.Length > 300)
        {
            problems.Add(new("Notes", "Keep the note to 300 characters."));
        }

        var colours = true;
        if (!HexPattern().IsMatch(app.Accent))
        {
            problems.Add(new("Accent", "A colour like #2f6feb."));
            colours = false;
        }

        if (app.OnAccent is { } onAccent && !HexPattern().IsMatch(onAccent))
        {
            problems.Add(new("OnAccent", "A colour like #0b1020, or leave it empty for white."));
            colours = false;
        }

        if (app.AccentText is { } text && !HexPattern().IsMatch(text))
        {
            problems.Add(new("AccentText", "A colour like #1d7f77, or leave it empty."));
            colours = false;
        }

        if (colours)
        {
            problems.AddRange(Contrast(app));
        }

        return problems;
    }

    /// <summary>The contrast checks that fail (IDENT-02, COLOR-05), each with the field to change and what to do. Advice-only checks never fail.</summary>
    public static IReadOnlyList<Problem> Contrast(MatApp app)
    {
        var report = AccentReport.For(app);
        var problems = new List<Problem>();
        foreach (var check in report.Checks().Where(c => !c.Advisory && !c.Passes))
        {
            var ratio = check.Ratio.ToString("0.0", CultureInfo.InvariantCulture);
            var needed = check.Required.ToString("0.0", CultureInfo.InvariantCulture);
            problems.Add(check.Name switch
            {
                "Text on the accent (light)" => new Problem("OnAccent", $"{(app.OnAccent is null ? "White text" : "That text colour")} reaches only {ratio}:1 on this accent and needs {needed}:1. Darken the accent, or give it a dark text colour."),
                "Link on a surface (light)" => new Problem("AccentText", $"A link in this accent reaches only {ratio}:1 on white and needs {needed}:1. Give the app a darker link colour, or darken the accent."),
                "Text on the accent (dark)" => new Problem("Accent", $"In dark mode the text on this accent reaches only {ratio}:1 and needs {needed}:1. The accent is too dark."),
                _ => new Problem("Accent", $"{check.Name}: {ratio}:1, needs {needed}:1. Choose a lighter or a stronger accent."),
            });
        }

        return problems;
    }
}
