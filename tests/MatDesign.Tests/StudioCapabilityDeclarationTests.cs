using System.Reflection;
using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Pages.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>
/// Every place that changes something in the Studio declares the capability it needs (stage 16, CODE-10). A test reads the whole assembly: every page model of the Studio and every handler of it,
/// and every endpoint that is not a page. A page or a handler that is added without a declaration fails here, and the list of what each needs is written out, so that a change of it is a change of this
/// file, seen in a review.
/// </summary>
public sealed partial class StudioCapabilityDeclarationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex(@"^On(Get|Post|Put|Delete|Patch)([A-Za-z0-9]*?)(Async)?$")]
    private static partial Regex HandlerName();

    /// <summary>Page models of the Studio that are open without a person: the sign-in, the sign-out and the setup assistant. Nothing else may be.</summary>
    private static readonly string[] Open = ["LoginModel", "LogoutModel", "SetupModel"];

    private static IEnumerable<Type> StudioPages()
        => typeof(StudioPageModel).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(StudioPageModel).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static string Key(Type page, MethodInfo? handler)
        => (page.FullName!.Replace("MatDesign.Web.Pages.", string.Empty, StringComparison.Ordinal) + (handler is null ? string.Empty : "." + handler.Name));

    private static IEnumerable<MethodInfo> Handlers(Type page)
        => page.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => HandlerName().IsMatch(m.Name)).OrderBy(m => m.Name, StringComparer.Ordinal);

    private static string Needs(Type page, MethodInfo? handler)
        => string.Join("+", StudioAccess.Required(page, handler).Select(c => c.ToString()));

    /// <summary>What each page and handler of the Studio needs, in words a reviewer can read. A new line here is a decision.</summary>
    private static readonly Dictionary<string, string> Expected = new()
    {
        ["Studio.AccountModel"] = "ViewStudio",
        ["Studio.AccountModel.OnGet"] = "ViewStudio",
        ["Studio.AccountModel.OnPostPasswordAsync"] = "ViewStudio+ViewStudio",
        ["Studio.ActivityModel"] = "ViewStudio",
        ["Studio.ActivityModel.OnGet"] = "ViewStudio",
        ["Studio.Apps.EditModel"] = "ViewStudio",
        ["Studio.Apps.EditModel.OnGet"] = "ViewStudio",
        ["Studio.Apps.EditModel.OnPost"] = "ViewStudio+EditDesign",
        ["Studio.Apps.EditModel.OnPostDelete"] = "ViewStudio+EditDesign",
        ["Studio.Apps.IndexModel"] = "ViewStudio",
        ["Studio.Apps.IndexModel.OnGet"] = "ViewStudio",
        ["Studio.BrandComponentsModel"] = "ViewStudio",
        ["Studio.BrandComponentsModel.OnGet"] = "ViewStudio",
        ["Studio.BrandComponentsModel.OnPost"] = "ViewStudio+EditDesign",
        ["Studio.BrandModel"] = "ViewStudio",
        ["Studio.BrandModel.OnGet"] = "ViewStudio",
        ["Studio.BrandModel.OnPost"] = "ViewStudio+EditDesign",
        ["Studio.BrandModel.OnPostReset"] = "ViewStudio+EditDesign",
        ["Studio.ChecksModel"] = "ViewStudio",
        ["Studio.ChecksModel.OnGet"] = "ViewStudio",
        ["Studio.ChecksModel.OnPostRun"] = "ViewStudio+RunChecks",
        ["Studio.CompanyModel"] = "ViewStudio",
        ["Studio.CompanyModel.OnGet"] = "ViewStudio",
        ["Studio.CompanyModel.OnPost"] = "ViewStudio+ManageCompany",
        ["Studio.CompanyModel.OnPostSwitch"] = "ViewStudio+ManageCompany",
        ["Studio.Components.EditModel"] = "ViewStudio",
        ["Studio.Components.EditModel.OnGet"] = "ViewStudio",
        ["Studio.Components.EditModel.OnPost"] = "ViewStudio+EditDesign",
        ["Studio.Components.EditModel.OnPostAddExample"] = "ViewStudio+EditDesign",
        ["Studio.Components.EditModel.OnPostDelete"] = "ViewStudio+EditDesign",
        ["Studio.Components.EditModel.OnPostRemoveExample"] = "ViewStudio+EditDesign",
        ["Studio.Components.IndexModel"] = "ViewStudio",
        ["Studio.Components.IndexModel.OnGet"] = "ViewStudio",
        ["Studio.HistoryModel"] = "ViewStudio",
        ["Studio.HistoryModel.OnGet"] = "ViewStudio",
        ["Studio.HistoryModel.OnPostRestore"] = "ViewStudio+RestoreHistory",
        ["Studio.IndexModel"] = "ViewStudio",
        ["Studio.IndexModel.OnGet"] = "ViewStudio",
        ["Studio.Releases.IndexModel"] = "ViewStudio",
        ["Studio.Releases.IndexModel.OnGet"] = "ViewStudio",
        ["Studio.Releases.NewModel"] = "ManageReleases",
        ["Studio.Releases.NewModel.OnGet"] = "ManageReleases",
        ["Studio.Releases.NewModel.OnPost"] = "ManageReleases+ManageReleases",
        ["Studio.Releases.RunModel"] = "ViewStudio",
        ["Studio.Releases.RunModel.OnGet"] = "ViewStudio",
        ["Studio.Releases.WhiteLabelModel"] = "ViewStudio",
        ["Studio.Releases.WhiteLabelModel.OnGet"] = "ViewStudio",
        ["Studio.Releases.WhiteLabelModel.OnPost"] = "ViewStudio+ManageWhiteLabel",
        ["Studio.Releases.WhiteLabelModel.OnPostCheck"] = "ViewStudio+ManageWhiteLabel",
        ["Studio.Releases.WhiteLabelModel.OnPostReset"] = "ViewStudio+ManageWhiteLabel",
        ["Studio.Rules.EditModel"] = "ViewStudio",
        ["Studio.Rules.EditModel.OnGet"] = "ViewStudio",
        ["Studio.Rules.EditModel.OnPost"] = "ViewStudio+EditDesign",
        ["Studio.Rules.EditModel.OnPostDelete"] = "ViewStudio+EditDesign",
        ["Studio.Rules.IndexModel"] = "ViewStudio",
        ["Studio.Rules.IndexModel.OnGet"] = "ViewStudio",
        ["Studio.Users.EditModel"] = "ManageUsers",
        ["Studio.Users.EditModel.OnGet"] = "ManageUsers",
        ["Studio.Users.EditModel.OnPostCreate"] = "ManageUsers+ManageUsers",
        ["Studio.Users.EditModel.OnPostDelete"] = "ManageUsers+ManageUsers",
        ["Studio.Users.EditModel.OnPostDisable"] = "ManageUsers+ManageUsers",
        ["Studio.Users.EditModel.OnPostReset"] = "ManageUsers+ManageUsers",
        ["Studio.Users.EditModel.OnPostRole"] = "ManageUsers+ManageUsers",
        ["Studio.Users.IndexModel"] = "ManageUsers",
        ["Studio.Users.IndexModel.OnGet"] = "ManageUsers",
        ["Studio.Versions.EditModel"] = "ViewStudio",
        ["Studio.Versions.EditModel.OnGet"] = "ViewStudio",
        ["Studio.Versions.EditModel.OnPost"] = "ViewStudio+ManageVersions",
        ["Studio.Versions.EditModel.OnPostForget"] = "ViewStudio+ManageVersions",
        ["Studio.Versions.EditModel.OnPostLatest"] = "ViewStudio+ManageVersions",
        ["Studio.Versions.IndexModel"] = "ViewStudio",
        ["Studio.Versions.IndexModel.OnGet"] = "ViewStudio",
        ["StudioBrandAssets.AssetsModel"] = "ViewStudio",
        ["StudioBrandAssets.AssetsModel.OnGet"] = "ViewStudio",
        ["StudioBrandAssets.AssetsModel.OnPostAddFontAsync"] = "ViewStudio+EditDesign",
        ["StudioBrandAssets.AssetsModel.OnPostRemove"] = "ViewStudio+EditDesign",
        ["StudioBrandAssets.AssetsModel.OnPostRemoveFont"] = "ViewStudio+EditDesign",
        ["StudioBrandAssets.AssetsModel.OnPostReset"] = "ViewStudio+EditDesign",
        ["StudioBrandAssets.AssetsModel.OnPostSaveAsync"] = "ViewStudio+EditDesign",
    };

    [Fact]
    public void Every_page_of_the_studio_declares_what_opening_it_needs()
    {
        var missing = StudioPages().Where(t => StudioAccess.Required(t, null).Count == 0).Select(t => t.FullName).ToList();

        Assert.True(missing.Count == 0, "page models without a [RequireCapability]: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_handler_that_changes_something_declares_the_capability_it_needs_itself()
    {
        var missing = new List<string>();
        foreach (var page in StudioPages())
        {
            foreach (var handler in Handlers(page).Where(h => !h.Name.StartsWith("OnGet", StringComparison.Ordinal)))
            {
                if (handler.GetCustomAttributes<RequireCapabilityAttribute>().Count() != 1)
                {
                    missing.Add(Key(page, handler));
                }
            }
        }

        Assert.True(missing.Count == 0, "handlers without exactly one [RequireCapability] of their own: " + string.Join(", ", missing));
    }

    [Fact]
    public void Only_the_sign_in_the_sign_out_and_the_setup_are_open_to_somebody_who_is_not_signed_in()
    {
        var pages = typeof(StudioPageModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(PageModel).IsAssignableFrom(t) && (t.Namespace ?? string.Empty).StartsWith("MatDesign.Web.Pages.Studio", StringComparison.Ordinal))
            .Where(t => !typeof(StudioPageModel).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Order()
            .ToList();

        Assert.Equal(Open.Order(), pages);
    }

    [Fact]
    public void The_list_of_what_every_page_and_handler_needs_is_the_one_in_this_file()
    {
        var actual = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in StudioPages())
        {
            actual[Key(page, null)] = Needs(page, null);
            foreach (var handler in Handlers(page))
            {
                actual[Key(page, handler)] = Needs(page, handler);
            }
        }

        var report = string.Join("\n", actual.Select(p => $"        [\"{p.Key}\"] = \"{p.Value}\","));
        Assert.True(new SortedDictionary<string, string>(Expected, StringComparer.Ordinal).SequenceEqual(actual), "the declarations differ from the list in this file; they are now:\n" + report);
    }

    [Fact]
    public void Every_endpoint_that_changes_something_and_is_not_a_page_declares_a_capability()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var unsafeVerbs = new[] { "POST", "PUT", "DELETE", "PATCH" };
        var changing = endpoints
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods && methods.HttpMethods.Any(m => unsafeVerbs.Contains(m, StringComparer.OrdinalIgnoreCase)))
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.RazorPages.CompiledPageActionDescriptor>() is null && e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor>() is null)
            .Select(e => (Path: e.RoutePattern.RawText ?? string.Empty, Declared: e.Metadata.GetMetadata<RequireCapabilityAttribute>()))
            .ToList();

        // the demo endpoints of the documentation keep nothing and need no Studio (they say so in Program.cs)
        var withoutDeclaration = changing.Where(c => c.Declared is null && c.Path != "/api/demo/create").Select(c => c.Path).ToList();

        Assert.Contains(changing, c => c.Path == "/Studio/Releases/WhiteLabel/rename" && c.Declared?.Capability == Capability.ManageWhiteLabel);
        Assert.True(withoutDeclaration.Count == 0, "endpoints that change something and declare no capability: " + string.Join(", ", withoutDeclaration));
    }
}
