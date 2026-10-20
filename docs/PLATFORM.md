# The platform contract of the Mat apps

What every app of the family offers besides its own pages, so that **Mattiverse** (and any other app: Matmon, a dashboard, the next one) can show it, watch it,
set it up and act on it **without knowing anything about it beforehand**, so that the apps can find and call each other, and so that one sign-in (**MatIdentity**)
works everywhere. The look is MatUi ([ADOPTING.md](ADOPTING.md)); this is the rest.

Contract version: **1**. A change that breaks a consumer raises it; adding a field does not.

## The idea in one paragraph

An app **describes itself** at `GET /api`: the **MatApiDefinition**. It says which pages a consumer should build (a menu entry each), what is on them
(tiles, a form, a row of buttons), what each tile, field and action is (label, type, icon, limits, when a field is shown) and what the app offers to other apps.
The definition changes only with the app's version. The **values** come from small endpoints that a consumer calls as often as it likes. A consumer draws everything
with MatUi, so an app looks the same inside Mattiverse as on its own.

## Overview

| Endpoint | Purpose | Access |
|---|---|---|
| `GET /api` | the **MatApiDefinition**: who the app is, pages, tiles, fields, actions, what it provides | anonymous |
| `GET /api/health` | is the app well: `ok`, `degraded`, `down` | anonymous |
| `GET /api/state`, `GET /api/state/{key}` | the values of the tiles | key or token |
| `GET /api/configure`, `PUT /api/configure` | the values of the **few** settings | key or token |
| `POST /api/actions/{id}` | run an action | key or token |
| `/api/provides/{capability}` | what the app does for other apps (see "Apps talking to each other") | token |

- Everything starts with **`/api/`**. An app's own API stays where it is (the engines' `/api/v1/...`, see Mattiverse `docs/ENGINES.md`); the contract lives next to it and
  never under a version segment.
- JSON, camelCase, `Content-Type: application/json`, UTF-8. Times are ISO 8601 with offset (`2026-10-15T09:30:00+02:00`).
- **A consumer ignores what it does not know** (a field, a widget, a section kind) and draws the rest. An app may add fields of its own to every answer.
- Errors: `400` with `{"errors": {"<key>": "<message>"}}` for a wrong input, `401` without valid credentials, `403` when the caller may not, `404` for an unknown key or action,
  `409` when an action cannot run now. A message is for a person.
- Texts (labels, hints, questions) are in the language the caller asks for with `Accept-Language` (`de`, `en`), else in the app's language.

## Access

- **API key:** the header **`X-Api-Key`** (as the engines already do), compared in constant time with **`MAT_API_KEY`** from the environment.
  No `MAT_API_KEY` set = the key is off, only a token is accepted. Mattiverse generates the key when it installs an app and writes it into the app's `.env`.
- **Token:** `Authorization: Bearer <access token>` issued by MatIdentity for the audience of this app (see "Sign-in with MatIdentity").
- `/api` and `/api/health` answer without either. They never contain a secret, a user name, a value of a setting or a path of the host.
- Rate-limit the anonymous endpoints.

## `GET /api`: the MatApiDefinition

```json
{
  "contract": 1,
  "app": {
    "id": "matsplit",
    "name": "MatSplit",
    "description": "Split costs in groups",
    "version": "1.4.27-20261015",
    "icon": "/icon.svg",
    "accent": "#2f6feb",
    "home": "/"
  },
  "auth": { "apiKey": true, "identity": "https://id.example.com" },

  "pages": [
    {
      "id": "overview", "label": "Overview", "icon": "dashboard",
      "sections": [
        { "kind": "tiles", "items": ["groups", "openBalance", "queue", "disk"] },
        { "kind": "actions", "items": ["openAdmin", "refresh", "clearCache"] }
      ]
    },
    {
      "id": "settings", "label": "Settings", "icon": "settings",
      "sections": [
        { "kind": "form", "label": "General", "items": ["publicUrl", "currency", "registration"] },
        { "kind": "form", "label": "Mail", "items": ["smtpEnabled", "smtpHost", "smtpPassword"] }
      ]
    }
  ],

  "state": [
    { "key": "groups", "label": "Groups", "widget": "number", "icon": "users" },
    { "key": "openBalance", "label": "Open balance", "widget": "number", "unit": "EUR", "decimals": 2 },
    { "key": "queue", "label": "Mails waiting", "widget": "number", "icon": "mail", "warnAbove": 5 },
    { "key": "disk", "label": "Storage", "widget": "meter", "unit": "bytes", "max": "diskMax" },
    { "key": "lastMail", "label": "Last mail sent", "widget": "time" }
  ],

  "configure": [
    { "key": "publicUrl", "label": "Public address", "type": "url", "required": true },
    { "key": "currency", "label": "Currency", "type": "select",
      "options": [{ "value": "EUR", "label": "Euro" }, { "value": "CHF", "label": "Franc" }] },
    { "key": "registration", "label": "Allow sign-up", "type": "bool", "hint": "Only invited people can join when off" },
    { "key": "smtpEnabled", "label": "Send mails", "type": "bool" },
    { "key": "smtpHost", "label": "Mail server", "type": "text", "showIf": { "smtpEnabled": true }, "required": true },
    { "key": "smtpPassword", "label": "Password", "type": "secret", "showIf": { "smtpEnabled": true } }
  ],

  "actions": [
    { "id": "openAdmin", "label": "Open admin", "icon": "settings", "kind": "link", "url": "/admin" },
    { "id": "refresh", "label": "Refresh feeds", "icon": "refresh", "kind": "post" },
    { "id": "clearCache", "label": "Clear cache", "icon": "trash", "kind": "post", "confirm": "Clear the cache now?", "danger": true }
  ],

  "roles": [ { "key": "admin", "label": "Administrator" }, { "key": "member", "label": "Member" } ],

  "provides": [ { "capability": "mail.send", "version": 1 } ],
  "consumes": [ { "capability": "files.store", "version": 1, "optional": true } ]
}
```

**`app`:** `id` is the id in MatDesign's registry (`AppRegistry.cs`), `icon` and `home` are relative to the app, `accent` is the one colour of the app (MatUi `data-accent`).

**`pages`:** what a consumer turns into a menu (one entry per page, in this order) and into the pages behind it. A page has `id`, `label`, `icon` (an id of the MatUi sprite) and
`sections`, drawn top to bottom. A section has a `kind`, an optional `label` (a heading) and `items` (keys of `state`, `configure` or `actions`):

| `kind` | Drawn as (MatUi) | `items` are |
|---|---|---|
| `tiles` | a grid of stat tiles (`mat-grid` of `mat-card`) | keys of `state` |
| `form` | a form (`mat-form`, `mat-field`), Save at the end; Save sends `PUT /api/configure` | keys of `configure` |
| `actions` | a button row (`mat-btn-row`), positive to negative, `danger` after the gap | ids of `actions` |
| `link` | a link that opens the app at `url` (in Mattiverse: in its frame) | none, `url` and `label` instead |

An app that defines no `pages` gets one page `overview` with all tiles and all actions and one page `settings` with all fields.

**`state`:** the tiles. `key` (camelCase, stable), `label`, `widget`, optional `icon`, `unit` (`EUR`, `bytes`, `%`, `ms` ... ; `bytes` is shown as KB, MB, GB), `decimals`,
`warnAbove` / `errorAbove` (the consumer colours the tile by the value; the app may also send a `level`, which wins).

| `widget` | Value | Drawn as |
|---|---|---|
| `number` | number | a big number with its unit |
| `text` | string | a short text |
| `bool` | true / false | Yes / No with an icon |
| `time` | ISO time | "5 minutes ago", the time in the title |
| `meter` | number; `max` = a number or the key of another state value | a meter (`mat-meter`) with "used of max" |
| `status` | `ok` / `info` / `warn` / `error` + `text` | a pill (`mat-pill`) |

**`configure`:** the **rudimentary** settings: what someone has to set to make the app work, and nothing else; the app's own settings pages stay the place for everything.
Rule of thumb: at most ten fields. A field: `key`, `label`, `type` (`text`, `number`, `bool`, `select`, `url`, `email`, `secret`), optional `required`, `options` (for `select`),
`min` / `max` (number, or length of a text), `pattern` (a regular expression the server checks too), `placeholder`, `hint`, `showIf` (shown only when all named fields have these
values: dependent fields only when they are needed), `restart: true` (takes effect after a restart), `fixed: true` (set from outside the app, for instance by the `MATSMTP_*` variables: shown, never changed; `PUT /api/configure` refuses it).

**`actions`:** `id` (camelCase), `label`, `icon`, `kind` = `link` (open `url`, relative to the app) or `post` (`POST /api/actions/{id}`), optional `confirm` (asked first) and `danger`.

**`roles`:** the roles of the app, the most powerful first (`key` is the value of the claim `role`, small letters, digits, `-` and `_`). MatIdentity offers them when an
administrator gives the app to people or groups. Optional: an app that decides its roles itself, or has none, leaves it out.

**`provides` / `consumes`:** see "Apps talking to each other".

The definition is the same for every caller and changes only with a new version of the app: answer with an `ETag` and `Cache-Control: no-cache`, so a consumer asks again cheaply.
The **values are never in the definition**.

## `GET /api/health`

```json
{
  "status": "ok",
  "app": "matsplit",
  "version": "1.4.27-20261015",
  "time": "2026-10-15T09:30:00+02:00",
  "checks": [
    { "name": "database", "status": "ok" },
    { "name": "mail", "status": "degraded", "message": "SMTP not reachable" }
  ]
}
```

- `status` is the worst of the checks: `ok`, `degraded` (works, but something is wrong) or `down` (cannot do its job).
- **HTTP 200 for `ok` and `degraded`, 503 for `down`.** The Docker `HEALTHCHECK` uses this address, and a degraded app must not be restarted.
- Fast and cheap (under a second, no outside call that can hang: cache it), safe to call every few seconds.

## `GET /api/state` and `GET /api/state/{key}`

```json
{
  "time": "2026-10-15T09:30:00+02:00",
  "values": {
    "groups": { "value": 12 },
    "openBalance": { "value": 318.5 },
    "queue": { "value": 7, "level": "warn" },
    "disk": { "value": 1288490188 },
    "diskMax": { "value": 10737418240 },
    "lastMail": { "value": "2026-10-15T08:02:00+02:00" }
  }
}
```

- One entry per key of the definition (plus helpers like `diskMax`); `level` (`ok`, `info`, `warn`, `error`) is optional, `text` too (for `status`).
- `GET /api/state/{key}` answers one entry (`{"key": "queue", "value": 7, "level": "warn"}`), `404` for an unknown key. Matmon can watch one value this way.

## `GET /api/configure` and `PUT /api/configure`

```json
{ "values": { "publicUrl": "https://split.example.com", "currency": "EUR", "registration": false,
              "smtpEnabled": true, "smtpHost": "mail.example.com", "smtpPassword": { "isSet": true } } }
```

- **A secret is never sent back:** its value is `{"isSet": true|false}`.
- `PUT /api/configure` takes `{"values": {"smtpHost": "mail2.example.com"}}`: only the keys that change; a secret that is not in the body stays as it is, `""` clears it.
  The answer is the same as `GET` (`200`) or `400` with `errors` per key; nothing is saved when one value is wrong. The server checks everything the definition says (`required`,
  `min`, `max`, `pattern`, `options`), and a field hidden by `showIf` is not required.
- The app keeps these values where it keeps its configuration, and its own settings page shows the same values.
- Each change is written to the app's activity / audit log with who did it (`mattiverse` for the key, the user or the client for a token).

## `POST /api/actions/{id}`

An empty body; the answer is `200 {"ok": true, "message": "12 feeds refreshed"}` or `409 {"ok": false, "message": "..."}`. Work that takes longer than a few seconds is started and
answered right away (`"message": "Started"`); its result shows up in `/api/state`. A consumer shows the message as a toast.

## Apps talking to each other

An app can do something for the others (**provides** a capability) and use what another one does (**consumes** it). A capability has a name (`mail.send`, `files.store`,
`notify.push`, ...) and a version, and its endpoints are under `/api/provides/{capability}/...` of the app that provides it. Each capability is written down here, in its own section
below, before the first app uses it: what it takes and what it answers. Nothing else of an app is called by another app.

- **Finding each other:** Mattiverse knows what is installed. `GET /api/directory` on Mattiverse (token) answers the apps with their address and their `provides`:
  `{"apps": [{"id": "matmail", "url": "https://mail.example.com", "provides": [{"capability": "mail.send", "version": 1}]}]}`. Without Mattiverse an app is told the address
  of another one by a variable (`MAT_PROVIDER_MAIL_SEND=https://mail.example.com`).
- **Trusting each other:** the calling app asks MatIdentity for a token with client credentials, for the audience of the other app and the scope of the capability
  (`mail.send`); the providing app checks both. No shared keys between apps.
- `consumes` with `optional: true` means the app works without it (and says so in a field hint or a tile); a consumer like Mattiverse can offer to install a provider.

### Capabilities

None yet. The first one is defined together with the first pair of apps that needs it.

## Sign-in with MatIdentity

Optional for every app, on as soon as the environment names MatIdentity:

| Variable | Meaning |
|---|---|
| `MATIDENTITY_AUTHORITY` | the address of MatIdentity (`https://id.example.com`). Set = sign-in through MatIdentity |
| `MATIDENTITY_CLIENT_ID` | the client of this app in MatIdentity (default: the app id) |
| `MATIDENTITY_CLIENT_SECRET` | its secret |
| `MATIDENTITY_INTERNAL_URL` | optional: where the app reaches MatIdentity from inside Docker (`http://mattiverse-matidentity-matidentity:2300`). The browser keeps using `MATIDENTITY_AUTHORITY`; the app sends its own server-to-server calls (discovery, keys, token, user info) to this address with the same path. MatIdentity has its public address as a fixed issuer and names it for every endpoint in its discovery document, also when it is read here |
| `MAT_API_KEY` | the key for `/api/*` (see Access) |

- **Auto login:** with `MATIDENTITY_AUTHORITY` set, a person who is not signed in goes straight to MatIdentity (OpenID Connect, authorization code with PKCE) and comes back signed in;
  there is no local sign-in form in the way. The local sign-in stays reachable at `/login?local=1` for the administrator (a way in when MatIdentity is down).
- Without the variable the app behaves exactly as before: its own users and its own sign-in.
- **The person:** matched by the claim `sub`; the first sign-in creates the local user (name, e-mail from the claims). The app keeps its own table of users (for its foreign keys and
  its own data). **Linking an existing account needs proof of both identities:** MatIdentity proves the person (its e-mail is set by its administrators), but the address of a local
  account proves nothing (a user may type any address into their own profile, or register with it as a user name), so an account whose e-mail (or, without an e-mail column, user
  name) matches is **never linked by the address alone** and no second account is made beside it. The app keeps a short-lived pending link (10 minutes, a protected cookie of the
  app) and shows its local sign-in (`?local=1&link=1`: an account with this address exists here; sign in once with its password to connect it, or cancel); only when exactly
  that account signs in there (with its second factor, if the app has one) does it get the `sub`, and the app writes it to its activity log. Another account signing in links
  nothing; several matches or a match linked to another `sub` are refused; no match creates the user. In .NET apps MatPlatform does this (`MatIdentitySignInResult.LinkRequired`,
  `MatIdentityPendingLink`).
- **Who may come in:** MatIdentity decides before the app sees anyone. An app is open only to the people and groups an administrator gave it to (and to the
  administrators of MatIdentity), unless it is opened to everyone. Someone else never reaches the app; taking the app away ends their sign-in at the next renewal.
- **The role:** per app the administrator chooses *Admin* (MatIdentity gives each person or group one of the app's `roles`; the claim `role` carries it and the
  app follows it at every sign-in) or *Managed by the app* (no claim `role`; the app gives rights itself, as before). An app reads `role` when it is there and keeps its
  own role management when it is not.
- **Sign-out** ends the session in the app and at MatIdentity (end-session endpoint).
- **Behind a TLS proxy** (Matcad) the scheme comes from `X-Forwarded-Proto`: the first value `https` makes the request https, so the `redirect_uri` is `https://.../signin-oidc` and the
  correlation cookies are `Secure` (only the scheme; the client address and the host stay the app's own policy; `X-Forwarded-Prefix` is not needed, every app is served at the root of its address).
  In .NET apps MatPlatform does this when MatIdentity is on (`MatIdentitySignInOptions.TrustForwardedProto`, default on).
- **Machines:** Mattiverse and the other apps get tokens with client credentials (see above); `X-Api-Key` stays as the way without MatIdentity.
- Mattiverse sets these variables when it installs or updates an app and MatIdentity is installed (the catalog entry says `identity: {}`, and `platform: true` for `MAT_API_KEY`); the client is created in MatIdentity through its API at the same time (`managedBy: mattiverse`, client id = the slug of the app) and removed with the app. When MatIdentity comes or goes, Mattiverse connects or disconnects every such app and redeploys it. Mattiverse signs in through MatIdentity itself too (client `mattiverse`).
- The local sign-in stays at `?local=1` (`1` or `true`) in every app.

## Mail through MatSMTP

**MatSMTP** is the mail relay of the family and one of the engines of Mattiverse (Mattiverse `docs/ENGINES.md`): the apps hand their mail to it on the shared Docker network
and it delivers it (directly or through a smarthost). Optional for every app that sends mail, on as soon as the environment names MatSMTP:

| Variable | Meaning |
|---|---|
| `MATSMTP_HOST` | the address of MatSMTP, inside Docker its container name (`mattiverse-matsmtp-matsmtp`). Set = all mail of the app goes through MatSMTP |
| `MATSMTP_PORT` | its SMTP port (`2025` in the container) |
| `MATSMTP_USERNAME` | the relay user of this app in MatSMTP (SMTP AUTH) |
| `MATSMTP_PASSWORD` | its password (a secret) |
| `MATSMTP_FROM` | the sender address the app uses for all its mail (`matsplit@example.com`) |
| `MATSMTP_TLS` | `none`, `starttls` or `tls`: how the connection is secured. Inside the Docker network a relay user may authenticate without TLS (`none`) |

- **Set:** the app sends **all** its mail (notifications, invitations, password resets, reports) through MatSMTP with these values and the sender `MATSMTP_FROM`. Its own SMTP
  settings (server, port, user, password, sender, encryption) are **shown as fixed**: on its settings page as values that cannot be edited, with one sentence that says where the
  mail goes and that Mattiverse sets it (never the password), and in its `/api/configure` as fields with `fixed: true` (a consumer draws them without letting them be changed;
  `PUT /api/configure` answers `400` for such a key). What the app stored before stays stored and comes back when the variables are gone.
- **Unset:** the app behaves exactly as before: its own SMTP settings, editable.
- **Mattiverse** sets these variables when it installs or updates an app whose catalog entry says `mail: {}` and MatSMTP is installed: it creates one **relay user per app** in
  MatSMTP through its API (`managedBy: mattiverse`, id = the slug of the app, allowed sender = `MATSMTP_FROM`), keeps its password and renews it only when it is missing, and removes
  the user with the app. When MatSMTP comes or goes, Mattiverse connects or disconnects every such app and redeploys it (the variables are then empty).
- In .NET apps **MatPlatform** reads them (`MatSmtpSettings.From(configuration)`: `IsConfigured`, `Host`, `Port`, `Username`, `Password`, `FromAddress`, `Tls` with
  `UseStartTls` / `UseTls`) and describes the fixed state (`Describe(language)` for the sentence, `Fix(field, language)` for a field of `/api/configure`).

## What an app has to do (definition of done)

1. The MatApiDefinition at `/api` and `/api/health`, `/api/state`, `/api/configure`, `/api/actions/{id}` as above; the Docker `HEALTHCHECK` calls `/api/health`.
2. `MAT_API_KEY` and the `MATIDENTITY_*` variables, documented in the README's settings table and in the release `docker-compose.yml` as comments (not required).
3. MatUi as the toolkit ([ADOPTING.md](ADOPTING.md)), the app in the registry here (`AppRegistry.cs`, `matui.apps.css`) with its accent and port.
4. Tests: the definition is valid (every key a section names exists, every icon is in the sprite, every `showIf` names a field), the answers of the endpoints (shape, status codes,
   a secret never sent back, `401` without key), the sign-in with and without MatIdentity.
5. A catalog entry in Mattiverse (`src/Mattiverse.Core/Catalog/apps/<app>.yml`) that passes `MAT_API_KEY` and, when present, the `MATIDENTITY_*` variables.
6. An app that sends mail reads the `MATSMTP_*` variables (see "Mail through MatSMTP"): set = all its mail through MatSMTP and its own SMTP settings fixed, in its pages and in
   `/api/configure`; its catalog entry says `mail: {}` and passes the six variables; README and release `docker-compose.yml` name them like the others.

**Shared code.** For .NET apps the endpoints, the key check, the checks of the settings, the validation of the definition and the OIDC wiring come as one package,
**MatPlatform** ([src/MatPlatform](../src/MatPlatform/README.md), version 1.3.0), so that an app only describes its pages and supplies its values:
`builder.AddMatPlatform<AppPlatform>(AppPlatform.Describe)` (a description with `o.App(...)`, `o.Page(...)`, `o.Tile(...)`, `o.Field(...)`, `o.LinkAction(...)` / `o.PostAction(...)`,
`o.Role(...)`), the values from the interfaces `IMatPlatformState`, `IMatPlatformConfiguration`, `IMatPlatformActions` and `IMatPlatformHealth`, `.AddMatIdentitySignIn<AppUsers>(...)` with
`IMatIdentityUsers` for the local user, and `app.MapMatPlatform()`. `MatPlatformValidator` checks a definition in the app's tests (keys of the sections, `showIf`, unique ids,
kinds, icons of the sprite). Like MatUi it is **copied** (`CODE-03`): `node tools/matplatform-copy.js <web project of the app>` writes the files to `<app>/MatPlatform/` with a
first line that names the version, and `matplatform.version`. The first app on it is MatSplit. The same types (`MatApiDefinition` and its records, `MatApiDefinition.Json`) read
a definition on the consumer's side; Mattiverse (`PlatformClient`) and MatIdentity still carry their own copies. Not there yet: a JSON Schema of the MatApiDefinition and a page
of MatDesign that checks the `/api` of a running app.
