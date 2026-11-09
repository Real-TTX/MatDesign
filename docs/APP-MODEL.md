# The Mat app model (concept)

**Status: agreed on 2026-11-09 with the recommendations of section 8, and built as contract version 2 of [PLATFORM.md](PLATFORM.md)**: MatPlatform 2.0.0, the broker and
the home screen of Mattiverse, MatOffice and MatDrive as the first pair. PLATFORM.md is the contract; this paper keeps the reasoning.

Where the build differs from the draft:

- The Connections area is a tag helper of MatPlatform, drawn on the server (`<mat-connections />`), not a component of MatUi. It needs no script and no new MatUi.
- The search across the apps and the widgets of the person who is looking come later.
- Step 5 (2026-11-09): MatIdentity (`identity.oidc`), MatSMTP (`mail.smtp`) and Matcad (`web.route`) are providers like any app, Mattiverse provides `shell.embed`
  itself; the per-app parts of IdentityLink, MailLink, FrameLink and ProxyPublisher are gone, the catalog says `consumes`. The DNS records stay Mattiverse's setup of Matddns
  (they give an app nothing; see PLATFORM.md "Capabilities"). MatAi (`ai.openai`) follows when its branch `feature/matai` is merged.
- Mattiverse's own icons and widgets (Store, Backups, the Engines widget, ...) are still drawn by its own code, not yet from a manifest of its own.

One language for three questions that every Mat app answers today in its own way:

- **What do I offer the other apps, and what do I need from them?** For example, MatOffice edits the office files of MatDrive, and MatSMTP sends the mail of all the others.
- **What do I add to the desktop?** Icons, widgets, search, the menu of an icon.
- **How am I set up?** Keys, addresses, the few settings.

Two rules stand behind it:

- **No code that knows a particular app.** Not in Mattiverse, and not in another app. An app describes itself, and everything else is generic.
- **Every app works without Mattiverse, just as well.** Mattiverse makes the connections for you, but an administrator can make every one of them by hand, in the app
  itself. Nothing of an app's own function needs Mattiverse (see "Without Mattiverse: the same app").

## What there is today (checked 2026-11-09)

| What | How it works today | Code that knows one app |
|---|---|---|
| An app describes itself (pages, tiles, settings, buttons) | `GET /api` (MatApiDefinition) | none: Mattiverse draws its *Manage* page from it |
| Sign-in through MatIdentity | catalog `identity: {}`. Mattiverse makes a client through MatIdentity's `/api/v1` and writes `MATIDENTITY_*` | `IdentityLink`, `MatIdentityClient`, `MatIdentityProvisioner` |
| Mail through MatSMTP | catalog `mail: {}`. Mattiverse makes a relay user through MatSMTP's `/api/v1` and writes `MATSMTP_*` | `MailLink`, `MatSmtpClient`, `MatSmtpProvisioner` |
| AI through MatAi (branch `feature/matai`) | catalog `ai: {}`. Mattiverse makes a client in MatAi and writes `MATAI_*` | `AiLink`, `MatAiClient`, `MatAiProvisioner` |
| Shown in the window of Mattiverse | `MAT_FRAME_ANCESTORS`, set when the compose file names it | `FrameLink` |
| Reachable under a domain | a route in Matcad and a record in Matddns | `ProxyPublisher`, `DnsPublisher`, `MatcadClient`, `MatddnsClient` and their provisioners |
| MatDrive opens office files in MatOffice | **by hand**: an administrator makes a key in MatOffice (*Connected apps*) and types it into MatDrive (*Integrations*). MatOffice has its own API for it (`/api/v1/host/*`) | two admin pages that only these two apps have |
| Icons on the home screen | one per installed app (the catalog icon). Store, Apps, Backups, Settings, Activity and Users are fixed in `Launcher.cs` | **an app cannot add an icon** |
| Widgets on the home screen | Today, Engines, Apps, Backups, Activity, Docker, Setup and Get started, all fixed in `Index.cshtml` | **an app cannot add a widget** |
| Keys | `MAT_API_KEY` for `/api`, plus one key per engine for its `/api/v1`: `MatDock__Api__Key`, `Matcad__ApiKey`, `MATDDNS_API_KEY`, `MATSMTP_API_KEY`, `MATAI_API_KEY` | two keys per engine |
| `provides` / `consumes` | part of the definition, but nobody uses them. PLATFORM.md says "None yet" | none |

So far, every new engine has added a link, a client, a provisioner and a catalog key to Mattiverse, and its variables to the compose file of every app that uses it. The
model below replaces all of that with one mechanism.

## The model in one paragraph

An app has a **manifest** (the MatApiDefinition, extended). In it, the app says three things:

- what it **provides** to other apps and what it **consumes** from them, both as **capabilities** (named, versioned interfaces, each written down once in PLATFORM.md);
- what it **contributes** to a shell: icons, widgets, search and the menu of an icon.

A **broker** (Mattiverse) reads the manifests of all installed apps. For every pair of a consumer and a provider of the same capability it makes a **binding**: it asks the
provider for the connection of this consumer, through one endpoint that every app has, and hands the answer to the consumer as environment variables.

A **shell** (the home screen of Mattiverse) draws the contributions of every app from the manifests and the values. It has no code of its own for any app.

Every app shows its bindings in one **Connections** area, and that area looks the same in every app.

```
            manifest: GET /api                          manifest: GET /api
 MatDrive  ───────────────────────►  Mattiverse  ◄───────────────────────  MatOffice
 consumes office.edit                broker + shell                       provides office.edit
 contributes: Drive, a storage          │    ▲                            contributes: Docs, Sheets,
   widget, search "Files"               │    │ the binding's variables      Slides, Base
                                        │    │ (a key made for MatDrive)
   ▲                                    ▼    │
   │ MAT_OFFICE_EDIT_URL, _KEY  PUT /api/bindings/office.edit/matdrive ──────►  MatOffice
   └──────────────────────────
 MatDrive ── /api/provides/office.edit/...  (Bearer: the key of the binding) ──►  MatOffice
```

Known patterns that this follows: *provision and bind* of the Open Service Broker API and servicebinding.io (a binding hands credentials to a consumer), and the
contribution points of VS Code and of Nextcloud apps (navigation, dashboard widgets, unified search: declared by the app, drawn by the host).

## Without Mattiverse: the same app

Mattiverse is a convenience, never a requirement. Every app runs on its own with `docker compose up` and does its whole job. The only difference is who makes the
connections: the broker, or an administrator by hand.

- **No variable is required.** An app starts and does its whole job with none of the `MAT_*`, `MATIDENTITY_*`, `MATSMTP_*` or `MATAI_*` variables set. Without them it uses its own
  sign-in and its own SMTP settings, has no AI and no office editor, or the administrator connects them by hand.
- **Every binding can be made by hand, in the app itself.**
  - The provider's Connections area has *Connect an app by hand*. It makes the binding and shows its values once.
  - The consumer's Connections area has a form for those values.
  - This is the same binding the broker makes, with `managedBy: manual`.
  - Instead, the administrator can also set the variables in the compose file.
- **A field is fixed only while a variable sets it.** Without the variable, the app's own settings stay editable, as they are today: the public address, the mail server, the
  office key. What was stored stays stored, and it comes back when the variable goes.
- **Contributions cost nothing.** Without a shell, nobody reads them, and the app's own menu stays the way in.
- **The endpoints for a broker are off.** `/api/bindings` answers only to `MAT_API_KEY`. Without the key it is off, like the rest of `/api` today.
- **Any broker will do.** The contract does not name Mattiverse. Any program with the key can read the manifests and make bindings: a small script, Matmon, or the next shell.
- **Only the broker adds:** finding the other apps by itself, bindings without a click, the restarts, icons and widgets on a home screen, and later the widgets for the person who is
  looking. It adds nothing to an app's own function.
- **A test checks it.** The definition of done gets two tests. One runs the app without any of these variables (sign-in, mail and its main function). The other makes a binding by
  hand through the Connections area.

**MatDrive and MatOffice without Mattiverse:**
1. In MatOffice, open Connections → *Connect an app by hand* → MatDrive. It shows the address and the key once.
2. In MatDrive, open Connections → *Opens office files with* and enter them.

That is how it works today, but now on the same pages as every other connection.

## 1. Capabilities

A capability is the one thing two apps agree on.

- **Name:** `area.verb` in small letters (`mail.smtp`, `office.edit`). Its **version** is a whole number and goes up only when a change breaks something.
- **Written down once**, in PLATFORM.md "Capabilities", before the first app uses it. The entry names:
  - the **parameters** a consumer may pass;
  - the **values** of a binding: name, meaning, and whether it is a secret;
  - the **environment variables** those values become;
  - the **endpoints** under `/api/provides/<capability>/`, if it has any;
  - what a binding **is** for the provider.

The first set is what exists today, put into the model:

| Capability | Provider | A binding is | Variables of the consumer |
|---|---|---|---|
| `identity.oidc` | MatIdentity | an OpenID Connect client for the consumer (sign-in, sign-out and back-channel addresses, roles) | `MATIDENTITY_AUTHORITY`, `_INTERNAL_URL`, `_CLIENT_ID`, `_CLIENT_SECRET` |
| `mail.smtp` | MatSMTP | a relay user with its sender | `MATSMTP_HOST`, `_PORT`, `_USERNAME`, `_PASSWORD`, `_FROM`, `_TLS` |
| `ai.openai` | MatAi | a client with its models and budget | `MATAI_URL`, `MATAI_KEY`, `MATAI_MODEL` |
| `web.route` | Matcad | a route `<app>.<domain>` to the consumer's container, with a certificate | `MAT_PUBLIC_URL` |
| `dns.record` | Matddns | a record for the name of the route | none |
| `shell.embed` | Mattiverse | the right to show the consumer in its window | `MAT_FRAME_ANCESTORS` |
| `office.edit` | MatOffice | a key for the consumer (MatDrive) | `MAT_OFFICE_EDIT_URL`, `_INTERNAL_URL`, `_KEY` |

- **Bound without being asked:** the broker binds `web.route`, `dns.record` and `shell.embed` for every app that has a UI, without the app naming them.
- **More than one provider:** a capability may have several. A hosted `dns.record` for people without their own domain (the backlog idea `<name>.user.mattiverse.de`) would be one more provider of
  the same capability, and no app would notice.
- **Variable names:** a capability names its variables once. The ones that exist keep their names. New ones are `MAT_<CAPABILITY>_<VALUE>`: capitals, and dots become `_`.
- **Later:** `files.store`, `files.open` (open with …), `notify.push`, … Each one is added together with the first pair of apps that needs it.

## 2. The manifest (MatApiDefinition, contract 2)

The manifest keeps everything it has today (`app`, `pages`, `state`, `configure`, `actions`, `roles`). Three members are new or extended: `consumes` takes parameters, `provides` is
used, and `contributes` is new.

```json
{
  "contract": 2,
  "app": { "id": "matdrive", "name": "MatDrive", "icon": "/img/logo/matdrive-mark.svg", "accent": "#8e24aa", "home": "/drive" },

  "provides": [],
  "consumes": [
    { "capability": "identity.oidc", "version": 1, "optional": true },
    { "capability": "mail.smtp", "version": 1, "optional": true, "params": { "sender": "drive" } },
    { "capability": "office.edit", "version": 1, "optional": true }
  ],

  "contributes": {
    "launchers": [
      { "id": "drive", "label": "Drive", "icon": "/img/logo/matdrive-mark.svg", "url": "/drive" }
    ],
    "widgets": [
      { "id": "storage", "label": "Storage", "size": "small", "kind": "meter", "items": ["used"], "url": "/Admin/System", "audience": "admin" },
      { "id": "recent", "label": "Recent files", "size": "medium", "kind": "list", "url": "/drive/recent", "audience": "person" }
    ],
    "quickActions": [
      { "launcher": "drive", "label": "Upload", "icon": "upload", "url": "/drive?upload=1" }
    ],
    "search": { "label": "Files", "audience": "person" }
  }
}
```

MatOffice says `"provides": [{ "capability": "office.edit", "version": 1 }]` and contributes four launchers: Docs, Sheets, Slides and Base. Each one opens the list of its
kind and has the quick action *New …*.

- Addresses and icons are relative to the app. The shell opens a launcher in its window when the app allows that (`shell.embed`), and in a new tab otherwise.
- `audience` decides who sees something: `everyone`, `admin`, or a list of `roles` of the app.
- An app without `launchers` gets one icon for `app.home`, as it does today.

## 3. Bindings

### What every provider answers

MatPlatform maps these endpoints. Only the broker may call them (`MAT_API_KEY`).

| Endpoint | Purpose |
|---|---|
| `GET /api/bindings` | the bindings: capability, consumer, `managedBy`, since, last used |
| `PUT /api/bindings/{capability}/{consumer}` | make or update the binding of this consumer |
| `DELETE /api/bindings/{capability}/{consumer}` | remove it (only one with the same `managedBy`) |

The body of a `PUT`:

```json
{
  "version": 1,
  "managedBy": "mattiverse",
  "consumer": { "id": "matdrive", "name": "MatDrive", "url": "https://drive.example.com", "internalUrl": "http://mattiverse-matdrive-web:4810" },
  "params": { },
  "renew": false
}
```

The answer is `200`:

```json
{ "variables": { "MAT_OFFICE_EDIT_URL": "https://office.example.com", "MAT_OFFICE_EDIT_INTERNAL_URL": "http://mattiverse-matoffice-web:4820", "MAT_OFFICE_EDIT_KEY": "mo_…" } }
```

- **A secret goes out only once**, when the binding is made or `renew` is `true`. After that it comes back as `{"isSet": true}`, the same rule as in `/api/configure`. The broker keeps
  what it got, and asks with `renew` when it has lost it.
- **Calls from the consumer** go to `/api/provides/{capability}/...` with `Authorization: Bearer <the key of the binding>`. MatPlatform checks the key and tells the endpoint which
  consumer is calling.
- **Protocols:** for capabilities that are a protocol (OpenID Connect, SMTP, the OpenAI API), the protocol itself is the endpoint, not `/api/provides`.

### Who writes the code for a binding

- **Most capabilities need no code.** When a binding is just a key for this consumer (`office.edit` and most app-to-app ones), MatPlatform keeps the bindings itself: a small
  file in the app's data folder, with the keys stored as hashes. The app writes no code for bindings, only the endpoints of the capability.
- **When a binding is more than a key**, the app implements one interface, `IMatBindingHandler` (bind, unbind, list), with its existing service code. Examples: MatIdentity (an OIDC
  client), MatSMTP (a relay user), MatAi (a client with a budget), Matcad (a route).

### How the consumer gets its binding

- **Through environment variables.** The broker writes them into the consumer's `.env`, passes them to its services and redeploys it.
- The catalog entry no longer lists the variables. The broker adds them to the services the entry names, by default the UI service.
- **Without a broker**, an administrator sets the same variables by hand, or enters the values in the Connections area. When both exist, the variables win and the values show as fixed.
- **In .NET apps**, MatPlatform reads them: `MatBindings.Get("office.edit")` gives `IsBound`, the values, and `Fixed(field)` for `/api/configure`. This replaces `MatSmtpSettings` and
  `MatAiSettings` with one generic class.

### What the broker does, the same for every app

1. **Install app A.** Start A, read its manifest, then:
   - bind every capability A consumes whose provider is installed, write the variables, and redeploy A once;
   - for every capability A provides, bind the consumers that are already installed, and redeploy them.
2. **Update A.** Read the manifest again, bind what is new, and unbind what is gone.
3. **Uninstall A.** Unbind A at every provider. For the apps that consumed something from A, the variables become empty, and they are redeployed.
4. **Health.** A consumer reports a broken binding in its `/api/health` as a check named after the capability (`office.edit: degraded`). The broker and the Connections area show it.

## 4. Contributions to a shell

The shell draws what the apps contribute, in its own look (`os.css`). It knows no app. Mattiverse's own icons and widgets become the contributions of Mattiverse's own manifest:
Store, Apps, Backups and Settings, and the widgets Engines, Backups and Activity. People arrange and hide icons and widgets, and the shell keeps that per person.

| Member | What the shell does with it |
|---|---|
| `launchers` | Icons on the home screen. An app may have several. Each has a label, an icon, an address, optionally an accent, an `audience` and a `badge` (a state key whose number sits on the icon) |
| `widgets` | Tiles of glass on the home screen. Each has a `size` (`small` 2×1, `medium` 2×2, `wide` 4×2), a `kind`, a `url` for a click, `refresh` in seconds (at least 30) and an `audience` |
| `quickActions` | The menu of an icon (long press or right click): a label, an icon, and an address or the id of a post action |
| `search` | The search box of the shell asks `GET /api/search?q=` of every app that contributes it. The results are `{title, subtitle, icon, url}` |

These are the kinds of widget:

| `kind` | Shows | Its values come from |
|---|---|---|
| `stat` | one value: a number, a text, a time or a status | `items`: one key of `state` |
| `meter` | used of max | one key of `state` with its `max` |
| `tiles` | up to four values | up to four keys of `state` |
| `list` | up to eight rows: title, subtitle, icon, time, address | `GET /api/widgets/{id}` → `{"items": [...]}` |

- **Where the values come from:** the browser never sees an app's key. The shell's server asks for the values: with `MAT_API_KEY` for `audience: admin` (numbers about the system), and
  with a token of the person who is looking for `audience: person` (that person's recent files).
- **Not in version 1:** a widget made of the app's own HTML in a frame. It would break the look and slow the home screen down. It comes only if a real need does.

## 5. One interface, one configure

- **One key.** `MAT_API_KEY` works on every endpoint the broker calls, including the engines' `/api/v1`. The old variables (`MatDock__Api__Key`, `Matcad__ApiKey`, …) stay as aliases, so
  an installation without Mattiverse does not break with an update. The documentation names only `MAT_API_KEY`.
- **One address scheme**, everything under `/api`:

| Endpoint | Who calls it |
|---|---|
| `/api`, `/api/health` | anyone (as today) |
| `/api/state`, `/api/configure`, `/api/actions/{id}` | the broker, a consumer like Matmon (as today) |
| `/api/bindings/...` | the broker (key) |
| `/api/connections` | the Connections area and the broker: what the app uses and offers, the state of each binding |
| `/api/provides/{capability}/...` | the consumers of a capability (the key of their binding) |
| `/api/widgets/{id}`, `/api/search` | the shell |

- **The engines stop being special.** Mattiverse no longer calls their `/api/v1` resources (clients, relay users, routes, records). It binds. Their `/api/v1` stays, for their own pages
  and for people who script them.
- **Configure stays as it is, with three new rules:**
  1. A value that a binding or a platform variable sets is `fixed`, automatically. MatPlatform marks it, so no app writes code for a single variable.
  2. When the broker passes `MAT_PUBLIC_URL`, the app's `publicUrl` field shows it as fixed. Without the variable, the field stays the app's own setting.
  3. The integration settings the apps have today move to the Connections area: MatDrive's *Integrations*, MatOffice's *Connected apps*, and the mail and AI fields. They stay
     editable there whenever no variable sets them.
- **The catalog says only how to run an app:** the compose file, the store text, the icon and the ports. These go: `platform`, `identity`, `mail`, `ai`, `provision`, `role`,
  and every `${MATIDENTITY_*}` line, because the broker injects the variables.

## 6. The Connections area of every app

It looks the same in every app. It is an admin page, and Mattiverse shows the same content on its *Manage* page.

- **Uses:** one row for each capability the app consumes:
  - what it is for, in words ("Opens office files with");
  - the provider (MatOffice, as a link);
  - how it was set (*Set by Mattiverse*, *Entered here*, or *Not connected*);
  - its state, taken from the health check;
  - when no variable sets it, a form with the capability's values, to enter them by hand.
- **Offers:** one row for each capability the app provides, with its consumers:
  - the app, since when, last used, and who manages it (Mattiverse or by hand);
  - the buttons *Renew key* and *Remove*;
  - *Connect an app by hand*, which makes a binding and shows its values once.
- **Adds to the home screen:** the app's launchers and widgets, read-only, so people know where they come from.

It is built once:

- MatPlatform answers `GET /api/connections`.
- MatUi has one component, `<mat-connections>` in `matui.js`. The app puts it on its admin page with one line, and Mattiverse draws the same JSON.
- No app builds its own page for this.

## 7. MatDrive and MatOffice in the model

**MatOffice**
- Provides `office.edit` version 1.
- Its host API `/api/v1/host/{formats,new,sessions}` moves to `/api/provides/office.edit/...`. The old paths keep working for one release.
- Its `HostClient` table becomes the binding store of MatPlatform, and the existing keys move over.
- *Connected apps* becomes the Connections area.
- It contributes four launchers: Docs, Sheets, Slides and Base.

**MatDrive**
- Consumes `office.edit` version 1, optionally.
- `OfficeService` reads `MatBindings.Get("office.edit")`.
- *Integrations* becomes the Connections area.
- It contributes the launcher Drive, the widgets Storage and Recent files, and the search *Files*.

**Mattiverse**
- Nothing about either app. Install MatOffice before or after MatDrive: the broker binds, MatDrive restarts with the variables, and a click on a `.docx` opens it in MatOffice.

**What stays app code** is what `office.edit` means: sessions, formats, and the file callback with MatDrive's token. That is the capability itself, written down in PLATFORM.md.

## 8. Decisions to make

1. **Trust between two apps.**
   - *Recommended:* a key per binding, made by the provider. It works without MatIdentity, and SMTP and the OpenAI API already work this way.
   - *Or:* a MatIdentity token for every call (client credentials).
   - PLATFORM.md says "no shared keys between apps" today. A binding key is shared by exactly two apps, and one call replaces it.
2. **How a binding reaches the consumer.**
   - *Recommended:* variables and one restart. It works without Mattiverse, and it is how things work today.
   - *Or:* live, through an endpoint of the consumer. There is no restart, but every app has to store the values and reload them.
3. **How the broker learns what an app consumes before it first runs.**
   - *Recommended:* start the app, read `/api`, bind, restart. The manifest stays the one source of truth.
   - *Or:* a copy in the catalog entry.
4. **What a widget can be.**
   - *Recommended:* only what the shell draws from data.
   - *Or:* also the app's own HTML in a frame.
5. **Widgets and search for the person who is looking.** These need a token of that person for the app. Mattiverse already signs in through MatIdentity, so it could ask for tokens for the apps'
   audiences. This belongs to a later phase.
6. **Variable names.**
   - *Recommended:* keep `MATIDENTITY_*`, `MATSMTP_*` and `MATAI_*`. Nothing breaks.
   - *Or:* rename them to `MAT_<CAPABILITY>_*`.

## 9. Order of work

1. **This paper.** Agree on it, then write contract 2 into PLATFORM.md: the manifest members, bindings, the capabilities (`office.edit` and the existing ones), contributions, and Connections.
2. **MatPlatform 2.0** (MatDesign):
   - the manifest builder: `o.Provides`, `o.Consumes(…, params)`, `o.Launcher`, `o.Widget`, `o.QuickAction`, `o.Search`;
   - `/api/bindings`, with the generic store and `IMatBindingHandler`;
   - `/api/connections`, `MatBindings` and `MAT_PUBLIC_URL`;
   - the validator checks the contributions;
   - MatUi gets `<mat-connections>`.
3. **Mattiverse:**
   - the broker: one generic class that binds and unbinds on install, update and uninstall;
   - the shell: launchers, widgets and quick actions from the manifests, with its own icons and widgets as its own manifest;
   - the old links stay until step 5.
4. **The first pair:** MatOffice and MatDrive (`office.edit`, launchers, widgets), tested end to end in the second Mattiverse.
5. **The engines, one at a time:**
   - MatSMTP (`mail.smtp`), MatAi (`ai.openai`), MatIdentity (`identity.oidc`), Matcad and Matddns (`web.route`, `dns.record`), then the frames (`shell.embed`);
   - after that, delete `IdentityLink`, `MailLink`, `AiLink`, `FrameLink`, `ProxyPublisher`, `DnsPublisher`, the write paths of the engine clients, and the catalog keys.
6. **The other apps:** contract 2, with their launchers and widgets. The first are MatDo, MatPaper, MatBoard and MatShape (the owner's choice, 2026-11-09):
   they stand completely on their own and use no other app (at most the sign-in of the engine, `identity.oidc`), so they need MatPlatform 2.2, their
   launchers and widgets, and nothing new in Mattiverse. MatBoard and MatShape also need an entry in Mattiverse's catalog.

MatDock stays the runtime of the broker: it runs the compose files and the backups. That is Mattiverse's own job, not a binding.
