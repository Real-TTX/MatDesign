using System.Text.Json;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Web.Studio;

/// <summary>
/// What the history pages need to know about the kinds of items the Studio keeps (apps, rules, components): their names, their pages, and whether an old revision may be
/// restored. A revision is restored only if it would be accepted as it is today: the same checks as for a save run on it, with the other items as they are now (a port that is
/// taken since, a rule that is gone, a name that somebody else has). Restoring appends a copy of the old revision, so nothing is ever lost.
/// </summary>
public sealed class StudioHistory(StudioService studio, DesignData design, ToolkitFiles toolkit)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The kinds that have a history page.</summary>
    public static readonly IReadOnlyList<string> Kinds = ["apps", "rules", "components", Brand.Kind, Tenant.Kind, WhiteLabelSetting.Kind, BrandAssets.Kind];

    public static bool IsKind(string? kind) => kind is not null && Kinds.Contains(kind);

    public static string Singular(string kind) => kind switch { "apps" => "App", "rules" => "Rule", Brand.Kind => "Brand", Tenant.Kind => "Company", WhiteLabelSetting.Kind => "White label setting", BrandAssets.Kind => "Brand assets", _ => "Component" };

    public static string Plural(string kind) => kind switch { "apps" => "Apps", "rules" => "Rules", Brand.Kind => "Brand", Tenant.Kind => "Company", WhiteLabelSetting.Kind => "White label", BrandAssets.Kind => "Brand/Assets", _ => "Components" };

    /// <summary>The list page of a kind in the Studio.</summary>
    public static string ListUrl(string kind) => kind == WhiteLabelSetting.Kind ? WhiteLabelUrl : $"/Studio/{Plural(kind)}";

    /// <summary>The page of the white label setting (one item, so its list page is its edit page).</summary>
    public const string WhiteLabelUrl = "/Studio/Releases/WhiteLabel";

    public static string EditUrl(string kind, string id)
        => kind == Brand.Kind ? "/Studio/Brand" : kind == Tenant.Kind ? "/Studio/Company" : kind == WhiteLabelSetting.Kind ? WhiteLabelUrl : kind == BrandAssets.Kind ? "/Studio/Brand/Assets" : $"/Studio/{Plural(kind)}/Edit?id={Uri.EscapeDataString(id)}";

    public static string HistoryUrl(string kind, string id) => $"/Studio/History?kind={kind}&id={Uri.EscapeDataString(id)}";

    /// <summary>The item as the site shows it now: its name or title, or the id when it is gone.</summary>
    public string TitleOf(string kind, string id) => kind switch
    {
        "apps" => design.App(id)?.Name,
        "rules" => design.Rule(id)?.Title,
        "components" => design.Component(id)?.Name,
        Brand.Kind => id == Brand.ItemId ? design.Brand.Company : null,
        Tenant.Kind => id == Tenant.ItemId ? design.Tenant.Company : null,
        WhiteLabelSetting.Kind => id == WhiteLabelSetting.ItemId ? "White label" : null,
        BrandAssets.Kind => id == BrandAssets.ItemId ? "Brand assets" : null,
        _ => null,
    } ?? id;

    /// <summary>True when the item exists on the site now (built in, added or changed).</summary>
    public bool Exists(string kind, string id) => kind switch
    {
        "apps" => design.App(id) is not null,
        "rules" => design.Rule(id) is not null,
        "components" => design.Component(id) is not null,
        Brand.Kind => id == Brand.ItemId,
        Tenant.Kind => id == Tenant.ItemId,
        WhiteLabelSetting.Kind => id == WhiteLabelSetting.ItemId,
        BrandAssets.Kind => id == BrandAssets.ItemId,
        _ => false,
    };

    /// <summary>True when the Studio has revisions of the item: the history button is there.</summary>
    public bool HasHistory(string kind, string id) => studio.Enabled && StudioStore.IsId(id) && studio.Store.Current(kind, id) is not null;

    /// <summary>Why a revision cannot be restored today; empty when it can.</summary>
    public IReadOnlyList<Problem> CanRestore(string kind, string id, Revision revision)
    {
        if (revision.Deleted)
        {
            // restoring a deletion deletes again: possible only while the item is not deleted, and not while a component of the Studio names the rule
            if (studio.Store.Current(kind, id) is not { Deleted: false })
            {
                return [new("Revision", "The item is deleted already.")];
            }

            return kind == "rules" && design.RuleSourceOf(id) == ItemSource.Added && design.ComponentsNaming(id) is { Count: > 0 } users
                ? [new("Revision", $"{id} is named by {string.Join(", ", users)}. Take it out of the rules of the component first.")]
                : [];
        }

        try
        {
            return kind switch
            {
                "apps" => CheckApp(revision.Data),
                "rules" => CheckRule(revision.Data),
                "components" => CheckComponent(revision.Data),
                Brand.Kind => CheckBrand(revision.Data),
                Tenant.Kind => CheckTenant(revision.Data),
                WhiteLabelSetting.Kind => CheckWhiteLabel(revision.Data),
                BrandAssets.Kind => CheckAssets(revision.Data),
                _ => [new("Kind", "Unknown kind of item.")],
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException)
        {
            return [new("Revision", "The revision cannot be read as " + Singular(kind).ToLowerInvariant() + " any more.")];
        }
    }

    private IReadOnlyList<Problem> CheckApp(JsonElement data)
    {
        var app = AppOverrides.Normalize(AppValidator.Normalize(data.Deserialize<MatApp>(Options)!), toolkit.EditableTokens, toolkit.EditableProperties);
        return [.. AppValidator.Validate(app, design.Apps.Where(a => a.Id != app.Id), toolkit.IconNames), .. AppOverrides.Validate(app, toolkit.EditableTokens, toolkit.EditableProperties, design.Brand)];
    }

    private IReadOnlyList<Problem> CheckRule(JsonElement data)
    {
        var rule = RuleValidator.Normalize(data.Deserialize<Rule>(Options)!);
        return RuleValidator.Validate(rule, design.Rules.Where(r => r.Id != rule.Id));
    }

    private IReadOnlyList<Problem> CheckComponent(JsonElement data)
    {
        var doc = ComponentValidator.Normalize(data.Deserialize<ComponentDoc>(Options)!);
        return ComponentValidator.Validate(doc, design.Components.Where(c => c.Id != doc.Id), rule => design.Rule(rule) is not null, toolkit.Facts);
    }

    private IReadOnlyList<Problem> CheckBrand(JsonElement data)
    {
        var brand = BrandValidator.Normalize(data.Deserialize<Brand>(Options)!, toolkit.EditableTokens, toolkit.EditableProperties, toolkit.Schemes);
        return BrandValidator.Validate(brand, toolkit.Tokens, toolkit.Schemes, design.Apps, toolkit.EditableProperties);
    }

    private static IReadOnlyList<Problem> CheckWhiteLabel(JsonElement data)
        => WhiteLabel.Validate(WhiteLabel.Normalize(data.Deserialize<WhiteLabelSetting>(Options)!));

    /// <summary>The company of an old revision must be one the Company page would save today (names, template, and for the blank template an accent that reaches AA).</summary>
    private static IReadOnlyList<Problem> CheckTenant(JsonElement data)
        => TenantValidator.Validate(TenantValidator.Normalize(data.Deserialize<Tenant>(Options)!));
    /// <summary>
    /// An old revision of the assets is restored only when every file it refers to is still on the volume (files are never deleted, so this is normally so), still has the hash in its name and
    /// would pass the checks of a save today.
    /// </summary>
    private IReadOnlyList<Problem> CheckAssets(JsonElement data)
    {
        var assets = BrandAssetValidator.Normalize(data.Deserialize<BrandAssets>(Options)!);
        return BrandAssetValidator.ValidateFiles(assets, toolkit.Assets.Store.Read, toolkit.ReservedFamilies);
    }

    /// <summary>Appends a copy of the revision as the newest one and reads the items again. <see cref="CanRestore"/> must have said yes.</summary>
    public Revision Restore(string kind, string id, int number)
    {
        var revision = studio.Store.Restore(kind, id, number, studio.Administrator);
        design.Reload();
        return revision;
    }
}
