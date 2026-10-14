namespace MatDesign.Core.Studio;

/// <summary>What a brand sets inside one colour scheme (<c>paper</c>, <c>contrast</c>, <c>oled</c>): the tokens that the scheme sets, in light and in dark (<c>oled</c> has no light values).</summary>
public sealed record SchemeValues(IReadOnlyDictionary<string, string> Light, IReadOnlyDictionary<string, string> Dark)
{
    private static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>();

    /// <summary>True when it sets at least one token.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasValues => (Light?.Count ?? 0) > 0 || (Dark?.Count ?? 0) > 0;

    /// <summary>The values of a mode (never null).</summary>
    public IReadOnlyDictionary<string, string> Values(bool dark) => (dark ? Dark : Light) ?? Nothing;
}

/// <summary>
/// The brand of a company (docs/PRODUCT.md, stages 10 and 12): how it names itself and which tokens of the toolkit it sets. <see cref="Light"/> and <see cref="Dark"/> map the name of a
/// token (<c>--mat-bg</c>) to the value the brand gives it; a token that is not in the map keeps the toolkit's own value. <see cref="Components"/> maps the name of a documented component
/// property (<c>--mat-btn-radius</c>) to its value (one value, for light and dark alike); <see cref="Schemes"/> maps the id of a colour scheme to the tokens the brand changes inside it.
/// The Studio keeps exactly one brand, the item <see cref="ItemId"/> of the kind <see cref="Kind"/>, and what it sets is written after the tokens in <c>matui.css</c> (<c>COLOR-11</c>,
/// <c>SPACE-06</c>): a brand changes tokens and properties only, within limits, and is checked for AA.
/// </summary>
public sealed record Brand(
    string Company,
    string ShortName,
    string Tagline,
    IReadOnlyDictionary<string, string> Light,
    IReadOnlyDictionary<string, string> Dark,
    IReadOnlyDictionary<string, string>? Components = null,
    IReadOnlyDictionary<string, SchemeValues>? Schemes = null)
{
    /// <summary>The kind of the Studio item that holds the brand.</summary>
    public const string Kind = "brand";

    /// <summary>The id of the one item of that kind.</summary>
    public const string ItemId = "brand";

    private static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>();

    /// <summary>The brand that is built in: the demo data of the Mat family, no token changed.</summary>
    public static Brand BuiltIn { get; } = new("Mat", "Mat", "One look for every Mat app", Nothing, Nothing);

    /// <summary>True when the brand sets at least one token, property or scheme token.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasOverrides => (Light?.Count ?? 0) > 0 || (Dark?.Count ?? 0) > 0 || (Components?.Count ?? 0) > 0 || (Schemes?.Values.Any(s => s.HasValues) ?? false);

    /// <summary>The values of a mode (never null, also when a stored brand had none).</summary>
    public IReadOnlyDictionary<string, string> Values(bool dark) => (dark ? Dark : Light) ?? Nothing;

    /// <summary>The component properties the brand sets (never null).</summary>
    public IReadOnlyDictionary<string, string> ComponentValues => Components ?? Nothing;

    /// <summary>What the brand sets inside a colour scheme, or null when nothing.</summary>
    public SchemeValues? SchemeFor(string id) => Schemes is not null && Schemes.TryGetValue(id, out var values) && values.HasValues ? values : null;
}
