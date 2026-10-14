namespace MatDesign.Core.Studio;

/// <summary>
/// The brand of a company (docs/PRODUCT.md, stage 10): how it names itself and which tokens of the toolkit it sets. <see cref="Light"/> and <see cref="Dark"/> map the name of a
/// token (<c>--mat-bg</c>) to the value the brand gives it; a token that is not in the map keeps the toolkit's own value. The Studio keeps exactly one brand, the item <see cref="ItemId"/>
/// of the kind <see cref="Kind"/>, and what it sets is written after the tokens in <c>matui.css</c> (<c>COLOR-11</c>): a brand changes tokens only, within limits, and is checked for AA.
/// </summary>
public sealed record Brand(string Company, string ShortName, string Tagline, IReadOnlyDictionary<string, string> Light, IReadOnlyDictionary<string, string> Dark)
{
    /// <summary>The kind of the Studio item that holds the brand.</summary>
    public const string Kind = "brand";

    /// <summary>The id of the one item of that kind.</summary>
    public const string ItemId = "brand";

    private static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>();

    /// <summary>The brand that is built in: the demo data of the Mat family, no token changed.</summary>
    public static Brand BuiltIn { get; } = new("Mat", "Mat", "One look for every Mat app", Nothing, Nothing);

    /// <summary>True when the brand sets at least one token.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasOverrides => (Light?.Count ?? 0) > 0 || (Dark?.Count ?? 0) > 0;

    /// <summary>The values of a mode (never null, also when a stored brand had none).</summary>
    public IReadOnlyDictionary<string, string> Values(bool dark) => (dark ? Dark : Light) ?? Nothing;
}
