using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Builder;

/// <summary>
/// Tries an accent: derives the colours the toolkit would use, checks the contrast in both modes, names the nearest app and gives the
/// snippet for the app's CSS. The preview frames are the real sample app with this accent.
/// </summary>
public partial class IndexModel(DesignData design) : PageModel
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexRegex();

    public MatApp BaseApp { get; private set; } = null!;

    public string Accent { get; private set; } = string.Empty;

    /// <summary>True when the accent is not the one of the app (a proposal or a colour the user picked).</summary>
    public bool Custom { get; private set; }

    public AccentReport Report { get; private set; } = null!;

    public MatApp Nearest { get; private set; } = null!;

    public double NearestDistance { get; private set; }

    public string Snippet { get; private set; } = string.Empty;

    public bool TooClose => NearestDistance < PaletteAdvisor.TooClose;

    public string FrameUrl(string view, string theme)
        => $"/Preview?app={BaseApp.Id}{(Custom ? "&accent=" + Uri.EscapeDataString(Accent) : string.Empty)}&view={view}&theme={theme}";

    public string PresetUrl(string value) => $"/Builder?app={BaseApp.Id}&accent={Uri.EscapeDataString(value)}";

    public void OnGet(string? app, string? accent)
    {
        BaseApp = design.App(app) ?? design.App("mattiverse")!;
        var hex = accent is not null && HexRegex().IsMatch(accent) ? accent.ToLowerInvariant() : BaseApp.Accent.ToLowerInvariant();
        Accent = hex;
        Custom = !hex.Equals(BaseApp.Accent, StringComparison.OrdinalIgnoreCase);

        var effective = Custom ? BaseApp with { Accent = hex, OnAccent = OnAccentFor(Rgb.Parse(hex)), AccentText = null } : BaseApp;
        Report = AccentReport.For(effective);

        var rgb = Rgb.Parse(hex);
        var nearest = design.Apps.Where(a => a.Id != BaseApp.Id).Select(a => (App: a, Distance: Colors.Distance(rgb, a.AccentRgb))).MinBy(x => x.Distance);
        Nearest = nearest.App;
        NearestDistance = nearest.Distance;

        Snippet = BuildSnippet(effective);
    }

    /// <summary>White text if it is readable on the accent (or better than dark text), otherwise the dark ink. Null means white.</summary>
    private static string? OnAccentFor(Rgb accent)
    {
        var white = Colors.Contrast(Rgb.White, accent);
        var ink = Colors.Contrast(AccentReport.Ink, accent);
        return white >= 4.5 || white >= ink ? null : AccentReport.Ink.ToHex();
    }

    private string BuildSnippet(MatApp app)
    {
        var sb = new StringBuilder();
        sb.Append("<html lang=\"en\" data-app=\"").Append(app.Id).Append("\">\n\n");
        sb.Append("/* the colour of the app (matui.apps.css already has it for the family) */\n");
        sb.Append("[data-app=\"").Append(app.Id).Append("\"] { --mat-app-accent: ").Append(Accent);
        if (app.OnAccent is { } on)
        {
            sb.Append("; --mat-app-on-accent: ").Append(on);
        }

        sb.Append("; }\n");
        if (Custom)
        {
            sb.Append("\n// a colour the user picks in the settings (IDENT-05); the script works out the text colour\n");
            sb.Append(CultureInfo.InvariantCulture, $"MatUi.accent.custom('{Accent}');\n");
        }

        return sb.ToString();
    }
}
