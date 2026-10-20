namespace MatDesign.Web.Services;

/// <summary>
/// What the search says about the pages of the menu: one line of what is on the page, and words people look for it with. The menu knows a page's title and its address, nothing more.
/// A page that is not listed is found by its title alone. The names of the company's apps are the tenant's (<c>apps</c>), never a name written here (IDENT-09).
/// </summary>
internal static class SearchPages
{
    public static (string Text, string Keywords) Describe(string href, string apps) => href switch
    {
        "/" => ("The rules, the foundations and the toolkit at a glance", "start home overview startseite"),
        "/Identity" => ($"The {apps} and their accent colours", "apps accent colour logo family identity"),
        "/Identity/Palette" => ("Which accents are too close to each other, and proposals", "palette accents distance collision similar"),
        "/Identity/Audit" => ($"How close each of the {apps} is to the rules", "audit repositories compliance check snapshot"),
        "/Rules" => ("Every rule about how an app looks and behaves, with its id", "rulebook rules regeln regelwerk ids"),
        "/Foundations" => ("Colour: accents, surfaces, contrast in light and dark, the colour schemes", "colour color farbe palette contrast dark light scheme paper oled"),
        "/Foundations/Type" => ("Typography: the system font, the scale and the mono font", "type typography font schrift text size scale mono"),
        "/Foundations/Space" => ("Spacing on the 4 px scale, radii, shadows and layers", "space spacing radius radii shadow elevation layers abstand"),
        "/Foundations/Motion" => ("Durations, the easing curve and reduced motion", "motion animation transition duration easing reduced"),
        "/Foundations/Icons" => ("The icons of the sprite: search, filter and copy the markup", "icons sprite symbols svg"),
        "/Foundations/IconLibrary" => ("The optional icon library, sorted into categories", "icon library lucide categories home automation"),
        "/Foundations/Tokens" => ("Every custom property of the toolkit and every component property", "tokens custom properties variables css values schemes"),
        "/Shell" => ("The frame of every page: menu, header bar, footer", "shell frame layout menu sidebar header bar footer skeleton"),
        "/Components" => ("Every control with live examples, its markup and its rules", "components controls widgets examples markup"),
        "/Layout" => ("Breakpoints, containers and the grid of twelve columns", "layout grid columns breakpoints containers"),
        "/Utilities" => ("Classes that set one property from the scales of the toolkit", "utilities classes spacing display flex text colour"),
        "/Patterns" => ("The page types: list, gallery, edit, dashboard, settings", "patterns page types list edit dashboard settings templates"),
        "/Builder" => ("Try an accent colour and a colour scheme on a sample app", "builder accent colour picker custom scheme contrast"),
        "/Download" => ("Copy the toolkit: the CSS, the script and the icon sprite", "download copy files archive zip matui install"),
        "/Changelog" => ("What changed in each version of the toolkit, and what an app has to do", "changelog versions release notes breaking migration update"),
        _ => (string.Empty, string.Empty),
    };
}
