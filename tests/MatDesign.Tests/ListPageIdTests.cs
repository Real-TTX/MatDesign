using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The toolbar of every list draws three dialogs with fields of their own: no id may be there twice, whatever the sort options are called (a sort by "title" once met the title of its own dialog).</summary>
public sealed partial class ListPageIdTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [Theory]
    [InlineData("/Rules")]
    [InlineData("/Identity")]
    [InlineData("/Identity/Palette")]
    [InlineData("/Identity/Audit")]
    [InlineData("/Foundations/Icons")]
    [InlineData("/Foundations/Tokens")]
    [InlineData("/Patterns")]
    public async Task A_list_page_has_no_id_twice(string url)
    {
        var html = await factory.CreateClient().GetStringAsync(url);

        var ids = IdAttribute().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        var twice = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(twice.Count == 0, $"{url}: ids that appear twice: {string.Join(", ", twice)}");
    }
}
