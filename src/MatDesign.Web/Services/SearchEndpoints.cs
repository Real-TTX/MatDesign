using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MatDesign.Web.Services;

/// <summary>
/// The addresses of the search: <c>GET /api/search?q=&amp;limit=&amp;kind=</c> answers what the search of the toolkit asks for (<c>{ groups: [ { name, items: [ { title, text, href, icon, kind, id } ] } ] }</c>), and
/// <c>GET /api/demo/search</c> is the stateless answer the examples of the catalogue use. Both are public and read-only, JSON only; nothing of the query is written anywhere and every text in the
/// answer is data that the script puts into the page as text, never as markup.
/// </summary>
public static class SearchEndpoints
{
    public const int DefaultLimit = 8;

    // What goes in is a text a visitor typed: the default encoder writes < > & and ' as \uXXXX, so the answer can never be taken for HTML, whatever a browser sniffs (and nosniff says it must not).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.Default };

    private static IResult Answer(HttpContext http, object body, int status = StatusCodes.Status200OK)
    {
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Text(JsonSerializer.Serialize(body, Json), "application/json; charset=utf-8", statusCode: status);
    }

    public static void MapSearch(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/search", (SearchIndex index, HttpContext http) =>
        {
            var query = http.Request.Query["q"].ToString();
            var limit = int.TryParse(http.Request.Query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) ? Math.Clamp(asked, 1, 50) : DefaultLimit;
            SearchKind? kind = null;
            var kindText = http.Request.Query["kind"].ToString();
            if (kindText.Length > 0)
            {
                if (!SearchKinds.TryParse(kindText, out var parsed))
                {
                    return Answer(http, new { error = "kind is one of " + string.Join(", ", SearchKinds.All.Select(SearchKinds.Name)) + "." }, StatusCodes.Status400BadRequest);
                }

                kind = parsed;
            }

            var normalised = SearchIndex.Normalise(query);
            // the answer for a version of the data and a query is the same: the browser asks again only to learn that
            var etag = $"\"{index.Version:x}-{ToolkitFiles.ShortHash(SearchText.Fold(normalised) + "|" + limit + "|" + kind)}\"";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers.ETag = etag;
            if (http.Request.Headers.IfNoneMatch == etag)
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            var results = index.Query(normalised);
            return Answer(http, new
            {
                query = normalised,
                total = kind is { } k ? results.CountOf(k) : results.Total,
                groups = results.Top(limit, kind).Select(g => new
                {
                    name = g.Name,
                    kind = SearchKinds.Name(g.Kind),
                    count = g.Count,
                    items = g.Hits.Select(h => new
                    {
                        title = h.Doc.Title,
                        text = h.Doc.Text,
                        href = h.Doc.Href,
                        icon = h.Doc.Icon,
                        kind = SearchKinds.Name(h.Doc.Kind),
                        id = h.Doc.Id,
                    }),
                }),
            });
        });

        // ---- the demo of the catalogue: documents, people and settings, found by every word of the text; "slow" waits a moment (the loading row), "error" fails (the failure row)
        api.MapGet("/demo/search", async (HttpContext http) =>
        {
            var text = SearchIndex.Normalise(http.Request.Query["q"].ToString());
            var limit = int.TryParse(http.Request.Query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) ? Math.Clamp(asked, 1, 50) : DefaultLimit;
            var terms = SearchText.Terms(text).ToList();
            if (terms.Contains("error"))
            {
                return Answer(http, new { error = "The demo fails on purpose when the text has the word error." }, StatusCodes.Status500InternalServerError);
            }

            if (terms.Remove("slow"))
            {
                await Task.Delay(1400, http.RequestAborted);
            }

            var hits = DemoSearch.Items
                .Select(item => (Item: item, Fold: SearchText.Fold(item.Title + " " + item.Text)))
                .Where(x => terms.All(t => x.Fold.Contains(t, StringComparison.Ordinal)))
                .Select(x => (x.Item, Score: terms.Sum(t => SearchText.Fold(x.Item.Title).StartsWith(t, StringComparison.Ordinal) ? 3 : SearchText.Fold(x.Item.Title).Contains(t, StringComparison.Ordinal) ? 2 : 1)))
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
            return Answer(http, new
            {
                groups = hits.GroupBy(x => x.Item.Group).Select(g => new
                {
                    name = g.Key,
                    items = g.Select(x => new { title = x.Item.Title, text = x.Item.Text, href = "#sr-remote", icon = x.Item.Icon, kind = x.Item.Kind, id = x.Item.Id }),
                }),
            });
        });
    }
}

/// <summary>The invented documents, people and settings that <c>/api/demo/search</c> answers with.</summary>
internal static class DemoSearch
{
    internal sealed record Item(string Group, string Id, string Title, string Text, string Icon, string Kind);

    public static IReadOnlyList<Item> Items { get; } =
    [
        new("Documents", "d1", "Quarterly report", "Finance, 12 pages, changed yesterday", "file-text", "document"),
        new("Documents", "d2", "Annual report 2025", "Finance, 48 pages, approved", "file-text", "document"),
        new("Documents", "d3", "Invoice 2026-04", "Sales, 1 page, paid", "file-text", "document"),
        new("Documents", "d4", "Rental contract", "Legal, 9 pages, signed", "file-text", "document"),
        new("Documents", "d5", "Meeting notes: planning", "Team, 2 pages, shared with 4 people", "file-text", "document"),
        new("Documents", "d6", "Travel expenses report", "Finance, 3 pages, waiting for a receipt", "file-text", "document"),
        new("People", "p1", "Alex Morgan", "Finance, alex@example.org", "user", "person"),
        new("People", "p2", "Sam Rivera", "Legal, sam@example.org", "user", "person"),
        new("People", "p3", "Jordan Lee", "Sales, jordan@example.org", "user", "person"),
        new("People", "p4", "Riley Chen", "Team lead, riley@example.org", "user", "person"),
        new("People", "p5", "Reporting team", "Group of 5 people", "users", "group"),
        new("Settings", "s1", "Report settings", "What a report contains and who gets it", "settings", "setting"),
        new("Settings", "s2", "Notifications", "What is sent to you, and when", "bell", "setting"),
        new("Settings", "s3", "Account", "Name, password and sign-in", "key", "setting"),
        new("Settings", "s4", "Appearance", "Theme and accent colour", "palette", "setting"),
        new("Settings", "s5", "Archive", "How long documents are kept", "archive", "setting"),
    ];
}
