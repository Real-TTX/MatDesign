using System.Net;
using System.Text;
using MatDesign.Core;

namespace MatDesign.Web.Services;

/// <summary>An entry of the user's context menu. <see cref="Danger"/> items are drawn in the danger colour; Sign out is always last.</summary>
public sealed record UserMenuItem(string Title, string Href, string Icon, bool Danger = false, bool SeparatorBefore = false);

/// <summary>
/// Everything the shell (the layout) needs to draw an app: which app, its menu, the user, the version. The site draws itself with
/// <see cref="Site"/>; the preview pages draw a sample app of the family with the same layout, so the shell is one code base (CODE-01).
/// </summary>
public sealed class ShellModel
{
    public required MatApp App { get; init; }

    public required IReadOnlyList<NavGroup> Nav { get; init; }

    public string? ActiveHref { get; init; }

    public required string UserName { get; init; }

    public required string UserRole { get; init; }

    /// <summary>A short line in the head of the user's menu (an e-mail address, or a note).</summary>
    public string UserLine { get; init; } = string.Empty;

    public IReadOnlyList<UserMenuItem> UserMenu { get; init; } = [];

    /// <summary>Shown below the user: the app name and its version.</summary>
    public required string Version { get; init; }

    /// <summary>Forces light or dark (a preview); null follows the user's choice and the system.</summary>
    public string? Theme { get; init; }

    /// <summary>A custom accent (a preview or the builder); null uses the app's own colour.</summary>
    public string? CustomAccent { get; init; }

    public string Tagline { get; init; } = string.Empty;

    /// <summary>True when links of the menu should leave the page alone (a preview inside a frame keeps its own address).</summary>
    public string? PreviewQuery { get; init; }

    /// <summary>
    /// The optional attributes of <c>&lt;html&gt;</c>, written only when they have a value. Razor writes an empty <c>data-theme=""</c> for a null, and an empty
    /// attribute is a present one: the boot script would take it for a theme chosen by the server and never apply the saved choice of the browser.
    /// </summary>
    public string RootAttributes
    {
        get
        {
            var sb = new StringBuilder();
            if (Theme is "light" or "dark")
            {
                sb.Append(" data-theme=\"").Append(Theme).Append('"');
            }

            if (CustomAccent is not null)
            {
                sb.Append(" data-accent=\"custom\" data-custom-accent=\"").Append(WebUtility.HtmlEncode(CustomAccent)).Append('"');
            }

            if (PreviewQuery is not null)
            {
                sb.Append(" data-preview");
            }

            return sb.ToString().TrimStart();
        }
    }

    public static ShellModel Site() => new()
    {
        App = AppRegistry.Get("matdesign")!,
        Tagline = "Identity of the Mat apps",
        Nav = Navigation.Groups,
        UserName = "Visitor",
        UserRole = "Guest",
        UserLine = "MatDesign has no accounts",
        UserMenu =
        [
            new("Download the toolkit", "/Download", "download"),
            new("Rulebook", "/Rules", "book"),
            new("API: version", "/api/version", "code"),
        ],
        Version = $"MatDesign {AppVersion.Current}",
    };
}
