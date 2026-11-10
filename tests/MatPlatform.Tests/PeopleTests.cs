using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MatPlatform.Tests;

/// <summary>2.4.0: the people of an app (docs/PLATFORM.md "People, permissions and quotas"): the definition, the claims, MatAccess, /api/quotas and MatPeople.</summary>
public sealed class PeopleTests
{
    private static void Describe(MatPlatformBuilder o)
    {
        DemoApp.Describe(o);
        o.Permission("share-public", o.T("Öffentlich teilen", "Share publicly"), roles: "admin")
            .Permission("export", "Export")
            .Quota("storage", o.T("Speicher", "Storage"), MatQuotaUnits.Bytes)
            .Quota("ai-tokens", "AI tokens", MatQuotaUnits.Tokens, MatQuotaUnits.Month, max: 1_000_000_000)
            .Launcher("demo", "Demo", "/icon.svg", "/", audience: MatAudience.Admin, roles: ["admin"]);
    }

    // ---- the definition --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_definition_has_permissions_and_quotas_and_is_valid()
    {
        var definition = MatPlatformBuilder.Build(Describe, "en");
        Assert.Empty(MatPlatformValidator.Validate(definition));

        var json = JsonSerializer.Serialize(definition, MatApiDefinition.Json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("share-public", root.GetProperty("permissions")[0].GetProperty("key").GetString());
        Assert.Equal("admin", root.GetProperty("permissions")[0].GetProperty("roles")[0].GetString());
        Assert.False(root.GetProperty("permissions")[1].TryGetProperty("roles", out _));
        Assert.Equal("bytes", root.GetProperty("quotas")[0].GetProperty("unit").GetString());
        Assert.Equal(0, root.GetProperty("quotas")[0].GetProperty("min").GetInt64());
        Assert.Equal("month", root.GetProperty("quotas")[1].GetProperty("period").GetString());
        Assert.Equal("admin", root.GetProperty("contributes").GetProperty("launchers")[0].GetProperty("roles")[0].GetString());
        Assert.Contains("/api/quotas", root.GetProperty("endpoints").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Without_them_the_definition_has_no_such_members()
    {
        var json = JsonSerializer.Serialize(MatPlatformBuilder.Build(DemoApp.Describe), MatApiDefinition.Json);
        Assert.DoesNotContain("\"permissions\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"quotas\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Share", "permission \"Share\"")]
    [InlineData("-share", "permission \"-share\"")]
    public void A_permission_key_is_a_role_key(string key, string expected)
        => Assert.Contains(MatPlatformValidator.Validate(MatPlatformBuilder.Build(o => { DemoApp.Describe(o); o.Permission(key, "x"); })), p => p.StartsWith(expected, StringComparison.Ordinal));

    [Fact]
    public void The_validator_names_every_problem_of_permissions_and_quotas()
    {
        var problems = MatPlatformValidator.Validate(MatPlatformBuilder.Build(o =>
        {
            DemoApp.Describe(o);
            o.Permission("export", "Export", roles: "owner")
                .Permission("export", "Again")
                .Permission("empty", "")
                .Quota("storage", "Storage", "gigabytes")
                .Quota("calls", "Calls", MatQuotaUnits.Requests, "week")
                .Quota("files", "Files", MatQuotaUnits.Count, min: 10, max: 5)
                .Quota("Big", "Big", MatQuotaUnits.Count)
                .Launcher("demo", "Demo", "/icon.svg", "/", roles: ["owner"]);
        }));

        Assert.Contains(problems, p => p.Contains("permission \"export\": \"owner\" is not a role", StringComparison.Ordinal));
        Assert.Contains("permissions: \"export\" is there 2 times.", problems);
        Assert.Contains(problems, p => p.Contains("permission \"empty\": the label", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("quota \"storage\": the unit \"gigabytes\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("quota \"calls\": the period \"week\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("quota \"files\": min", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("quota \"Big\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("launcher demo: \"owner\" is not a role", StringComparison.Ordinal));
    }

    // ---- the claims ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_permissions_come_from_one_claim_per_value_or_a_json_array()
    {
        var principal = Principal(
            new Claim(MatAccessClaims.Entitlements, "export"),
            new Claim(MatAccessClaims.Entitlements, "share-public"),
            new Claim(MatAccessClaims.Entitlements, """["export","drives-create","Bad Key",5]"""),
            new Claim(MatAccessClaims.Entitlements, "<script>"));

        Assert.Equal(["export", "share-public", "drives-create"], MatAccessClaims.PermissionsOf(principal));
    }

    [Fact]
    public void The_quotas_come_from_a_json_object_and_keep_no_limit_apart_from_not_set()
    {
        var principal = Principal(new Claim(MatAccessClaims.Quotas, """{"storage": 53687091200, "ai-tokens": null, "files": -1, "name": "x", "Big": 4, "half": 1.5}""", "JSON"));

        var quotas = MatAccessClaims.QuotasOf(principal);

        Assert.Equal(2, quotas.Count);
        Assert.Equal(53687091200, quotas["storage"]);
        Assert.True(quotas.ContainsKey("ai-tokens"));
        Assert.Null(quotas["ai-tokens"]);
        Assert.Empty(MatAccessClaims.QuotasOf(Principal(new Claim(MatAccessClaims.Quotas, "[1,2]"))));
        Assert.Empty(MatAccessClaims.QuotasOf(Principal(new Claim(MatAccessClaims.Quotas, "not json"))));
    }

    // ---- MatAccess -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MatAccess_answers_with_the_values_of_matidentity_and_falls_back_to_the_app()
    {
        var access = new MatAccess(Options(null));
        await access.RememberAsync(Person("sub-anna", "user", ["export"], new() { ["storage"] = 1000, ["ai-tokens"] = null }));
        await access.RememberAsync(Person("sub-ben", null, null, null));

        Assert.True(await access.HasAsync("sub-anna", "export", fallback: false));
        Assert.False(await access.HasAsync("sub-anna", "share-public", fallback: true));
        Assert.Equal(1000, await access.LimitAsync("sub-anna", "storage", ownLimit: 5));
        Assert.Null(await access.LimitAsync("sub-anna", "ai-tokens", ownLimit: 5));
        Assert.Equal(5, await access.LimitAsync("sub-anna", "files", ownLimit: 5));
        Assert.True(await access.IsFixedAsync("sub-anna", "ai-tokens"));
        Assert.False(await access.IsFixedAsync("sub-anna", "files"));

        // MatIdentity does not decide the roles for Ben (no role): the app's own rules count.
        Assert.True(await access.HasAsync("sub-ben", "export", fallback: true));
        Assert.Equal(7, await access.LimitAsync("sub-ben", "storage", ownLimit: 7));
        Assert.False(await access.IsDecidedAsync("sub-ben"));

        // Nobody signed in: the app's own rules.
        Assert.True(await access.HasAsync("sub-nobody", "export", fallback: true));
        Assert.Equal(9, await access.LimitAsync("sub-nobody", "storage", ownLimit: 9));
    }

    [Fact]
    public async Task MatAccess_keeps_the_people_in_the_data_folder_and_a_new_sign_in_replaces_them()
    {
        var folder = Directory.CreateTempSubdirectory("matplatform-people-");
        try
        {
            var first = new MatAccess(Options(folder.FullName));
            await first.RememberAsync(Person("sub-anna", "user", ["export"], new() { ["storage"] = 1000 }));
            Assert.True(File.Exists(Path.Combine(folder.FullName, "matplatform", MatAccess.FileName)));

            var second = new MatAccess(Options(folder.FullName));
            Assert.Equal(1000, await second.LimitAsync("sub-anna", "storage", null));
            Assert.True(await second.HasAsync("sub-anna", "export", false));

            await second.RememberAsync(Person("sub-anna", "admin", [], new()));
            Assert.False(await second.HasAsync("sub-anna", "export", true));
            Assert.Null(await second.LimitAsync("sub-anna", "storage", null));
            Assert.Equal("admin", (await new MatAccess(Options(folder.FullName)).GetAsync("sub-anna"))?.Role);

            await second.ForgetAsync("sub-anna");
            Assert.Null(await new MatAccess(Options(folder.FullName)).GetAsync("sub-anna"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_an_error_not_an_empty_list()
    {
        var folder = Directory.CreateTempSubdirectory("matplatform-people-");
        try
        {
            Directory.CreateDirectory(Path.Combine(folder.FullName, "matplatform"));
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, "matplatform", MatAccess.FileName), "{ broken");
            await Assert.ThrowsAnyAsync<JsonException>(() => new MatAccess(Options(folder.FullName)).GetAsync("sub-anna"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_sign_in_keeps_the_values_before_the_app_signs_the_person_in()
    {
        var store = new DemoStore();
        bool? seenByTheApp = null;
        await using var app = await DemoHost.StartAsync(store: store);
        var access = app.Services.GetRequiredService<MatAccess>();
        store.SignInAnswer = person =>
        {
            seenByTheApp = access.HasAsync(person.Subject, "export", fallback: false).GetAwaiter().GetResult();
            return MatIdentitySignInResult.Success();
        };

        var http = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        await MatIdentitySignIn.CompleteSignInAsync(http, Person("sub-anna", "member", ["export"], new() { ["storage"] = 1 }));

        Assert.True(seenByTheApp);
        Assert.Equal(1, await access.LimitAsync("sub-anna", "storage", null));
    }

    // ---- GET /api/quotas/{subject} ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_usage_of_a_person_with_the_limit_matidentity_set()
    {
        await using var app = await StartWithQuotas();
        await app.Services.GetRequiredService<MatAccess>().RememberAsync(Person("sub-anna", "member", [], new() { ["storage"] = 50, ["ai-tokens"] = null }));

        var answer = await app.Api().GetFromJsonAsync<JsonElement>("/api/quotas/sub-anna");

        Assert.Equal("sub-anna", answer.GetProperty("subject").GetString());
        var storage = answer.GetProperty("values").GetProperty("storage");
        Assert.Equal(12, storage.GetProperty("used").GetInt64());
        Assert.Equal(50, storage.GetProperty("limit").GetInt64());
        Assert.True(storage.GetProperty("fixed").GetBoolean());
        var tokens = answer.GetProperty("values").GetProperty("ai-tokens");
        Assert.Equal(JsonValueKind.Null, tokens.GetProperty("limit").ValueKind);
        Assert.True(tokens.GetProperty("fixed").GetBoolean());
        Assert.Equal("month", tokens.GetProperty("period").GetString());
    }

    [Fact]
    public async Task Without_matidentity_the_limit_is_the_apps_own()
    {
        await using var app = await StartWithQuotas();

        var answer = await app.Api().GetFromJsonAsync<JsonElement>("/api/quotas/sub-ben");

        var storage = answer.GetProperty("values").GetProperty("storage");
        Assert.Equal(100, storage.GetProperty("limit").GetInt64());
        Assert.False(storage.GetProperty("fixed").GetBoolean());
        // A quota the app did not answer counts as nothing used and no limit.
        var tokens = answer.GetProperty("values").GetProperty("ai-tokens");
        Assert.Equal(0, tokens.GetProperty("used").GetInt64());
        Assert.Equal(JsonValueKind.Null, tokens.GetProperty("limit").ValueKind);
    }

    [Fact]
    public async Task An_unknown_person_is_404_and_no_key_is_401()
    {
        await using var app = await StartWithQuotas();

        Assert.Equal(HttpStatusCode.NotFound, (await app.Api().GetAsync("/api/quotas/sub-nobody")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetTestClient().GetAsync("/api/quotas/sub-anna")).StatusCode);
    }

    [Fact]
    public async Task An_app_without_quotas_answers_404()
    {
        await using var app = await DemoHost.StartAsync(services: s => s.AddScoped<IMatPlatformQuotas, DemoQuotas>());
        Assert.Equal(HttpStatusCode.NotFound, (await app.Api().GetAsync("/api/quotas/sub-anna")).StatusCode);
    }

    [Fact]
    public async Task A_machine_token_may_read_the_usage_a_persons_token_may_not()
    {
        await using var app = await StartWithQuotas(DemoHost.Authority);

        var machine = Bearer(app, Token(subject: "mattiverse", client: "mattiverse"));
        Assert.Equal(HttpStatusCode.OK, (await machine.GetAsync("/api/quotas/sub-anna")).StatusCode);

        // A person's token for this app (its own client): it reads the tiles, but never anyone's usage.
        var person = Bearer(app, Token(subject: "sub-ben", client: "demoapp"));
        Assert.Equal(HttpStatusCode.OK, (await person.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/quotas/sub-anna")).StatusCode);

        // A token without a client says nothing about a machine.
        var unknown = Bearer(app, Token(subject: "mattiverse", client: null));
        Assert.Equal(HttpStatusCode.Forbidden, (await unknown.GetAsync("/api/quotas/sub-anna")).StatusCode);
    }

    // ---- MatPeople (people.scim) -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MatPeople_reads_a_person_with_their_values_in_this_app()
    {
        var scim = new ScimHandler();
        await using var app = await StartWithPeople(scim);
        var people = app.Services.GetRequiredService<MatPeople>();

        var anna = await people.FindAsync("sub-anna");

        Assert.NotNull(anna);
        Assert.Equal("anna", anna.UserName);
        Assert.Equal("Anna Berg", anna.Name);
        Assert.Equal("anna@example.com", anna.Email);
        Assert.True(anna.Active);
        Assert.Equal("member", anna.Role);
        Assert.Equal(["export"], anna.Permissions);
        Assert.Equal(50, anna.Quotas["storage"]);
        Assert.Null(anna.Quotas["ai-tokens"]);
        Assert.Equal(["Team"], anna.Groups);

        var request = Assert.Single(scim.Requests);
        Assert.Equal("http://identity.internal/scim/v2/Users/sub-anna", request.Url);
        Assert.Equal("Bearer mat_people_key", request.Authorization);
    }

    [Fact]
    public async Task MatPeople_reads_every_page_and_escapes_a_filter()
    {
        var scim = new ScimHandler();
        await using var app = await StartWithPeople(scim);
        var people = app.Services.GetRequiredService<MatPeople>();

        var all = await people.ListAsync();
        Assert.Equal(["sub-anna", "sub-ben", "sub-cleo"], all.Select(p => p.Subject));
        Assert.Contains(scim.Requests, r => r.Url.Contains("startIndex=3", StringComparison.Ordinal));

        await people.FindByEmailAsync("x\"y@example.com");
        Assert.Contains(scim.Requests, r => r.Url.Contains(Uri.EscapeDataString("emails.value eq \"x\\\"y@example.com\""), StringComparison.Ordinal));

        Assert.Null(await people.FindAsync("sub-nobody"));
        var groups = await people.GroupsAsync();
        Assert.Equal("Team", Assert.Single(groups).Name);
    }

    [Fact]
    public async Task Without_the_binding_matpeople_answers_nothing_and_calls_nobody()
    {
        var scim = new ScimHandler();
        await using var app = await DemoHost.StartAsync(services: s => s.AddHttpClient(MatPeople.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => scim));
        var people = app.Services.GetRequiredService<MatPeople>();

        Assert.False(await people.IsConnectedAsync());
        Assert.Null(await people.FindAsync("sub-anna"));
        Assert.Empty(await people.ListAsync());
        Assert.Empty(scim.Requests);
    }

    [Fact]
    public void A_person_of_another_app_has_no_values_here()
    {
        using var document = JsonDocument.Parse(ScimHandler.User("sub-anna", "anna", app: "matdrive"));
        var person = MatPeople.PersonOf(document.RootElement, "demoapp");
        Assert.NotNull(person);
        Assert.Null(person.Role);
        Assert.Empty(person.Permissions);
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------------------------

    private static Task<WebApplication> StartWithQuotas(string? authority = null)
        => DemoHost.StartAsync(authority: authority, describe: Describe, services: s => s.AddScoped<IMatPlatformQuotas, DemoQuotas>());

    private static Task<WebApplication> StartWithPeople(ScimHandler scim)
        => DemoHost.StartAsync(
            describe: Describe,
            configuration: new Dictionary<string, string?>
            {
                ["MAT_PEOPLE_SCIM_URL"] = "https://id.example.com",
                ["MAT_PEOPLE_SCIM_INTERNAL_URL"] = "http://identity.internal/",
                ["MAT_PEOPLE_SCIM_KEY"] = "mat_people_key",
            },
            services: s => s.AddHttpClient(MatPeople.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => scim));

    private static MatPlatformOptions Options(string? folder)
    {
        var options = (MatPlatformOptions)Activator.CreateInstance(typeof(MatPlatformOptions), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, [(Action<MatPlatformBuilder>)DemoApp.Describe], null)!;
        options.DataDirectory = folder;
        return options;
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    private static MatIdentityPerson Person(string subject, string? role, IReadOnlyList<string>? permissions, Dictionary<string, long?>? quotas)
        => new(subject, subject + "@example.com", subject, subject, role, "the.id.token", new ClaimsPrincipal())
        {
            Permissions = permissions,
            Quotas = quotas,
        };

    private static HttpClient Bearer(WebApplication app, string token)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>An access token of MatIdentity for the demo app: a machine's (its subject is its client id) or a person's.</summary>
    private static string Token(string subject, string? client)
    {
        var claims = new Dictionary<string, object> { ["sub"] = subject };
        if (client is not null)
        {
            claims["client_id"] = client;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = DemoHost.Authority,
            Audience = "demoapp",
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = DemoHost.Credentials,
        });
    }

    /// <summary>The demo's usage: Anna uses 12 bytes of 100 (the app's own limit), Ben nothing; nobody else is known.</summary>
    private sealed class DemoQuotas : IMatPlatformQuotas
    {
        public Task<IReadOnlyDictionary<string, MatQuotaUsage>?> GetUsageAsync(string subject, MatPlatformCall call, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, MatQuotaUsage>?>(subject switch
            {
                "sub-anna" => new Dictionary<string, MatQuotaUsage> { ["storage"] = new(12, 100) },
                "sub-ben" => new Dictionary<string, MatQuotaUsage> { ["storage"] = new(0, 100) },
                _ => null,
            });
    }

    /// <summary>MatIdentity's SCIM endpoint as the tests need it: three people on two pages, one group, 404 for anyone else.</summary>
    private sealed class ScimHandler : HttpMessageHandler
    {
        public List<(string Url, string? Authorization)> Requests { get; } = [];

        public static string User(string id, string userName, string app = "demoapp") => $$"""
            { "schemas": ["urn:ietf:params:scim:schemas:core:2.0:User", "{{MatPeople.AccessSchema}}"],
              "id": "{{id}}", "userName": "{{userName}}", "displayName": "Anna Berg", "active": true,
              "emails": [ { "value": "other@example.com" }, { "value": "{{userName}}@example.com", "primary": true } ],
              "groups": [ { "value": "g1", "display": "Team" } ],
              "{{MatPeople.AccessSchema}}": { "access": [ { "app": "{{app}}", "role": "member", "permissions": ["export"], "quotas": { "storage": 50, "ai-tokens": null }, "via": ["person"] } ] } }
            """;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add((url, request.Headers.Authorization?.ToString()));
            var path = request.RequestUri.AbsolutePath;
            var query = request.RequestUri.Query;

            string? body = path switch
            {
                "/scim/v2/Users/sub-anna" => User("sub-anna", "anna"),
                "/scim/v2/Users" when query.Contains("filter", StringComparison.Ordinal) => List(0, 0),
                "/scim/v2/Users" when query.Contains("startIndex=1", StringComparison.Ordinal) => List(1, 3, User("sub-anna", "anna"), User("sub-ben", "ben")),
                "/scim/v2/Users" when query.Contains("startIndex=3", StringComparison.Ordinal) => List(3, 3, User("sub-cleo", "cleo")),
                "/scim/v2/Groups" => List(1, 1, """{ "id": "g1", "displayName": "Team", "members": [ { "value": "sub-anna" } ] }"""),
                _ => null,
            };

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"status":"404"}""", Encoding.UTF8, "application/scim+json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/scim+json") });
        }

        private static string List(int start, int total, params string[] resources)
            => $$"""{ "schemas": ["urn:ietf:params:scim:api:messages:2.0:ListResponse"], "totalResults": {{total}}, "startIndex": {{start}}, "itemsPerPage": {{resources.Length}}, "Resources": [ {{string.Join(",", resources)}} ] }""";
    }
}
