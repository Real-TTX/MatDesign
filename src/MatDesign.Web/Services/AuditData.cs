using System.Text.Json;

namespace MatDesign.Web.Services;

/// <summary>One thing the static scan looks for in an app's files.</summary>
public sealed record AuditCheck(string Id, string Title, string Rule, string How);

/// <summary>What the scan found for one check of one app: <c>yes</c>, <c>partial</c>, <c>no</c> or <c>unknown</c>, with a short note.</summary>
public sealed record AuditResult(string State, string Note);

/// <summary>The scan result of one app.</summary>
public sealed record AuditApp(string Id, string Layout, IReadOnlyDictionary<string, AuditResult> Results);

/// <summary>
/// The result of <c>tools/audit.js</c>: a static scan of the repositories of the apps (their layouts, stylesheets and scripts) against the
/// rules of the rulebook. It finds what is written in the files; it cannot tell how a page looks. The file is a snapshot (see <see cref="Generated"/>).
/// </summary>
public sealed class AuditData
{
    public AuditData(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "audit.json");
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<Document>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (doc is null)
        {
            return;
        }

        Generated = doc.Generated;
        Checks = doc.Checks;
        Apps = doc.Apps;
    }

    public string Generated { get; } = string.Empty;

    public IReadOnlyList<AuditCheck> Checks { get; } = [];

    public IReadOnlyList<AuditApp> Apps { get; } = [];

    public bool Available => Apps.Count > 0;

    public AuditResult? Result(string appId, string checkId)
        => Apps.FirstOrDefault(a => a.Id == appId)?.Results.GetValueOrDefault(checkId);

    private sealed record Document(string Generated, List<AuditCheck> Checks, List<AuditApp> Apps);
}
