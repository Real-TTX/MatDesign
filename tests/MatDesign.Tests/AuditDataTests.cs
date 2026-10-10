using System.Text.Json;
using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>The snapshot of the static scan (Data/audit.json) is well formed and speaks about apps that exist.</summary>
public class AuditDataTests
{
    private static readonly JsonElement Root = JsonDocument.Parse(File.ReadAllText(TestPaths.Web("Data/audit.json"))).RootElement;

    private static readonly string[] States = ["yes", "partial", "no", "unknown"];

    [Fact]
    public void The_snapshot_has_a_date_checks_and_apps()
    {
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", Root.GetProperty("generated").GetString());
        Assert.True(Root.GetProperty("checks").GetArrayLength() >= 12);
        Assert.True(Root.GetProperty("apps").GetArrayLength() >= 10);
    }

    [Fact]
    public void Every_check_names_a_rule_that_exists()
    {
        foreach (var check in Root.GetProperty("checks").EnumerateArray())
        {
            Assert.True(RuleBook.Get(check.GetProperty("rule").GetString()!) is not null, $"{check.GetProperty("id").GetString()} cites a rule that does not exist");
            Assert.False(string.IsNullOrWhiteSpace(check.GetProperty("how").GetString()));
        }
    }

    [Fact]
    public void Every_scanned_app_is_in_the_registry_and_has_a_result_for_every_check()
    {
        var checks = Root.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("id").GetString()!).ToList();
        Assert.Equal(checks.Count, checks.Distinct().Count());

        foreach (var app in Root.GetProperty("apps").EnumerateArray())
        {
            var id = app.GetProperty("id").GetString()!;
            Assert.NotNull(AppRegistry.Get(id));
            var results = app.GetProperty("results");
            Assert.Equal(checks.Order(), results.EnumerateObject().Select(p => p.Name).Order());
            foreach (var result in results.EnumerateObject())
            {
                Assert.Contains(result.Value.GetProperty("state").GetString(), States);
            }
        }
    }

    [Fact]
    public void Every_app_of_the_registry_was_scanned()
    {
        var scanned = Root.GetProperty("apps").EnumerateArray().Select(a => a.GetProperty("id").GetString()).ToHashSet();
        var missing = AppRegistry.All.Where(a => !scanned.Contains(a.Id)).Select(a => a.Name).ToList();
        Assert.True(missing.Count == 0, "Not in the audit: " + string.Join(", ", missing));
    }
}
