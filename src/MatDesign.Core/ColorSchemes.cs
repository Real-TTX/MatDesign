namespace MatDesign.Core;

/// <summary>
/// A colour scheme of the toolkit (<c>COLOR-09</c>): another set of surfaces, lines and text colours for the same controls, chosen with <c>data-scheme</c> on <c>&lt;html&gt;</c>.
/// <see cref="DarkOnly"/> is a scheme that has no light values: in light mode it is the standard look.
/// </summary>
public sealed record ColorScheme(string Id, string Name, string Description, bool DarkOnly = false);

/// <summary>
/// The colour schemes. Their colours are in <c>matui.tokens.css</c> (the CSS is the source of truth, <see cref="SchemeSurfaces"/> reads them); this list is what a person
/// is offered, with the words for it, and a test checks that it and the CSS name the same schemes.
/// </summary>
public static class ColorSchemes
{
    /// <summary>The id of the standard look. The attribute never has to say it: absent, this or any other value is the standard look.</summary>
    public const string Standard = "standard";

    public static IReadOnlyList<ColorScheme> All { get; } =
    [
        new("paper", "Paper", "Warm, slightly yellowed surfaces and brown-black text, in light and in dark."),
        new("contrast", "High contrast", "Pure white or pure black surfaces, strong lines instead of shadows, near-black or near-white muted text, borders of controls at 3:1 or more."),
        new("oled", "Black (OLED)", "A black page and menu with near-black surfaces. Dark mode only: in light mode it is the standard look.", DarkOnly: true),
    ];

    public static bool IsKnown(string? id) => All.Any(s => s.Id == id);

    public static ColorScheme? Get(string? id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>The scheme of a query string or a setting: a known id as the CSS writes it, anything else (also "standard") is null, the standard look.</summary>
    public static string? Normalize(string? id) => IsKnown(id) ? id : null;
}

/// <summary>
/// The colours of one colour scheme in one mode, as <c>matui.tokens.css</c> gives them: the base tokens (light, or dark) with what the scheme sets over them. The contrast
/// checks and the pages that show a scheme read this, so the numbers can never differ from the CSS. For the standard scheme nothing is set.
/// </summary>
public sealed class SchemeSurfaces
{
    private readonly IReadOnlyDictionary<string, string> _tokens;

    private SchemeSurfaces(string scheme, bool dark, bool standardLook, IReadOnlyDictionary<string, string> tokens, IReadOnlySet<string> set)
    {
        Scheme = scheme;
        Dark = dark;
        IsStandardLook = standardLook;
        _tokens = tokens;
        Set = set;
        AccentKeep = _tokens.TryGetValue("--mat-accent-text-keep", out var keep) ? double.Parse(keep.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100 : 1;
    }

    /// <summary>The id of the scheme, or <see cref="ColorSchemes.Standard"/>.</summary>
    public string Scheme { get; }

    public bool Dark { get; }

    public string Mode => Dark ? "dark" : "light";

    /// <summary>True for the standard scheme and for a dark-only scheme in light mode: the colours are the standard ones.</summary>
    public bool IsStandardLook { get; }

    /// <summary>The names of the tokens that the scheme sets in this mode.</summary>
    public IReadOnlySet<string> Set { get; }

    /// <summary>The share of the accent that the colour of a link keeps (<c>--mat-accent-text-keep</c>): 1 in the standard look, less where the surfaces need darker links.</summary>
    public double AccentKeep { get; }

    public Rgb Bg => Opaque("--mat-bg");

    public Rgb Bg2 => Opaque("--mat-bg-2");

    public Rgb Surface => Opaque("--mat-surface");

    public Rgb Surface2 => Opaque("--mat-surface-2");

    public Rgb Surface3 => Opaque("--mat-surface-3");

    public Rgb Glass => Opaque("--mat-glass");

    public Rgb Nav => Opaque("--mat-nav-bg");

    /// <summary>The value of a token as the CSS writes it (<c>#fcf9f2</c>, <c>rgba(90, 60, 10, .19)</c>, <c>none</c>).</summary>
    public string Value(string token) => _tokens[token];

    /// <summary>A colour token painted over a background: text over a surface, a hairline over a card, a tint over the page.</summary>
    public Rgb Paint(string token, Rgb over) => Rgba.Parse(_tokens[token]).Over(over);

    /// <summary>The primary text colour over the surface (the text tokens are opaque in every scheme).</summary>
    public Rgb Text => Paint("--mat-text", Surface);

    private Rgb Opaque(string token) => Rgba.Parse(_tokens[token]).Over(Dark ? Rgb.Black : Rgb.White);

    /// <summary>The colours of <paramref name="scheme"/> (null or "standard" for the standard look) in light or dark mode, read from the text of <c>matui.tokens.css</c>.</summary>
    public static SchemeSurfaces Read(string tokensCss, string? scheme, bool dark)
    {
        var tokens = CssTokens.Tokens(tokensCss).ToDictionary(t => t.Name, t => dark ? t.Dark ?? t.Light : t.Light);
        var id = ColorSchemes.Normalize(scheme);
        var blocks = id is null ? null : CssTokens.Schemes(tokensCss).FirstOrDefault(s => s.Id == id);
        var values = blocks is null ? null : dark ? blocks.Dark : blocks.Light;
        var set = new HashSet<string>();
        foreach (var (name, value) in values ?? new Dictionary<string, string>())
        {
            tokens[name] = value;
            set.Add(name);
        }

        return new SchemeSurfaces(id ?? ColorSchemes.Standard, dark, values is null, tokens, set);
    }

    /// <summary>The modes in which a scheme has colours of its own: both, except for a dark-only scheme (in light mode it is the standard look, which is checked as such).</summary>
    public static IReadOnlyList<SchemeSurfaces> Modes(string tokensCss, string? scheme)
        => ColorSchemes.Get(ColorSchemes.Normalize(scheme))?.DarkOnly == true
            ? [Read(tokensCss, scheme, dark: true)]
            : [Read(tokensCss, scheme, dark: false), Read(tokensCss, scheme, dark: true)];

    /// <summary>
    /// The pairings that do not depend on the accent and have to hold on these surfaces: the text colours on every surface, the status texts on a surface and on their own tint,
    /// the border of a control. WCAG AA: 4.5 to 1 for text, 3 to 1 for the border of a control; faint text is for placeholders only and is shown as advice.
    /// </summary>
    public IReadOnlyList<ContrastCheck> Checks()
    {
        var checks = new List<ContrastCheck>();
        var surfaces = new (string Name, Rgb Colour)[] { ("the page", Bg), ("a surface", Surface), ("a tinted surface", Surface3), ("the menu", Nav) };
        foreach (var (name, colour) in surfaces)
        {
            checks.Add(Check($"Text on {name} ({Mode})", Paint("--mat-text", colour), colour, 4.5));
        }

        foreach (var (name, colour) in surfaces)
        {
            checks.Add(Check($"Muted text on {name} ({Mode})", Paint("--mat-text-muted", colour), colour, 4.5));
        }

        checks.Add(Check($"Faint text on a surface ({Mode}), for placeholders", Paint("--mat-text-faint", Surface), Surface, 3.0, advisory: true));
        checks.Add(Check($"Border of a control on a surface ({Mode})", Paint("--mat-border-strong", Surface), Surface, 3.0, advisory: Scheme != "contrast"));
        foreach (var (key, name) in new[] { ("danger", "Danger"), ("success", "Success"), ("warning", "Warning"), ("info", "Info") })
        {
            var text = Paint($"--mat-{key}-text", Surface);
            var tint = Paint($"--mat-{key}-bg", Surface);
            checks.Add(Check($"{name} text on a surface ({Mode})", text, Surface, 4.5));
            checks.Add(Check($"{name} text on its tint ({Mode})", Paint($"--mat-{key}-text", tint), tint, 4.5));
        }

        return checks;
    }

    private static ContrastCheck Check(string name, Rgb fg, Rgb bg, double required, bool advisory = false)
        => new(name, fg, bg, Colors.Contrast(fg, bg), required, advisory);
}

/// <summary>The weakest accent for one pairing on the surfaces of a scheme: how close it comes to what is needed, and which accent it is.</summary>
public sealed record WeakestAccent(string Check, double Ratio, double Required, bool Advisory, string Accent);

/// <summary>The scheme dimension of the contrast checks: every scheme, in every mode it has, for every accent an app can have.</summary>
public static class SchemeContrast
{
    /// <summary>
    /// Every accent that is checked: the apps (their own colour with their own text colours) and the presets a user can choose in the settings (white text on it, the link derived).
    /// A free colour cannot be listed; the builder shows it.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name, AccentReport Report)> Accents(IEnumerable<MatApp> apps)
    {
        var list = apps.Select(a => (a.Id, a.Name, AccentReport.For(a))).ToList();
        var template = AppRegistry.Get("matdesign")!;
        list.AddRange(AccentPresets.All.Select(p => ($"preset:{p.Id}", $"{p.Name} (preset)", AccentReport.For(template with { Accent = p.Value, OnAccent = null, AccentText = null }))));
        return list;
    }

    /// <summary>The pairings that depend on the accent, each with the accent that reaches it least well (the site shows these numbers; a test requires the required ones to pass for every accent).</summary>
    public static IReadOnlyList<WeakestAccent> Weakest(SchemeSurfaces surfaces, IEnumerable<MatApp> apps)
    {
        var accents = Accents(apps);
        return accents[0].Report.Checks(surfaces)
            .Select((check, index) =>
            {
                var worst = accents.Select(a => (a.Name, Check: a.Report.Checks(surfaces)[index])).MinBy(x => x.Check.Ratio);
                return new WeakestAccent(check.Name, worst.Check.Ratio, check.Required, check.Advisory, worst.Name);
            })
            .ToList();
    }
}
