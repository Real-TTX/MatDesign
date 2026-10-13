namespace MatDesign.Core;

/// <summary>
/// One colour pairing that has to reach a contrast: what it is, the two colours and the ratio they reach. An advisory check is shown on the
/// site but does not fail a test (a light accent cannot reach 3:1 against white, and does not have to: the text on it carries the control).
/// </summary>
public sealed record ContrastCheck(string Name, Rgb Foreground, Rgb Background, double Ratio, double Required, bool Advisory = false)
{
    public bool Passes => Ratio >= Required;
}

/// <summary>
/// The colours the toolkit derives from an app's accent in light and dark mode, computed the way <c>matui.tokens.css</c> does (OKLab mixing), and the
/// contrasts that have to hold. A unit test requires every app in the registry to pass; the site shows the numbers.
/// </summary>
public sealed class AccentReport
{
    // The surfaces of matui.tokens.css that text sits on.
    public static readonly Rgb LightSurface = Rgb.Parse("#ffffff");
    public static readonly Rgb DarkSurface = Rgb.Parse("#1e1e1c");
    public static readonly Rgb Ink = Rgb.Parse("#0b1020");

    private AccentReport(MatApp app, Rgb accent)
    {
        App = app;
        Accent = accent;

        // light
        Hover = Colors.Mix(accent, Rgb.Black, 0.86);
        Soft = Colors.Mix(accent, LightSurface, 0.11);
        OnAccent = app.OnAccentRgb;
        LinkLight = app.AccentText is { } text ? Rgb.Parse(text) : Colors.Mix(accent, Rgb.Black, 0.78);

        // dark: the accent is lightened, text on it is dark
        DarkAccent = Colors.Mix(accent, Rgb.White, 0.74);
        DarkOnAccent = Ink;
        LinkDark = Colors.Mix(DarkAccent, Rgb.White, 0.78);
    }

    public MatApp App { get; }

    public Rgb Accent { get; }

    public Rgb Hover { get; }

    public Rgb Soft { get; }

    public Rgb OnAccent { get; }

    public Rgb LinkLight { get; }

    public Rgb DarkAccent { get; }

    public Rgb DarkOnAccent { get; }

    public Rgb LinkDark { get; }

    public static AccentReport For(MatApp app) => For(app, app.AccentRgb);

    /// <summary>The report for any accent (the builder uses it): white text on it, or dark text when the accent is light.</summary>
    public static AccentReport For(MatApp app, Rgb accent) => new(app, accent);

    public IReadOnlyList<ContrastCheck> Checks() =>
    [
        Check("Text on the accent (light)", OnAccent, Accent, 4.5),
        Check("Link on a surface (light)", LinkLight, LightSurface, 4.5),
        Check("Accent against the surface (light), for large parts", Accent, LightSurface, 3.0, advisory: true),
        Check("Text on the accent (dark)", DarkOnAccent, DarkAccent, 4.5),
        Check("Link on a surface (dark)", LinkDark, DarkSurface, 4.5),
        Check("Accent against the surface (dark), for large parts", DarkAccent, DarkSurface, 3.0, advisory: true),
    ];

    public bool Passes => Checks().Where(c => !c.Advisory).All(c => c.Passes);

    /// <summary>
    /// The colour of a link on the surfaces of a scheme, as the CSS derives it: the accent's own text colour (<see cref="LinkLight"/>, <see cref="LinkDark"/>), pulled toward the text
    /// colour of the scheme by what <c>--mat-accent-text-keep</c> gives up. The standard look keeps all of it.
    /// </summary>
    public Rgb LinkOn(SchemeSurfaces surfaces)
    {
        var own = surfaces.Dark ? LinkDark : LinkLight;
        return surfaces.AccentKeep >= 1 ? own : Colors.Mix(own, surfaces.Text, surfaces.AccentKeep);
    }

    /// <summary>
    /// What has to hold for this accent on the surfaces of a colour scheme in one mode (the pairings that do not depend on the accent are <see cref="SchemeSurfaces.Checks"/>): the text
    /// on the accent, a link on the page, on a surface, in the menu, on a hovered surface and on the tint of a chip or a pill, and the current item of the menu. For a scheme they all
    /// have to reach AA. For the standard look only the first two are required, as they have always been; the others are advice, because a few accents are a little under them
    /// there and the standard look is not changed.
    /// </summary>
    public IReadOnlyList<ContrastCheck> Checks(SchemeSurfaces surfaces)
    {
        var mode = surfaces.Mode;
        var accent = surfaces.Dark ? DarkAccent : Accent;
        var on = surfaces.Dark ? DarkOnAccent : OnAccent;
        var link = LinkOn(surfaces);
        var tint = new Rgba(accent, surfaces.Dark ? 0.26 : 0.19);
        var tintOnSurface = tint.Over(surfaces.Surface);
        var tintInMenu = tint.Over(surfaces.Nav);
        var advice = surfaces.Scheme == ColorSchemes.Standard;
        return
        [
            Check($"Text on the accent ({mode})", on, accent, 4.5),
            Check($"Link on a surface ({mode})", link, surfaces.Surface, 4.5),
            Check($"Link on the page ({mode})", link, surfaces.Bg, 4.5, advice),
            Check($"Link in the menu ({mode})", link, surfaces.Nav, 4.5, advice),
            Check($"Link on a hovered or tinted surface ({mode})", link, surfaces.Surface3, 4.5, advice),
            Check($"Link on the tint of a chip or a pill ({mode})", link, tintOnSurface, 4.5, advice),
            Check($"Current menu item, its label ({mode})", surfaces.Paint("--mat-text", tintInMenu), tintInMenu, 4.5),
            Check($"Current menu item, its icon ({mode})", link, tintInMenu, 3.0, advice),
            Check($"Accent against a surface ({mode}), for large parts", accent, surfaces.Surface, 3.0, advisory: true),
        ];
    }

    private static ContrastCheck Check(string name, Rgb fg, Rgb bg, double required, bool advisory = false)
        => new(name, fg, bg, Colors.Contrast(fg, bg), required, advisory);
}
