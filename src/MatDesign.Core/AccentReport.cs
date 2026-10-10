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
    public static readonly Rgb DarkSurface = Rgb.Parse("#131a2e");
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

    private static ContrastCheck Check(string name, Rgb fg, Rgb bg, double required, bool advisory = false)
        => new(name, fg, bg, Colors.Contrast(fg, bg), required, advisory);
}
