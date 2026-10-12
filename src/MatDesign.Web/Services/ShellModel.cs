using System.Net;
using System.Text;
using MatDesign.Core;

namespace MatDesign.Web.Services;

/// <summary>
/// An entry of the user's context menu. <see cref="Danger"/> items are drawn in the danger colour; Sign out is always last. A <see cref="Post"/> item
/// changes something (Sign out), so it is a button of a small form with an anti-forgery token, not a link.
/// </summary>
public sealed record UserMenuItem(string Title, string Href, string Icon, bool Danger = false, bool SeparatorBefore = false, bool Post = false);

/// <summary>Where the menu switcher is drawn (SHELL-15): under the logo, at the foot of the menu directly above the user, or at both places.</summary>
public enum SwitcherPlace
{
    Top,
    Bottom,
    Both,
}

/// <summary>
/// An entry of the popup of the switcher: one menu, or a <see cref="Parent"/> with the <see cref="Menus"/> below it. A parent is not a menu itself.
/// </summary>
public sealed record SwitcherEntry(NavSetParent? Parent, IReadOnlyList<NavSet> Menus)
{
    /// <summary>True when a menu below the parent asks for attention (a dot on the parent while it is closed).</summary>
    public bool NeedsAttention => Menus.Any(m => m.NeedsAttention);
}

/// <summary>
/// Everything the shell (the layout) needs to draw an app: which app, its menu, the user, the version. The site draws itself with
/// <see cref="Site"/>; the preview pages draw a sample app of the family with the same layout, so the shell is one code base (CODE-01).
/// </summary>
public sealed class ShellModel
{
    public required MatApp App { get; init; }

    /// <summary>The menus of the app, every one a panel of items. With more than one, the switcher appears (SHELL-15).</summary>
    public required IReadOnlyList<NavSet> Menus { get; init; }

    public string? ActiveHref { get; init; }

    public bool HasSwitcher => Menus.Count > 1;

    /// <summary>Where the switcher is drawn: under the logo (the default), at the foot above the user, or both.</summary>
    public SwitcherPlace SwitcherPlace { get; init; } = SwitcherPlace.Top;

    public bool SwitcherAtTop => HasSwitcher && SwitcherPlace is SwitcherPlace.Top or SwitcherPlace.Both;

    public bool SwitcherAtBottom => HasSwitcher && SwitcherPlace is SwitcherPlace.Bottom or SwitcherPlace.Both;

    /// <summary>
    /// What the popup of the switcher lists, in the order of <see cref="Menus"/>: a menu without a parent is an entry of its own, the menus of one parent are
    /// gathered below that parent, which stands where its first menu is.
    /// </summary>
    public IReadOnlyList<SwitcherEntry> SwitcherEntries
    {
        get
        {
            var entries = new List<SwitcherEntry>();
            foreach (var menu in Menus)
            {
                var at = menu.Parent is null ? -1 : entries.FindIndex(e => e.Parent?.Key == menu.Parent.Key);
                if (at < 0)
                {
                    entries.Add(new SwitcherEntry(menu.Parent, [menu]));
                }
                else
                {
                    entries[at] = entries[at] with { Menus = [.. entries[at].Menus, menu] };
                }
            }

            return entries;
        }
    }

    /// <summary>The menu that is shown: the one that has the current page, otherwise the first.</summary>
    public NavSet CurrentMenu(string? active) => Menus.FirstOrDefault(m => m.Contains(active)) ?? Menus[0];

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

    /// <summary>
    /// The site as a visitor sees it, or as the administrator of the Studio does (<paramref name="signedIn"/>: a third menu, a user menu with the Studio and Sign out).
    /// <paramref name="studioOn"/> says whether there is a Studio to sign in to, <paramref name="needsSetup"/> that it has no administrator yet (the way in is the setup assistant);
    /// <paramref name="design"/> is the data as the Studio left it (the built-in data when empty).
    /// </summary>
    public static ShellModel Site(bool studioOn = false, bool signedIn = false, MatDesign.Web.Studio.DesignData? design = null, MatDesign.Web.Studio.DesignChecks? checks = null, bool needsSetup = false)
    {
        var facts = design?.NavFacts ?? NavFacts.BuiltIn;
        if (signedIn && checks is not null)
        {
            facts = facts with { CheckErrors = checks.Report.Errors };
        }

        return new ShellModel
        {
            App = design?.App("matdesign") ?? AppRegistry.Get("matdesign")!,
            Tagline = "Identity of the Mat apps",
            Menus = Navigation.For(facts, signedIn),
            // the site has its switcher at the foot of the menu, directly above the user (SHELL-15); an app may choose SwitcherPlace.Top or Both instead
            SwitcherPlace = SwitcherPlace.Bottom,
            UserName = signedIn ? "Admin" : "Visitor",
            UserRole = signedIn ? "Studio" : "Guest",
            UserLine = signedIn ? "Signed in to the Studio" : needsSetup ? "Set up the Studio" : studioOn ? "Sign in to maintain the design" : "The Studio is off",
            UserMenu = SiteUserMenu(signedIn, needsSetup),
            Version = $"MatDesign {AppVersion.Current}",
        };
    }

    private static List<UserMenuItem> SiteUserMenu(bool signedIn, bool needsSetup)
    {
        var items = new List<UserMenuItem>
        {
            signedIn ? new("Studio", "/Studio", "wrench") : needsSetup ? new("Studio: set up", "/Studio/Setup", "key") : new("Studio: sign in", "/Studio/Login", "login"),
            new("Download the toolkit", "/Download", "download"),
            new("Rulebook", "/Rules", "book"),
            new("API: version", "/api/version", "code"),
        };
        if (signedIn)
        {
            items.Add(new("Sign out", "/Studio/Logout", "logout", Danger: true, SeparatorBefore: true, Post: true));
        }

        return items;
    }
}
