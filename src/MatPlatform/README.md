# MatPlatform

The platform contract of the Mat apps ([docs/PLATFORM.md](../../docs/PLATFORM.md)) for .NET apps, as one package: the types of the **MatApiDefinition**, a
builder for it, the endpoints (`/api`, `/api/health`, `/api/state[/{key}]`, `GET`/`PUT /api/configure`, `POST /api/actions/{id}`), the check of `X-Api-Key` and of
MatIdentity's tokens, the checks of the settings, a validator of a definition for the app's tests and the sign-in through MatIdentity (OpenID Connect).
An app only **describes** itself and **supplies its values**.

Version **1.3.0** (`MatPlatformVersion` in `MatPlatform.csproj`, the same as `MatPlatformInfo.Version`; semantic versioning: a change an app has to react to is a major).
The first app on it is MatSplit (`src/MatSplit.Web/Platform/SplitPlatform.cs` there is the example to read).

## How an app gets it: a copy (CODE-03)

Like MatUi, an app **copies** the package; nothing is loaded from elsewhere and there is no NuGet package.

```
node tools/matplatform-copy.js <folder of the app's web project>      # or: ./tools/matplatform-copy.ps1 -Target <folder>
```

It writes every `.cs` file of `src/MatPlatform` to `<folder>/MatPlatform/` with the first line `// MatPlatform 1.3.0 – a copy from MatDesign, do not edit here`, and
`matplatform.version` (the version, then the SHA-256 of each file as copied, with LF line ends). Copying again updates the files and removes the ones the package no longer has.
**Never change the copy in the app**: change `src/MatPlatform` here (with a test in `tests/MatPlatform.Tests`), raise the version and copy it to the apps.

The app's project needs (the tool says so when they are missing; the files rely on `ImplicitUsings` like every Mat app):

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.13" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" Version="10.0.13" />
```

## The public API (namespace `MatPlatform`)

```csharp
// Program.cs
MatPlatformSettings builder.AddMatPlatform<TApp>(Action<MatPlatformBuilder> describe, Action<MatPlatformOptions>? configure = null) // TApp: the value interfaces it implements
MatPlatformSettings builder.AddMatPlatform(Action<MatPlatformBuilder> describe, Action<MatPlatformOptions>? configure = null)
IServiceCollection  services.AddMatPlatformValues<TApp>()
AuthenticationBuilder authentication.AddMatIdentitySignIn<TUsers>(MatPlatformSettings settings, Action<MatIdentitySignInOptions>? configure = null) // TUsers : IMatIdentityUsers
RouteGroupBuilder   app.MapMatPlatform()      // returns the protected group /api, for the app's own /api/provides/...

// What the app implements (each optional; one class may implement all four)
interface IMatPlatformState         { Task<IReadOnlyDictionary<string, MatStateValue>> GetStateAsync(MatPlatformCall call, CancellationToken ct); }
interface IMatPlatformConfiguration { Task<IReadOnlyDictionary<string, object?>> GetConfigurationAsync(MatPlatformCall call, CancellationToken ct);
                                      Task<IReadOnlyDictionary<string, string>> ConfigureAsync(IReadOnlyDictionary<string, JsonElement> values, MatPlatformCall call, CancellationToken ct); }
interface IMatPlatformActions       { Task<MatActionResult?> RunActionAsync(string id, MatPlatformCall call, CancellationToken ct); }
interface IMatPlatformHealth        { Task<IReadOnlyList<MatHealthCheck>> CheckHealthAsync(MatPlatformCall call, CancellationToken ct); }
interface IMatIdentityUsers         { Task<MatIdentitySignInResult> SignInAsync(MatIdentityPerson person, HttpContext http, CancellationToken ct); }

record MatPlatformCall(HttpContext Http, string Language, string Caller) { string T(string de, string en); }
record MatIdentityPerson(string Subject, string? Email, string? Name, string? PreferredUserName, string? Role, string? IdToken, ClaimsPrincipal Principal);
record MatIdentitySignInResult(bool Succeeded, string? Error) { string? LinkAccount; static Success(); static Refused(string error); static LinkRequired(string account); }
record MatIdentityPendingLink(string Subject, string? Email, string? Name, string? PreferredUserName, string? Role, string? IdToken, string Account)
    { static Store(HttpContext, MatIdentityPerson, string account); static Peek(HttpContext); static Take(HttpContext); static TakeFor(HttpContext, string account); static Clear(HttpContext);
      CookieName, Purpose, Lifetime (10 minutes) }

// The description (called at startup and for each GET /api with the caller's language)
MatPlatformBuilder: Language, T(de, en), App(id, name, description, version, icon?, accent?, home = "/"),
    Page(id, label, icon, params MatSection[]), Tile(MatStateItem) / Tile(key, label, widget = "number", icon?, unit?),
    Field(MatField) / Field(key, label, type = "text", required = false, hint?), Action(MatAction), LinkAction(id, label, icon, url),
    PostAction(id, label, icon, confirm?, danger = false), Role(key, label), Provides(capability, version = 1), Consumes(capability, version = 1, optional = false),
    static Build(describe, language = "de", auth?) -> MatApiDefinition
MatSection.Tiles(keys...), MatSection.Form(label?, keys...), MatSection.Actions(ids...), MatSection.Link(label, url); MatField.When((key, value)...) for showIf
MatPlatformOptions: DefaultLanguage = "de", CheckValues = true, AnonymousPermitPerMinute = 120, LanguageOf(http)
MatIdentitySignInOptions: CookieScheme, LoginPath = "/Account/Login", SignedOutRedirect (?signedOut=1), FailedRedirect (?local=1&ssoError=1), LinkRedirect (?local=1&link=1), AccessDeniedPath = "/AccessDenied", RoleClaim = "role", Scopes, TrustForwardedProto = true
MatForwardedProto.Apply(HttpContext), MatForwardedProto.IsHttps(header): the scheme from X-Forwarded-Proto (registered by AddMatIdentitySignIn, see below)
MatIdentitySignIn.Scheme ("MatIdentity"), MatIdentitySignIn.SignOutAtIdentity(HttpContext http, string idToken) -> SignOutResult
MatIdentitySignIn.CompleteSignInAsync(HttpContext http, MatIdentityPerson person, string? returnUrl) -> where the browser goes (what /signin-oidc does; for the apps' tests)
MatApiAccess (X-Api-Key in constant time, or a bearer token of MatIdentity for the audience = client id), MatApiAccess.BearerScheme, MatApiAccess.CallerOf(http)
MatPlatformSettings.From(configuration, appId): MAT_API_KEY, MATIDENTITY_AUTHORITY, MATIDENTITY_CLIENT_ID (default: the app id), MATIDENTITY_CLIENT_SECRET, MATIDENTITY_INTERNAL_URL
MatSmtpSettings.From(configuration): MATSMTP_HOST, MATSMTP_PORT (default 2025), MATSMTP_USERNAME, MATSMTP_PASSWORD, MATSMTP_FROM, MATSMTP_TLS (none|starttls|tls)
    IsConfigured (the host is set), HasCredentials, Host, Port, Username, Password, FromAddress, Tls (MatSmtpTls.None|StartTls|Tls), UseStartTls, UseTls, TlsName,
    Describe(language) -> the sentence for the fixed state (null when off), Fix(MatField, language) -> the field with fixed: true and that sentence as its hint (unchanged when off)
MatField.Fixed: set from outside the app; a consumer shows it without editing, PUT /api/configure refuses a change (MatValueChecker)
MatPlatformValidator.Validate(MatApiDefinition | string json, IEnumerable<string>? icons) -> problems; MatPlatformValidator.ReadSprite(path)
MatValueChecker.Check(definition, changes, current, language) -> errors per key
MatApiDefinition.Json: the serializer options of the contract (camelCase, nulls left out but a state value, letters such as ü not escaped)
```

What the endpoints do for the app: `/api` and `/api/health` are anonymous and rate limited (per address); `auth` and `endpoints` of the definition are filled in; `/api` has an `ETag`
and answers `304`; the health status is the worst check (`503` for `down`, a check that throws is `down`); the rest answers `401` without the key or a token; a field of the type
`secret` is always sent as `{"isSet": bool}`, whatever the app returns; `PUT /api/configure` checks the values against the definition (`CheckValues`), saves nothing when one is
wrong, and answers the values; an action that is not a `post` action of the definition is `404` without asking the app; texts follow `Accept-Language` (`de`, `en`).

**Behind a TLS proxy** (Matcad, whose address in Docker is not fixed): when MatIdentity is on, `AddMatIdentitySignIn` registers an `IStartupFilter` that puts a tiny middleware
at the very start of the pipeline: when the first value of `X-Forwarded-Proto` is `https` (any case), `Request.Scheme` becomes `https` (and `IsHttps` follows); any other value
changes nothing. Without it the app sees `http`, the `redirect_uri` is `http://.../signin-oidc` and the correlation and nonce cookies (`SameSite=None`) are not `Secure`, so the
browser drops them and the sign-in fails. **Only the scheme**: `RemoteIpAddress` (`X-Forwarded-For`, which guards the rate limits) and `Host` stay the app's own
ForwardedHeaders policy, and that policy, before or after it, comes to the same scheme. `X-Forwarded-Prefix` is not read: Matcad serves every app at the root of its own
address. `MatIdentitySignInOptions.TrustForwardedProto = false` turns it off.

## Mail through MatSMTP (1.3.0)

An app that sends mail reads the `MATSMTP_*` variables (docs/PLATFORM.md "Mail through MatSMTP") once at startup and, when they are set, sends **all** its mail through MatSMTP
and shows its own SMTP settings as fixed:

```csharp
var smtp = MatSmtpSettings.From(builder.Configuration);           // a singleton for the app's mail service
builder.Services.AddSingleton(smtp);

// the mail service (MailKit)
if (smtp.IsConfigured)
{
    await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseTls ? SecureSocketOptions.SslOnConnect : smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
    if (smtp.HasCredentials) { await client.AuthenticateAsync(smtp.Username, smtp.Password, ct); }
    message.From.Clear(); message.From.Add(MailboxAddress.Parse(smtp.FromAddress ?? ownSender));
}
else { /* the app's own SMTP settings, as before */ }

// the description of /api: the SMTP fields become fixed (hint = where the mail goes); PUT /api/configure refuses them
builder.AddMatPlatform<AppPlatform>(o => AppPlatform.Describe(o, smtp));
o.Field(smtp.Fix(new MatField("smtpHost", o.T("SMTP-Server", "SMTP server"), "text"), o.Language));

// the settings page: smtp.Describe(language) as the sentence, the fields disabled when smtp.IsConfigured
```

What the app stored for its own server stays stored and is used again when the variables are gone. `GET /api/configure` answers the values in force (MatSMTP's, the password as
`{"isSet": true}`).

## Adopting it in an app, step by step

1. **Copy** it: `node tools/matplatform-copy.js ../<App>/src/<App>.Web` and add the two package references.
2. **Describe** the app: a class in the app's `Platform/` folder (MatSplit: `SplitPlatform`) with `public static void Describe(MatPlatformBuilder o)`: `o.App(id, name, description,
   version, "/icon.svg", accent)` with the id and accent of `AppRegistry.cs`, the pages (`overview` with tiles and actions, `settings` with the few fields), the tiles, at most ten
   fields (only what a fresh install needs), the actions, the roles (the most powerful first) and `Provides` / `Consumes`. Texts with `o.T("Deutsch", "English")`.
3. **Supply the values**: the same class implements `IMatPlatformState`, `IMatPlatformConfiguration` (a secret as `MatSecret.Of(value)`; write each change to the app's
   activity log with `call.Caller`), `IMatPlatformActions` and `IMatPlatformHealth` by calling the app's **services** (no `DbContext` or SQL in this class: it is the web layer).
4. **Register** in `Program.cs`:
   ```csharp
   var platform = builder.AddMatPlatform<AppPlatform>(AppPlatform.Describe);      // options => options.CheckValues = false when the app checks its settings itself
   builder.Services.AddAuthentication(CookieScheme).AddCookie(CookieScheme, ...)
       .AddMatIdentitySignIn<AppIdentityUsers>(platform, o => { o.CookieScheme = CookieScheme; o.LoginPath = "/Account/Login"; });
   ...
   app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
   app.MapMatPlatform();
   ```
5. **Sign-in**: implement `IMatIdentityUsers` (find the local user by `Subject`; else, when exactly one active account that is not linked yet has the same `Email` (or user name),
   answer `MatIdentitySignInResult.LinkRequired(<its id>)` and **do not link it**; else create the user; follow `Role` when it is set; sign in with the app's own cookie and
   keep `IdToken` with the session; refuse a disabled or deleted user, several matches or a match linked to another subject). **Linking needs proof of both identities**: the
   address in the app proves nothing (anyone may have typed it into their own profile, or registered with it as a user name), so a person who could link by it would be put into
   an account whose password someone else knows. On `LinkRequired` the package keeps a `MatIdentityPendingLink` (the claims and the ID token, Data Protection, HttpOnly,
   Secure on https, SameSite=Lax, 10 minutes, the path of the app) and sends the browser to `LoginPath?local=1&link=1`. The sign-in page says so when `MatIdentityPendingLink.Peek`
   finds one (German and English: an account with this address exists here; sign in once with its password to connect it with MatIdentity) and offers to cancel
   (`MatIdentityPendingLink.Clear`). When the local sign-in has succeeded (after the app's second factor, if it has one), `MatIdentityPendingLink.TakeFor(http, <the id of the
   user>)` gives the link only for the account that matched (and deletes it either way): set the external subject, apply the role claim like a MatIdentity sign-in, keep its
   `IdToken` with the session, write "linked with MatIdentity" to the activity log. Another account signs in normally and links nothing. The sign-in page challenges `MatIdentitySignIn.Scheme` when `platform.IdentityEnabled`
   and none of `?local=1`, `?signedOut=1`, `?ssoError=1` is set (and shows the local form when the challenge throws because MatIdentity cannot be reached); the sign-out page
   returns `MatIdentitySignIn.SignOutAtIdentity(HttpContext, idToken)` after the local sign-out when the session has an ID token. A column for the subject (`ExternalSubject`)
   needs a migration or a step of the app's schema upgrade.
6. **Tests** (`dotnet test`, the app in memory with `WebApplicationFactory`): `Assert.Empty(MatPlatformValidator.Validate(MatPlatformBuilder.Build(AppPlatform.Describe, "de"),
   MatPlatformValidator.ReadSprite(<the app's matui-icons.svg>)))` for `de` and `en`; the endpoints (shapes, `401` without key, a secret never sent back, `404` for an unknown
   key or action); the sign-in with and without `MATIDENTITY_AUTHORITY` (a fixed `OpenIdConnectConfiguration` through `PostConfigure<OpenIdConnectOptions>(MatIdentitySignIn.Scheme, ...)`
   and `PostConfigure<JwtBearerOptions>(MatApiAccess.BearerScheme, ...)`, so no MatIdentity has to run); the pending link: `MatIdentitySignIn.CompleteSignInAsync` with a person whose
   address an account pre-claimed lands on `?local=1&link=1` without entering it, the local sign-in of that account links it, another account does not, cancel, an expired or
   forged cookie is ignored (`MatIdentityPendingLink.Purpose` makes an expired one). MatSplit's `tests/MatSplit.Tests` has all of these.
7. **Docker**: `HEALTHCHECK ... CMD curl -fsS http://127.0.0.1:<port>/api/health || exit 1` (200 for `ok` and `degraded`, 503 for `down`; a degraded app is not restarted).
8. **README**: the settings table gets `MAT_API_KEY`, `MATIDENTITY_AUTHORITY`, `MATIDENTITY_CLIENT_ID` (default: the app id), `MATIDENTITY_CLIENT_SECRET`, `MATIDENTITY_INTERNAL_URL`;
   a section on the platform API (the endpoints and what the tiles, fields and actions are) and on the sign-in through MatIdentity (`?local=1`, redirect address `/signin-oidc`,
   signed-out address `/signout-callback-oidc`).
9. **Release `docker-compose.yml`**: the same variables as comments, none of them required:
   ```yaml
   # MAT_API_KEY: ""                    # the key of /api/* (Mattiverse sets it)
   # MATIDENTITY_AUTHORITY: ""          # https://id.example.com: sign-in through MatIdentity
   # MATIDENTITY_CLIENT_ID: "<app id>"
   # MATIDENTITY_CLIENT_SECRET: ""
   # MATIDENTITY_INTERNAL_URL: ""       # http://matidentity:2300 inside Docker
   ```
10. The Mattiverse catalog entry (`platform: true`, `identity: {}`, and `mail: {}` for an app that sends mail), see docs/PLATFORM.md "What an app has to do".
11. **Mail** (an app that sends any): `MatSmtpSettings` as in "Mail through MatSMTP" above, the six `MATSMTP_*` variables in the README's settings table and as comments in the
    release `docker-compose.yml`, and tests that the SMTP fields are fixed with the variables and editable without them.

## Changing the package

Every change has a test in `tests/MatPlatform.Tests` (the demo app in `DemoApp.cs` runs in memory); `dotnet test` in MatDesign runs them. Raise `MatPlatformVersion` and
`MatPlatformInfo.Version` together (a test holds them equal), then copy to the apps and run their tests. The JSON is the contract: Mattiverse (`PlatformClient`) and MatIdentity
(`AppRolesReader`) read it, so a change of a name or a shape raises the contract version of docs/PLATFORM.md.
