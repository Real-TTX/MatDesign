# The platform contract of the Mat apps

What every app of the family offers besides its own pages, so that **Mattiverse** (and any other app: Matmon, a dashboard, the next one) can show it, watch it,
set it up and act on it **without knowing anything about it beforehand**, so that the apps can find and call each other, and so that one sign-in (**MatIdentity**)
works everywhere. The look is MatUi ([ADOPTING.md](ADOPTING.md)); this is the rest.

Contract version: **2** (2026-11-09: capabilities with bindings, the Connections area, contributions to a shell; the reasoning is in [APP-MODEL.md](APP-MODEL.md)).
A change that breaks a consumer raises it; adding a field does not. A consumer of version 1 reads a definition of version 2: everything new is an addition.

Two rules stand behind the contract:

- **No code that knows a particular app**, in Mattiverse or in another app. An app describes itself; a broker, a shell and the other apps work from that description.
- **Every app works without Mattiverse, just as well.** Mattiverse makes the connections by itself; an administrator can make each of them by hand in the app
  (the Connections area) or with environment variables. Nothing of an app's own function needs Mattiverse (see "Without Mattiverse").

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
| `GET /api/bindings`, `PUT` / `DELETE /api/bindings/{capability}/{consumer}` | a broker binds the consumers of what the app provides (see "Bindings") | key only |
| `GET /api/connections` | what the app uses, offers and contributes, for the Connections area and a broker | key or token |
| `GET /api/widgets/{id}` | the rows of a widget of the kind `list` (see "Contributions") | key or token |
| `/api/provides/{capability}/...` | what the app does for other apps (see "Capabilities") | the key of a binding |

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
  "contract": 2,
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

  "provides": [ { "capability": "files.store", "version": 1, "label": "Keeps files for" } ],
  "consumes": [
    { "capability": "office.edit", "version": 1, "optional": true, "label": "Opens office files with" },
    { "capability": "mail.smtp", "version": 1, "optional": true, "params": { "sender": "split" } }
  ],

  "contributes": {
    "launchers": [ { "id": "split", "label": "MatSplit", "icon": "/icon.svg", "url": "/", "badge": "queue" } ],
    "widgets": [ { "id": "balance", "label": "Open balance", "kind": "stat", "size": "small", "items": ["openBalance"], "url": "/groups", "refresh": 300, "audience": "admin" } ],
    "quickActions": [ { "launcher": "split", "label": "New group", "icon": "plus", "url": "/groups/new" } ]
  }
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

**`provides` / `consumes`:** see "Capabilities" and "Bindings". **`contributes`:** see "Contributions".

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

## Capabilities

An app can do something for the others (it **provides** a capability) and use what another one does (it **consumes** it). A capability has a name (`area.verb` in small letters:
`office.edit`, `mail.smtp`) and a whole-number version that rises only when a change breaks something. Each capability is written down here before the first app uses it: the
parameters a consumer may pass, the **values** of a binding (and whether each is a secret), the **environment variables** they become, the endpoints under
`/api/provides/{capability}/` if it has any, and what a binding is for the provider. Nothing else of an app is called by another app.

| Capability | Provider | A binding is | Variables of the consumer |
|---|---|---|---|
| `identity.oidc` | MatIdentity | an OpenID Connect client (see "Sign-in with MatIdentity") | `MATIDENTITY_AUTHORITY`, `_INTERNAL_URL`, `_CLIENT_ID`, `_CLIENT_SECRET` |
| `mail.smtp` | MatSMTP | a relay user (see "Mail through MatSMTP") | `MATSMTP_HOST`, `_PORT`, `_USERNAME`, `_PASSWORD`, `_FROM`, `_TLS` |
| `ai.openai` | MatAi | a client with its models and budget | `MATAI_URL`, `MATAI_KEY`, `MATAI_MODEL` |
| `shell.embed` | Mattiverse | the right to show the app in its window (see "Shown inside another app") | `MAT_FRAME_ANCESTORS` |
| `web.route` | Matcad | a route to the app, with a certificate | `MAT_PUBLIC_URL` |
| `office.edit` | MatOffice | a key for the host that opens its files in MatOffice (MatDrive) | `MAT_OFFICE_EDIT_URL`, `_INTERNAL_URL`, `_KEY` |

- **A key capability** is one that is not in this table with values of its own: a binding is the address of the provider and a key made for the consumer, as
  `MAT_<CAPABILITY>_URL` (public, for a browser), `_INTERNAL_URL` (on the shared network, for a server) and `_KEY` (dots become `_`, capitals). So a new capability between
  two apps needs no new variable names. `office.edit` is one.
- The engines' capabilities (`identity.oidc`, `mail.smtp`, `ai.openai`, `shell.embed`, `web.route`) are set with their variables only, never typed into the Connections area.
  Since 2.1 Mattiverse binds `identity.oidc`, `mail.smtp`, `web.route` and `shell.embed` like any other capability (their sections below); `ai.openai` follows with MatAi.
- **Implicit capabilities:** an app does not name `web.route` and `shell.embed`. A broker binds them for every app with a web page: `web.route` when a domain is set and a
  provider is installed, `shell.embed` always (the broker is the shell).
- **The DNS records** of the routes are not a capability: the DNS account, the zone and the list of records belong to the broker's domain (Mattiverse keeps one group of records in
  Matddns that follows the routes). A record gives an app nothing, so it is the broker's setup of Matddns, like the DNS account of Matcad's wildcard certificate.
- `optional: true` on `consumes` means the app works without it and says so (the Connections area shows "Not connected (optional)").
- `label` says what the app does with it, in words ("Opens office files with"); `params` is what a consumer passes to the provider when it is bound (`sender` of `mail.smtp`).
- **Calls between apps** go to `/api/provides/{capability}/...` of the provider with `Authorization: Bearer <the key of the binding>`; the provider checks the key and knows the
  consumer from it. A capability that is a protocol (OpenID Connect, SMTP, the OpenAI API) is called as that protocol instead.

### `office.edit` (version 1)

MatOffice edits the office files of a host (MatDrive). A key capability: the host gets `MAT_OFFICE_EDIT_URL` (where a browser opens the editor), `MAT_OFFICE_EDIT_INTERNAL_URL`
(where the host's server calls MatOffice) and `MAT_OFFICE_EDIT_KEY`. The endpoints, with the key as a bearer token:

- `GET /api/provides/office.edit/formats`: the kinds MatOffice edits (Docs, Sheets, Slides, Base) with the file types each opens and writes.
- `GET /api/provides/office.edit/new?kind=&format=&name=&language=`: an empty file of a kind, to be stored by the host.
- `POST /api/provides/office.edit/sessions`: opens a file. The host hands over the file's name, where MatOffice reads it (`GET`) and writes it (`PUT` with `If-Match`) at the
  host, and a token for that address that names the file, the person and their right; the answer is the address of the editor for the person's browser.

The same endpoints answer under `/api/v1/host/` for hosts that were connected before version 2. The host's own address for the callbacks is `MAT_INTERNAL_URL` when a broker set it.

### `mail.smtp` (version 1)

MatSMTP relays the mail of an app. **Parameters:** `sender`, the part before the `@` (default: the consumer id). **A binding** is a relay user: its id is the consumer id,
its name the consumer's, `managedBy` as the request says, and its only allowed sender `<sender>@<domain>` (`domain`: MatSMTP's default sender domain, else `localhost`). A
password is made when the binding is new or renewed. **Values:** `MATSMTP_HOST` (the host of the provider's internal address: MatSMTP's container on the shared network),
`MATSMTP_PORT` (its SMTP port inside the container), `MATSMTP_USERNAME`, `MATSMTP_PASSWORD` (a secret), `MATSMTP_FROM`, `MATSMTP_TLS` (`none`, `starttls` or `tls`, as MatSMTP
offers it). The consumer reads them as before ("Mail through MatSMTP"); nothing changes for it.

### `identity.oidc` (version 1)

MatIdentity signs people in for an app. **Parameters:** `callback` (default `/signin-oidc`), `signedOut` (default `/signout-callback-oidc`), `backchannel` (default
`/signout-backchannel`; empty: none). **A binding** is an OpenID Connect client: its id is the consumer id (`-app` added when it is shorter than two characters), its name the
consumer's, `managedBy` as the request says, the redirect addresses `<address><callback>` and `<address><signedOut>` for **every** public address of the consumer (`consumer.urls`,
else `consumer.url`), the back-channel logout `<consumer.internalUrl><backchannel>` (server to server, so never the public address), and the roles the consumer's `/api` names.
A secret is made when the binding is new or renewed. **Values:** `MATIDENTITY_AUTHORITY` (MatIdentity's issuer: its public address), `MATIDENTITY_INTERNAL_URL` (the provider's
internal address), `MATIDENTITY_CLIENT_ID`, `MATIDENTITY_CLIENT_SECRET` (a secret). The consumer reads them as before ("Sign-in with MatIdentity").

### `web.route` (version 1, implicit)

Matcad makes an app reachable: under a name of the domain, or, without a domain, on a port of the machine. Every app with a web page is reached this way (the broker
asks for it by itself); an app with a route publishes no port of its own. **Parameters** (strings): `host` (`<app>.<domain>`) **or** `port` (1024–65535; not 80, 443
or 2019, which Caddy uses itself) with an optional `publicHost` (the name or IP address browsers use for the machine: `nas.local`, `192.168.1.20`, `fd00::20`); `host`
and `port` together are refused. **A binding** is a route from `host`, or from the port (Caddy listens on it, plain HTTP, no host match), to the consumer's internal
address, named after the consumer, `managedBy` as the request says; a host route's certificate is Matcad's (the wildcard certificate of the domain when the broker set up
a DNS account). One route per consumer: a new `host` or `port` moves it (host to port and back when a domain comes or goes), and what an administrator set on it stays.
Publishing the port on Matcad's container is the broker's job. **Values:** `MAT_PUBLIC_URL` = `https://<host>` (`http://` for a development name under `.localhost`), for a
port route `http://<publicHost>:<port>` (an IPv6 address in brackets), and empty without `publicHost`: an app is never told `localhost`.

### `shell.embed` (version 1, implicit)

Mattiverse shows an app in the window of its home screen. The broker is the provider: it needs no request to itself and hands every app with a web page **`MAT_FRAME_ANCESTORS`**,
its own addresses (its route, this machine with the port it is published on, the addresses people signed in from), see "Shown inside another app".

## Bindings

A **binding** connects one consumer with one provider for one capability. The provider makes it (a key, a client, a relay user) and answers its values; the consumer gets them as
environment variables. A broker (Mattiverse) makes the bindings by itself; an administrator can make each of them by hand in the Connections area of both apps.

**What every provider answers** (only to its own `MAT_API_KEY`: a token of a person or another app may not change bindings):

| Endpoint | Purpose |
|---|---|
| `GET /api/bindings` | `{"bindings": [{"capability", "consumer", "name", "managedBy", "since", "lastUsed"}]}` |
| `PUT /api/bindings/{capability}/{consumer}` | make or update the binding of this consumer; the same request twice is one binding |
| `DELETE /api/bindings/{capability}/{consumer}?managedBy=mattiverse` | remove it (only one made by `managedBy`); `204`, or `404` when there is none |

```json
PUT /api/bindings/office.edit/matdrive
{ "version": 1, "managedBy": "mattiverse",
  "consumer": { "name": "MatDrive", "url": "https://drive.example.com", "internalUrl": "http://mattiverse-matdrive-web:4810" },
  "provider": { "url": "https://office.example.com", "internalUrl": "http://mattiverse-matoffice-web:4820" },
  "params": {}, "renew": false }

200 { "variables": { "MAT_OFFICE_EDIT_URL": "https://office.example.com", "MAT_OFFICE_EDIT_INTERNAL_URL": "http://mattiverse-matoffice-web:4820", "MAT_OFFICE_EDIT_KEY": "mo_…" } }
```

- The consumer id is small letters, digits and `-` (in Mattiverse the slug of the app); `managedBy` is `mattiverse` or `manual` (the Connections area).
- **A secret goes out once**: when the binding is made, or with `"renew": true`. After that it is `{"isSet": true}`, the same rule as `/api/configure`. A broker keeps what it got
  and asks with `renew` when it has lost it; a new key replaces the old one at once.
- `provider` names the addresses the broker publishes the provider under; without them the provider uses `MAT_PUBLIC_URL` / `MAT_INTERNAL_URL`, else the address of the request.
- `404` for a capability the app does not provide, `409` for a version it does not speak, `400` for a wrong body or id.
- **A provider may refuse** a binding: `400` when a parameter is missing or wrong, `409` when something is in the way that it must not take over (a route someone made by hand for
  the same host), each with `{"errors": {"<key>": "<message>"}}`. The Connections area shows the message. In .NET apps the handler throws `MatBindingRefusedException`.

**What the consumer does:** it reads the variables. When any variable of a capability is set it wins, and the app shows the values as fixed (in the Connections area and in
`/api/configure`); else the values entered by hand in the Connections area count; else the capability is not bound and the app works without it.

**What a broker does, the same for every app (Mattiverse `BindingBroker`):**

1. **Install an app:** start it, read its `/api`, bind every capability it consumes to an installed provider, write the variables into its `.env` and its web service, and
   deploy it once more; then bind every installed consumer of what it provides (a consumer that has a provider keeps it) and deploy those again.
2. **Update an app:** read `/api` again; bind what is new, remove what it no longer consumes.
3. **Uninstall an app:** remove its bindings at their providers; the consumers of what it provided get another provider or lose the variables, and are deployed again.
4. Every app of the contract also gets its own addresses: `MAT_INTERNAL_URL` (where the other apps reach it) and `MAT_PUBLIC_URL` (only when it is a real address: a route
   under the domain or this host by its configured name, never a guessed `localhost`).

## Contributions

What an app adds to a shell (the home screen of Mattiverse): the shell draws it in its own look and knows no app. Addresses and pictures are relative to the app.

| Member | What the shell does with it |
|---|---|
| `launchers` | Icons on the home screen, each `id`, `label`, `icon` (a picture of the app), `url`; optional `accent`, `audience`, `badge` (a key of `state` whose number sits on the icon). None: one icon for `app.home` |
| `widgets` | Tiles of the home screen: `id`, `label`, `kind`, `size` (`small` 2×1, `medium` 2×2, `wide` 4×2), `items`, `icon` (of the MatUi sprite), `url`, `refresh` (seconds, at least 30), `audience` |
| `quickActions` | The menu of a launcher: `launcher`, `label`, `icon`, and a `url` of the app or the id of a post `action` |

| `kind` | Shows | Values |
|---|---|---|
| `stat` | one value: number, text, time or status | one key of `state` |
| `meter` | used of max | one key of `state` that is a `meter` |
| `tiles` | up to four values | one to four keys of `state` |
| `list` | up to eight rows: title, subtitle, icon, time, address | `GET /api/widgets/{id}` → `{"items": [{"title", "subtitle", "icon", "url", "time", "level"}]}` |

- `audience`: `everyone` (default), `admin`, or `person` (values of the person who is looking: a shell needs a token of that person for the app, so Mattiverse does not show
  such widgets yet; a launcher for `person` is shown to everyone).
- The browser never sees an app's key: the shell's server asks for the values (`/api/state`, `/api/widgets/{id}`) and keeps them a few seconds.
- Not in this version: a widget made of the app's own HTML, and a search across the apps.

## The Connections area

Every app has the same admin page: what it **uses** (each capability it consumes: what for, connected to what, set by the variables / entered here / not connected, and, when no
variable sets it, a form with the values), what it **offers** (each capability it provides with the apps bound to it, who manages each, renew a key, remove, and *Connect an app by
hand*, which makes a binding and shows its values once) and what it **adds to the home screen**. `GET /api/connections` answers the same as JSON
(`{"uses": [...], "offers": [...], "contributes": {...}}`, secrets as `{"isSet": bool}`); Mattiverse shows it in the tab *Connections* of the app.

In .NET apps MatPlatform draws it: `<mat-connections />` on an admin page (a tag helper, drawn on the server with the classes of MatUi, no script needed); its forms post to
`/api/connections/form` with the anti-forgery token, and the app says who may use them (`MatPlatformOptions.ConnectionsAccess`, usually its administrators) and, for an app in one language, its language (`MatPlatformOptions.UiLanguage`). *Issue new* is
offered only for a capability with a secret.

## Without Mattiverse

The same app, with the connections made by an administrator instead of a broker:

- **No variable is required.** An app starts and does its whole job with none of the `MAT_*`, `MATIDENTITY_*`, `MATSMTP_*` or `MATAI_*` variables.
- **Every binding can be made by hand**: in the provider's Connections area (*Connect an app by hand*, its values are shown once) and the consumer's (the form), or with the
  variables in the compose file. MatDrive and MatOffice: MatOffice → Connections → *Connect an app by hand* → MatDrive; then MatDrive → Connections → *Opens office files with*.
- **A field is fixed only while a variable sets it**; without it the app's own setting is editable, and what was stored comes back when the variable goes.
- **Contributions cost nothing** without a shell; the app's own menu stays the way in. `/api/bindings` answers only to `MAT_API_KEY`: without the key it is off.
- **Any broker will do**: the contract names no Mattiverse. Any program with the key can read the definitions and make bindings.
- The old key variables of the engines (`MatDock__Api__Key`, `Matcad__ApiKey`, `MATDDNS_API_KEY`, `MATSMTP_API_KEY`, `MATAI_API_KEY`) stay valid next to `MAT_API_KEY`, so an
  installation without Mattiverse does not break with an update.

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
- **Back-channel logout** (OpenID Connect Back-Channel Logout 1.0): when a person signs out at MatIdentity, or an administrator ends their sessions there (a disabled account,
  a new password, "sign out everywhere"), MatIdentity posts a **logout token** to every app the person was signed in to, server to server, and the app ends its own sessions
  too, so nobody stays signed in to an app after leaving MatIdentity.
  - The app answers **`POST /signout-backchannel`** (form field `logout_token`, anonymous, no anti-forgery), only when `MATIDENTITY_AUTHORITY` is set.
  - It checks the token: a JWT signed with a key of MatIdentity's discovery document (read through `MATIDENTITY_INTERNAL_URL` like the other server-to-server calls), `typ`
    `logout+jwt`, `iss` = the issuer of the document, `aud` contains the client id, `iat` at most five minutes old and not in the future, `exp` when present, `events` with the
    member `http://schemas.openid.net/event/backchannel-logout` (an object), `sub` and/or `sid`, **no** `nonce`, and a `jti` it has not seen in that time (a replay cache).
  - Then it ends the sessions: with `sid` the local session(s) that started with that MatIdentity session (the app keeps the claim `sid` of the ID token with its session at
    sign-in; an app that keeps none ends every session of the `sub`); with `sub` alone (MatIdentity sends that when the person is signed out everywhere) every session of the
    person. A subject or session the app does not know is no error.
  - Answers: `200` done; `400` with `{"error": "invalid_request", "error_description": "..."}` for a token that is not valid or a failure to end the sessions; `501` for an app
    that cannot end sessions yet; every answer `Cache-Control: no-store`. MatIdentity tries once more after a failure and writes it to its log.
  - The address is registered at MatIdentity as the client's `backchannelLogoutUri`; Mattiverse passes the app's **internal** address on the shared Docker network
    (`http://<container>:<port>/signout-backchannel`), since MatIdentity calls it directly.
  - In .NET apps MatPlatform maps the endpoint and checks the token (`MapMatPlatform`, 1.4.0); the app implements `IMatIdentitySessions.EndSessionsAsync(subject, sessionId)`
    and stores `MatIdentityPerson.SessionId` with its session.
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

## Shown inside another app: `MAT_FRAME_ANCESTORS`

Mattiverse opens apps in the window of its home screen, which is a frame. An app that guards itself against being framed (`X-Frame-Options`, `frame-ancestors 'none'`,
or the `SAMEORIGIN` that ASP.NET Core's anti-forgery tokens add) cannot be shown there. Such an app may name who may frame it:

| Variable | Meaning |
|---|---|
| `MAT_FRAME_ANCESTORS` | the origins that may show the app in a frame, separated by spaces (`http://localhost:8030 https://mattiverse.example.com`). Each entry must be an origin: `http` or `https`, a host, maybe a port, nothing else; anything else (a path, a wildcard, `*`, a keyword) is left out, so a mistake never opens the app to every site |

- **Set:** every answer carries `Content-Security-Policy: frame-ancestors 'self' <origins>` (an existing `frame-ancestors` is replaced, the rest of the policy stays) and **no**
  `X-Frame-Options` (it knows no list of origins; the browsers let `frame-ancestors` win anyway). The cookies keep working in the frame as long as the frame and the page around
  it are the same site (the apps of one host or of one domain are).
- **Unset or empty:** the app behaves exactly as before.
- **Mattiverse** sets it for every app whose catalog entry names `${MAT_FRAME_ANCESTORS}` in its compose file (the engines): its route behind the proxy
  (`https://mattiverse.<domain>`), this machine with the port Mattiverse is published on (`localhost` and the configured address), and the addresses people signed in from.
  An app learns a new address of Mattiverse with its next update. Mattiverse checks the headers before it frames an app and offers a new tab otherwise.
- This is the capability `shell.embed` (see "Capabilities"); Mattiverse sets it with its `FrameLink` until that moves onto the bindings.
- In .NET apps **MatPlatform** does it (1.5.0, `MatFrameAncestors`): `AddMatPlatform` registers it, an app without it calls `builder.Services.AddMatFrameAncestors()`; the app's own
  security headers may stay as they are, the headers of the variable have the last word (src/MatPlatform/README.md "Shown inside another app").

## What an app has to do (definition of done)

1. The MatApiDefinition at `/api` and `/api/health`, `/api/state`, `/api/configure`, `/api/actions/{id}` as above; the Docker `HEALTHCHECK` calls `/api/health`. Contract 2:
   what it provides and consumes, `/api/bindings` and `/api/connections`, the Connections area on an admin page, and its launchers and widgets.
2. `MAT_API_KEY` and the `MATIDENTITY_*` variables, documented in the README's settings table and in the release `docker-compose.yml` as comments (not required).
3. MatUi as the toolkit ([ADOPTING.md](ADOPTING.md)), the app in the registry here (`AppRegistry.cs`, `matui.apps.css`) with its accent and port.
4. Tests: the app runs without any of the platform variables (its sign-in, its mail, its main function), a binding made by hand works,
   the definition is valid (every key a section names exists, every icon is in the sprite, every `showIf` names a field, the contributions), the answers of the endpoints (shape, status codes,
   a secret never sent back, `401` without key), the sign-in with and without MatIdentity, the back-channel logout (a valid logout token ends the session, `404` without MatIdentity).
5. A catalog entry in Mattiverse (`src/Mattiverse.Core/Catalog/apps/<app>.yml`) that passes `MAT_API_KEY` and names what the app consumes (`consumes: identity.oidc`, `mail.smtp`, ...)
   for an image that does not say it in its `/api`; the variables of the bindings and `MAT_FRAME_ANCESTORS` the broker passes to the web service itself.
6. An app that sends mail reads the `MATSMTP_*` variables (see "Mail through MatSMTP"): set = all its mail through MatSMTP and its own SMTP settings fixed, in its pages and in
   `/api/configure`; it consumes `mail.smtp` (its `/api` or its catalog entry); README and release `docker-compose.yml` name the variables like the others.

**Shared code.** For .NET apps the endpoints, the key check, the checks of the settings, the validation of the definition and the OIDC wiring come as one package,
**MatPlatform** ([src/MatPlatform](../src/MatPlatform/README.md), version 2.2.1), so that an app only describes its pages and supplies its values:
`builder.AddMatPlatform<AppPlatform>(AppPlatform.Describe)` (a description with `o.App(...)`, `o.Page(...)`, `o.Tile(...)`, `o.Field(...)`, `o.LinkAction(...)` / `o.PostAction(...)`,
`o.Role(...)`), the values from the interfaces `IMatPlatformState`, `IMatPlatformConfiguration`, `IMatPlatformActions` and `IMatPlatformHealth`, `.AddMatIdentitySignIn<AppUsers>(...)` with
`IMatIdentityUsers` for the local user (and `IMatIdentitySessions` for the back-channel logout), and `app.MapMatPlatform()`. Contract 2 (2.0.0) adds `o.Provides(...)` and
`o.Consumes(..., label, parameters)`, `o.Launcher(...)`, `o.Widget(...)`, `o.QuickAction(...)`, `IMatPlatformWidgets` for list widgets, `MatBindings` for what the app consumes
(`GetAsync(capability)`), `IMatBindingHandler` when a binding is more than a key (a key capability needs no code: `MatKeyBindingHandler`, and `MatBindingAccess.Require(capability)`
for its endpoints), the options `DataDirectory` (where the bindings are kept) and `ConnectionsAccess` (who may use the Connections area), and the tag helper `<mat-connections />`.
`MatPlatformValidator` checks a definition in the app's tests (keys of the sections, `showIf`, unique ids,
kinds, icons of the sprite). Like MatUi it is **copied** (`CODE-03`): `node tools/matplatform-copy.js <web project of the app>` writes the files to `<app>/MatPlatform/` with a
first line that names the version, and `matplatform.version`. The first app on it is MatSplit. The same types (`MatApiDefinition` and its records, `MatApiDefinition.Json`) read
a definition on the consumer's side; Mattiverse (`PlatformClient`) and MatIdentity still carry their own copies. Not there yet: a JSON Schema of the MatApiDefinition and a page
of MatDesign that checks the `/api` of a running app.
