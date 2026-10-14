using System.Text.Json;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio;

/// <summary>What the form of the brand sends besides the token values: how the company names itself.</summary>
public sealed class BrandInput
{
    public string? Company { get; set; }

    public string? ShortName { get; set; }

    public string? Tagline { get; set; }
}

/// <summary>
/// The brand of the company (docs/PRODUCT.md, stage 10): its names and a table of the tokens of the toolkit that a brand may set, light and dark, each with an input of its kind and a reset, and a
/// live preview. Saving checks everything <see cref="BrandValidator"/> holds a brand to (kind and limits of every value, the contrast of the surfaces and of every app's accent, <c>COLOR-11</c>); what
/// is wrong stays on the page with the form as it was typed. Deleting the saved brand brings the built-in one back.
/// </summary>
public class BrandModel(StudioService studio, DesignData design, ToolkitFiles toolkit) : StudioPageModel(studio)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [BindProperty]
    public BrandInput Input { get; set; } = new();

    /// <summary>The brand as the form describes it (what was typed, normalised), or the saved one on a GET.</summary>
    public Brand Current { get; private set; } = Brand.BuiltIn;

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public bool Checked { get; private set; }

    public ItemSource Source => design.BrandSource;

    /// <summary>The tokens a brand may set, in the order of the CSS.</summary>
    public IReadOnlyList<BrandToken> Tokens => toolkit.EditableTokens;

    public IEnumerable<IGrouping<string, BrandToken>> Groups => Tokens.GroupBy(t => t.Group);

    /// <summary>The tokens a brand may not set, with the reason, grouped by reason.</summary>
    public IEnumerable<IGrouping<string, FixedToken>> Fixed => BrandTokens.Fixed(toolkit.Tokens).GroupBy(t => t.Reason);

    /// <summary>The pairings that do not depend on the accent, for the standard look in light and dark, on the surfaces of the brand (the valid part of what was typed).</summary>
    public IReadOnlyList<BrandContrastResult> Standard { get; private set; } = [];

    /// <summary>The pairings that depend on the accent, in the standard look, with the app that reaches each least well.</summary>
    public IReadOnlyList<BrandContrastResult> WithAccents { get; private set; } = [];

    /// <summary>The same pairings as data: the script of the page works out the accent-independent contrast in the browser while a value is typed.</summary>
    public string PairingsJson => JsonSerializer.Serialize(SchemeSurfaces.Pairings.Select(p => new { p.Name, p.Foreground, p.Background, p.Over, p.Required, p.Advisory, p.AdvisoryUnlessContrast }), Web);

    /// <summary>The derived accent tokens as the CSS writes them in light and dark: the preview sets them itself so that it can show the mode that the page is not in.</summary>
    public string AccentJson => JsonSerializer.Serialize(new
    {
        light = toolkit.Tokens.Where(t => t.Group == "Accent").ToDictionary(t => t.Name, t => t.Light),
        dark = toolkit.Tokens.Where(t => t.Group == "Accent").ToDictionary(t => t.Name, t => t.Dark ?? t.Light),
    }, Web);

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    /// <summary>What the brand sets for a token in a mode; empty when it leaves the toolkit's value.</summary>
    public string ValueOf(BrandToken token, bool dark) => Current.Values(dark).GetValueOrDefault(token.Name, string.Empty);

    public static string IdOf(BrandToken token, bool dark) => (dark ? "dark-" : "light-") + token.Key;

    /// <summary>The id of the input a problem is about, for the link in the summary (a component property is on the page of the components).</summary>
    public static string TargetOf(string field)
        => field.Split(':') switch
        {
            ["Scheme", var scheme, var mode, var name] => $"scheme-{scheme}-{mode.ToLowerInvariant()}-{name.TrimStart('-')}",
            ["Scheme", var scheme] => "scheme-" + scheme,
            ["Component", var name] => "/Studio/Brand/Components#component-" + name.TrimStart('-'),
            [var mode, var name] => (mode == "Dark" ? "dark-" : "light-") + name.TrimStart('-'),
            _ => field == "Contrast" ? "contrast-title" : "Input_" + field,
        };

    /// <summary>The colour schemes of the toolkit, each with the tokens a brand may change inside it (those the scheme itself sets) and its own values.</summary>
    public IReadOnlyList<SchemeRows> SchemeSections => ColorSchemes.All
        .Select(s =>
        {
            var blocks = toolkit.Schemes.FirstOrDefault(b => b.Id == s.Id);
            var rows = Tokens
                .Where(t => (blocks?.Light?.ContainsKey(t.Name) ?? false) || (blocks?.Dark?.ContainsKey(t.Name) ?? false))
                .Select(t => new SchemeRow(t, blocks?.Light?.GetValueOrDefault(t.Name), blocks?.Dark?.GetValueOrDefault(t.Name)))
                .ToList();
            return new SchemeRows(s, rows);
        })
        .ToList();

    /// <summary>What the brand sets for a token inside a scheme; empty when it leaves the scheme's value.</summary>
    public string SchemeValueOf(string scheme, BrandToken token, bool dark) => Current.SchemeFor(scheme)?.Values(dark).GetValueOrDefault(token.Name, string.Empty) ?? string.Empty;

    /// <summary>How many tokens the brand sets inside a scheme.</summary>
    public int SchemeCount(string scheme) => Current.SchemeFor(scheme) is { } v ? v.Values(false).Count + v.Values(true).Count : 0;

    /// <summary>The required pairings that fail inside a scheme on the surfaces of the brand, worked out on Check and on Save.</summary>
    public IReadOnlyList<BrandContrastResult> SchemeFailures(string scheme) => All.Where(r => r.Scheme == scheme && r.Fails).ToList();

    /// <summary>A colour scheme and the tokens a brand may change inside it.</summary>
    public sealed record SchemeRows(ColorScheme Scheme, IReadOnlyList<SchemeRow> Rows);

    /// <summary>A token a scheme sets, with the scheme's value in light and in dark (null where it sets none).</summary>
    public sealed record SchemeRow(BrandToken Token, string? Light, string? Dark);

    private IReadOnlyList<BrandContrastResult> All { get; set; } = [];

    public IActionResult OnGet()
    {
        Input = new BrandInput { Company = design.Brand.Company, ShortName = design.Brand.ShortName, Tagline = design.Brand.Tagline };
        Current = design.Brand;
        Evaluate(Current);
        return Page();
    }

    /// <summary>Saves, or (with the Check button, <c>check</c>) only shows what the form would give: the problems and the contrast.</summary>
    public IActionResult OnPost(bool check = false)
    {
        var typed = Describe();
        var problems = Validate(typed);
        Current = typed;
        Checked = check;
        Evaluate(typed);
        if (problems.Count > 0 || check)
        {
            Problems = problems;
            return Page();
        }

        var before = design.Brand;
        Studio.Store.Save(Brand.Kind, Brand.ItemId, typed, Studio.Administrator, design.StudioBrand is null ? "Created: " + BrandForm.What(before, typed) : "Changed " + BrandForm.What(before, typed));
        design.Reload();
        TempData["Flash"] = "The brand was saved.";
        return RedirectToPage("/Studio/Brand");
    }

    /// <summary>Resets the brand: the saved item is deleted and the built-in brand is back.</summary>
    public IActionResult OnPostReset()
    {
        if (design.StudioBrand is null)
        {
            return RedirectToPage("/Studio/Brand");
        }

        Studio.Store.Delete(Brand.Kind, Brand.ItemId, Studio.Administrator, "Reset to the built-in brand");
        design.Reload();
        TempData["Flash"] = "The built-in brand is back.";
        return RedirectToPage("/Studio/Brand");
    }

    /// <summary>
    /// The brand the form describes: the names, every field called <c>light:--mat-...</c> or <c>dark:--mat-...</c>, and what is set inside each colour scheme (<c>scheme:paper:light:--mat-...</c>).
    /// The component properties are edited on their own page and stay as they are saved.
    /// </summary>
    private Brand Describe()
    {
        var brand = new Brand(Input.Company ?? string.Empty, Input.ShortName ?? string.Empty, Input.Tagline ?? string.Empty,
            BrandForm.Values(Request.Form, "light:"), BrandForm.Values(Request.Form, "dark:"), design.Brand.Components, BrandForm.Schemes(Request.Form));
        return BrandValidator.Normalize(brand, Tokens, toolkit.EditableProperties, toolkit.Schemes);
    }

    private IReadOnlyList<Problem> Validate(Brand brand) => BrandValidator.Validate(brand, toolkit.Tokens, toolkit.Schemes, design.Apps, toolkit.EditableProperties);

    /// <summary>The contrast on the surfaces of the brand; what is not a value of its token is left out, so that a typo does not hide the numbers of the rest.</summary>
    private void Evaluate(Brand brand)
    {
        var byName = Tokens.ToDictionary(t => t.Name);
        IReadOnlyDictionary<string, string> Usable(IReadOnlyDictionary<string, string> map, bool dark, IReadOnlyDictionary<string, string>? own = null)
            => map.Where(p => byName.TryGetValue(p.Key, out var t) && (!dark || own is not null || t.HasDark) && (own is null || own.ContainsKey(p.Key)) && BrandValidator.CheckValue(t, p.Value) is null).ToDictionary(p => p.Key, p => p.Value);

        var schemes = (brand.Schemes ?? new Dictionary<string, SchemeValues>()).Where(s => ColorSchemes.IsKnown(s.Key)).ToDictionary(s =>
            s.Key,
            s =>
            {
                var blocks = toolkit.Schemes.FirstOrDefault(b => b.Id == s.Key);
                return new SchemeValues(blocks?.Light is null ? new Dictionary<string, string>() : Usable(s.Value.Values(false), false, blocks.Light), blocks?.Dark is null ? new Dictionary<string, string>() : Usable(s.Value.Values(true), true, blocks.Dark));
            });
        var usable = brand with { Light = Usable(brand.Values(false), false), Dark = Usable(brand.Values(true), true), Schemes = schemes };
        Standard = BrandContrast.Standard(toolkit.Tokens, toolkit.Schemes, usable);
        All = BrandContrast.Run(toolkit.Tokens, toolkit.Schemes, usable, design.Apps);
        WithAccents = All.Where(r => r.Scheme == ColorSchemes.Standard && r.Accent is not null).ToList();
    }
}
