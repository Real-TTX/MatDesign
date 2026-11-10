# People, access, rights and quotas (concept)

**Status: agreed on 2026-11-10 with the recommendations of section 10.** Its contract part is in [PLATFORM.md](PLATFORM.md) ("People, permissions and quotas", additions to
contract 2); this paper keeps the reasoning, like [APP-MODEL.md](APP-MODEL.md).

Built on 2026-11-10 and tested end to end in a test Mattiverse (a person and a group made in People, MatDrive's quota in the token, a change reaching MatDrive without a
new password, the usage coming back, the home screen of a person with only their apps):

- MatPlatform 2.4.0 (`o.Permission`, `o.Quota`, `MatAccess`, `MatPeople`, `IMatPlatformQuotas`).
- MatIdentity: grants with permissions and quotas, the claims, `WatchAsync` (a change ends the person's sessions in that app), SCIM 2.0, `people.scim`, the actions under
  `/api/v1/people`, the page of a grant under *Apps → Access*.
- Mattiverse: the area People (people, groups, what each is given per app), the home screen and the list of apps per person, `audience` and `roles` by the role in the app.
- MatDrive: the quota `storage`.

Not built yet: the switch *administrators only* for an app that does not sign in through MatIdentity (section 8), the permissions of MatDrive, and MatShape and MatAi.

The owner's request (2026-11-10): create people in Mattiverse, say which apps each person sees and what they may do there, and maybe give special rights or a quota in
one app. The information must travel through **standard mechanisms**, with **no code for a particular app**.

Three rules stand behind it:

- **No code that knows a particular app.** An app says what can be given in it (roles, rights, quotas). MatIdentity hands that out, and Mattiverse draws the forms for it.
  Mattiverse and MatIdentity know no app.
- **Every app works without Mattiverse, just as well,** and without MatIdentity it works as before: its own users, roles and limits.
- **Standards where there are standards.** The person and their rights reach an app as **OpenID Connect claims** (`roles` and `entitlements` are the names of RFC 9068
  and RFC 7643). People and groups are administered and read through **SCIM 2.0** (RFC 7643, RFC 7644). A change reaches an app as an **OpenID Connect Back-Channel Logout**.
  Only the quotas need a claim of our own, because no standard has one.

## What there is today (checked 2026-11-10)

| What | Today | What is missing |
|---|---|---|
| People and groups | MatIdentity has people (`sub`, name, e-mail, active, its own flag Admin/User) and groups (flat, with members) | Mattiverse keeps a user table of its own, mirrored at sign-in and not synchronised. It cannot create a person in MatIdentity |
| Who may use an app | MatIdentity: grants to people and groups, or *open to everyone*; its administrators may use every app. Checked at sign-in and at token renewal | Taking an app away does **not** end the person's sessions; it only takes effect at the next sign-in |
| The role in an app | MatIdentity in the mode *Admin*: one of the app's `roles` per person or group → claim `role`. The apps map `admin` and `user` to their own flag | Mattiverse ignores the `roles` of an app's `/api` and maps `admin` only for itself |
| Finer rights | none, anywhere | Rights per item (MatDrive's sharing roles, MatOffice's document roles, ...) are the apps' own business and stay so |
| Quotas | only inside the apps: MatDrive `User.QuotaBytes` (10 GB by default), MatShape's AI tokens per person (the same for everyone), MatAi's budgets per calling app and per assistant, MatSMTP's limits per relay account | Nothing travels. A quota per person can only be set in the app itself |
| An API for people | MatIdentity's `/api/v1` has clients only. People, groups, grants, invitations and passwords exist only as its pages | Nobody can administer people from outside |
| A directory for the apps | none (no SCIM, no `groups` claim, no webhooks) | An app knows a person only after their first sign-in |
| The home screen | every pinned app for every signed-in person; `audience` is `everyone` or `admin` (an administrator of Mattiverse); `person` widgets are not shown | APP-MODEL.md promised "a list of `roles` of the app" as an audience; it is not built |

## The model in one paragraph

**MatIdentity is the one place for people.** It keeps the people, their groups, and the **grants**: which person or group may use which app, with which role, which
extra **permissions** and which **quotas**. An app says in its manifest (`GET /api`) what can be given in it: its `roles` (as today), its `permissions` and its `quotas`.
When a person signs in to an app, MatIdentity puts their role, permissions and quotas **for this app only** into the token. MatPlatform reads them, and the app enforces
them with its own code. When a grant changes, MatIdentity ends the person's sessions in the affected app, and the next request signs them in again silently, with the
new values. **Mattiverse gets a People area that is a SCIM client of MatIdentity.** It keeps no people of its own, draws every form from the apps' definitions,
and shows each person only the apps they may use.

## 1. One place for people: MatIdentity

- People, groups and grants live in MatIdentity and nowhere else. Mattiverse's user table becomes what the apps' tables already are: a local mirror made at sign-in, for
  its own foreign keys (who started a job, who arranged their home screen). Its local administrator stays as the way in when MatIdentity is down (`?local=1`).
- A person who is created in Mattiverse is created **in MatIdentity** (section 7). A person who is created in MatIdentity's own pages is the same person.
- Sign-in, second step, passkeys, invitations, password resets and sessions stay MatIdentity's (they exist). Mattiverse only asks for them.

## 2. What an app can give: the manifest

Two members are added to the MatApiDefinition, next to `roles`. They are additions to contract 2, so a consumer that does not know them ignores them.

```json
"roles": [ { "key": "admin", "label": "Administrator" }, { "key": "user", "label": "User" } ],

"permissions": [
  { "key": "share-public", "label": "Share by public link", "roles": ["admin"] },
  { "key": "drives-create", "label": "Create shared drives", "hint": "Drives that belong to a team, not a person" }
],

"quotas": [
  { "key": "storage", "label": "Storage", "unit": "bytes", "min": 0 },
  { "key": "ai-tokens", "label": "AI tokens", "unit": "tokens", "period": "month", "min": 0 }
]
```

The values are only examples. Each app decides its own permissions and quotas when it adopts this.

- **`roles`** stays as it is: the most powerful role first. **A role is the coarse cut** (who administers the app, who uses it, who only reads). What a role may do inside
  the app stays the app's own business.
- **`permissions`** are rights **on top of a role**, each one an on/off switch. A permission has a `key` (same form as a role key: small letters, digits, `-` and `_`),
  a `label`, an optional `hint`, and optional `roles`: the roles that have the permission without anyone giving it. Without `roles`, a person has it only when it is given.
- **`quotas`** are numbers per person. A quota has a `key`, a `label`, a `unit` (the units of the tiles: `bytes`, `count`, `tokens`, `minutes`, ...), an optional `period`
  (`day` or `month`; none means a quota that does not renew, like storage), and optional `min`, `max` and `hint`. **A quota has no default in the manifest.** Without a
  value from MatIdentity the app's own setting applies (MatDrive's 10 GB), so the app keeps working without MatIdentity, and its default stays where it is set today.
- `MatPlatformValidator` checks both members: the keys are unique and well formed, every role a permission names exists, and every unit and period is known.
- An app whose roles MatIdentity does not manage (the mode *Managed by the app*) decides its permissions and quotas itself too. MatIdentity then only says who may come in.

## 3. Who gets what: grants in MatIdentity

A **grant** gives an app to a person or a group (as today), now with a role, permissions and quotas:

```json
{ "app": "matdrive", "role": "user", "permissions": ["share-public"], "quotas": { "storage": 53687091200 } }
```

What a person ends up with in an app (MatIdentity's `DecideAsync`, extended):

| | Rule |
|---|---|
| Access | As today: a MatIdentity administrator, a grant to the person, a grant to one of their groups, or an app open to everyone |
| Role | As today: the role of the person's own grant, else the most powerful role among their groups' grants, else the first role for an administrator, else the last role |
| Permissions | **Only added, never taken away:** the permissions the role brings (`roles` of the permission), plus those of the person's own grant, plus those of every grant of their groups. To take a right away, give a smaller role or remove the grant |
| Quotas | The value of the person's own grant; else the **largest** value among their groups' grants (*no limit* beats every number); else nothing, which means the app's own setting |

MatIdentity reads `permissions` and `quotas` from the app's `/api` the way it reads `roles` today (`AppRolesReader`: when the binding `identity.oidc` is made, and
with the button *Read again*). A grant that names a permission or quota the app no longer has keeps the rest, like a role that is gone today.

## 4. How an app learns it: claims

| Claim | Value | Standard |
|---|---|---|
| `role` | the person's role in this app (as today) | the contract since version 1 |
| `entitlements` | the person's permissions in this app, an array of keys: `["share-public"]` | RFC 9068 section 2.2.3.1 (the attribute of RFC 7643 section 4.1.2) |
| `mat_quotas` | the quotas that are set: `{"storage": 53687091200, "ai-tokens": null}`. `null` means *no limit*; a missing key means *the app's own setting* | a private claim (RFC 7519 section 4.3), written down in PLATFORM.md |

- All three come with the scope `roles`, which every app already asks for. They go into the ID token, the access token and the user info, and **each app sees only its
  own values**, because the audience of a token is the app.
- **MatPlatform** reads them at every sign-in. `MatIdentityPerson` gets `Permissions` and `Quotas`, next to `Role`.
- MatPlatform also keeps the last values of every person (`<DataDirectory>/matplatform/people.json`), so an app can ask about a person who is not the one making the
  request: a background job, or the owner of a shared folder. It asks through `MatAccess.Has(subject, "share-public")` and `MatAccess.Quota(subject, "storage")`, where
  `null` means the app's own setting.
- **The app enforces them with its own code.** Only MatDrive knows what storage is. That is the app's function, not integration code: MatDrive asks `MatAccess.Quota(...)`
  before its own `User.QuotaBytes`.
- **When a grant changes, the person signs in again without noticing.** A grant can change directly, through a group, through membership of a group, or through a
  role whose permissions changed. For every person whose access, role, permissions or quotas in an app change, MatIdentity:
  - sends a **back-channel logout** (with `sub` only) to that app;
  - revokes the person's refresh tokens for it.

  The app ends the person's sessions (MatPlatform already does this). Their next request goes through the auto sign-in, with the session at MatIdentity still valid and
  no password, and comes back with the new claims. A change takes effect within seconds, and it closes today's gap: taking an app away ends the person's sessions at once.
- The price is one round trip through MatIdentity, and a form the person was typing into is lost. The standard way without that price is OpenID's
  **CAEP** (Continuous Access Evaluation Profile, event *token claims change*): MatIdentity would push the new claims and the app would update the session. That is
  more work for every app, so it is an option for later (section 10).

## 5. People who have not signed in yet: SCIM for the apps

An app learns a person through their first sign-in. That is enough for most apps. Some need to know people before that: MatDrive sharing a folder with a colleague,
MatDo assigning a task, a list of names to pick from.

- **MatIdentity speaks SCIM 2.0** (section 7). An app reads it through a new key capability, **`people.scim`** (provider MatIdentity), with the variables
  `MAT_PEOPLE_SCIM_URL`, `_INTERNAL_URL` and `_KEY`. SCIM is a protocol, so it is called as that protocol, at `/scim/v2/...`, with the key of the binding as the bearer
  token. These are the rules of PLATFORM.md "Capabilities", so nothing new is needed.
- **The key of an app only reads, and sees only its own people:** the people who may use this app, with their role, permissions and quotas in this app only
  (`GET /scim/v2/Users`, `/Users/{id}`, `/Groups`).
- **The apps pull; nobody pushes.** No app has to host a SCIM server, and MatIdentity does not keep track of what each app knows.
- In .NET apps, MatPlatform reads it (`MatPeople.FindAsync`, `MatPeople.ListAsync`). An app that does not need a directory does not consume `people.scim`.

## 6. What is used: `GET /api/quotas/{subject}`

An app with quotas reports how much of them a person uses, so that Mattiverse can show "3.2 of 50 GB" next to the field.

```json
GET /api/quotas/6f1c...   (the key, or a machine token: never a person's token)
200 { "subject": "6f1c...",
      "values": { "storage":   { "used": 3435973836, "limit": 53687091200, "fixed": true },
                  "ai-tokens": { "used": 120400,     "limit": 1000000,     "fixed": false, "period": "month" } } }
```

- `limit` is what applies now: MatIdentity's value (`fixed: true`) or the app's own setting (`fixed: false`). `404` means the app does not know the person (they have never signed in).
- In .NET apps, the app implements `IMatPlatformQuotas.UsageAsync(subject)` and returns what is used and its own limit; MatPlatform puts MatIdentity's value over the limit.
- Today `MatApiAccess` lets **any** valid MatIdentity token with the app's audience use the whole `/api` (`MatApiAccess.cs:38-66`), and checks no scope or role. A person
  does not hold such a token: only the app's own confidential client gets one, and so do machines with `app:<id>`. Even so, MatPlatform 2.4.0 accepts only the key or a
  machine token (client credentials) for `/api/quotas`, so that one person's usage never reaches another. The same hardening would suit `/api/configure` and `/api/actions`;
  that is a change of its own.

## 7. The administration: SCIM at MatIdentity, the People area in Mattiverse

**MatIdentity becomes a SCIM 2.0 service provider** (`/scim/v2`: `Users`, `Groups`, `ServiceProviderConfig`, `Schemas`, `ResourceTypes`):

| SCIM | MatIdentity |
|---|---|
| `User.id` | the `Subject` (the `sub` of the tokens) |
| `userName`, `displayName`, `emails` (primary), `active` | `Username`, `DisplayName`, `Email`, `IsActive` |
| `password` (write only) | sets the password; a person created without a password and with an e-mail gets an invitation (sent through MatSMTP) |
| `groups` (read only), `Group.members` | the groups and their members |
| `roles`, `entitlements` (read only) | what the person **ends up with** (section 3), one entry per app with `type` = the app id: `{"value": "user", "type": "matdrive"}`. A standard SCIM client sees them without knowing our extension |
| the extension `urn:mat:scim:schemas:access:1.0` on User and Group | `grants` (what is given directly, can be written: the shape of section 3) and, on a User only, `access` (what they end up with per app, read only, with `via`: `person`, `group:<name>`, `everyone` or `administrator`) and `administrator` (MatIdentity's own flag) |

- **Scope of the first version:** filters `userName eq`, `emails.value eq`, `externalId eq` and `displayName eq` (groups); `PATCH` with `add`, `replace` and `remove`
  on `members`, `active` and the extension; paging. No bulk operations, no sorting, no ETags yet.
- **Access:**
  - MatIdentity's `MAT_API_KEY` reads and writes everything. It is accepted as `X-Api-Key` and as `Authorization: Bearer`, which is what SCIM clients send.
  - The key of a `people.scim` binding only reads, and only the people of its app (section 5).
  - Every write goes into MatIdentity's activity log. A broker names the person who acted in the header `X-Mat-Actor: <sub>`.
- **The actions SCIM does not have** (send an invitation again, send a reset link, sign out everywhere, unlock, reset the second step) become
  `POST /api/v1/people/{id}/actions/{action}` in MatIdentity's existing `/api/v1`.
- **The apps' side of the forms:** `GET /api/v1/clients` answers each app's roles, permissions, quotas, role mode and *open to everyone*, which MatIdentity already
  stores. Mattiverse draws its forms from this answer.
- **A side effect:** a company's own directory (Entra ID, Okta, Google) can provision people into MatIdentity through the same endpoint, because those are SCIM clients.

**The People area of Mattiverse** (a menu entry for administrators; it uses the key Mattiverse already holds as the broker):

- **People:** a list (name, e-mail, groups, number of apps, status; search, filter by app and by group). *New person*: name, user name, e-mail, then an invitation by
  mail or a starting password that must be changed, and groups and apps right away.
- **A person:** their details, active or not, their groups, the actions (invitation, reset link, sign out everywhere, unlock, second step), and the section **Apps**.
  It has one row per installed app that signs in through MatIdentity:
  - **Access:** on or off, or "through the group X", or "open to everyone".
  - **Role:** a choice of the app's roles.
  - **Permissions:** checkboxes. The ones the role brings are ticked and locked, with "from the role".
  - **Quotas:** a number with its unit. Empty means "the app's own setting (10 GB)", and the usage is shown when the app reports it (section 6).
- **Groups:** a list, and a group with its members and the same section Apps.
- **An app** (the app's page in Mattiverse): a new tab, **People**: open to everyone or not, and who has a grant (people and groups) with their roles.
- Every form is drawn from MatIdentity's answer about the app's roles, permissions and quotas. **No form knows an app.**
- Mattiverse talks SCIM to the provider of `identity.oidc`. Today that is MatIdentity, but any server that speaks SCIM with this extension would do, the same as
  "any broker will do".

## 8. What each person sees in Mattiverse

- Mattiverse is an app like the others. Its `/api` declares the roles `admin` and `user`, and MatIdentity gives them out.
  - `admin` sees everything, as today.
  - `user` sees their home screen, their apps and their account. Store, Backups, Jobs, Settings, Engines and People are for administrators only, as they already are
    (`AdminOnly`).
- **The home screen shows a person only the apps they may use.** Mattiverse reads the person's `access` from MatIdentity (SCIM `GET /Users/{sub}`) at sign-in and keeps it
  for a minute at most. An app the person may not use has no icon, no widget and no entry in the lists, and `/Apps/Open` refuses it. MatIdentity would refuse the
  sign-in anyway; this keeps the screen honest.
- **`audience` of launchers and widgets:**
  - `everyone`;
  - `admin`: the person has the **first role of this app**. For an app without roles it means an administrator of Mattiverse, which is today's meaning;
  - **the app's role keys** (`roles: ["admin", "verwalter"]`, a member of its own next to `audience`, so a shell that does not know it still reads `audience`), as
    APP-MODEL.md promised. Mattiverse knows the role per app from `access`;
  - `person` stays for later: it needs a token of the person for the app, and OAuth 2.0 Token Exchange (RFC 8693) is the standard way to get one.
- **Apps that do not sign in through MatIdentity** (no `identity.oidc`) have users of their own, and Mattiverse cannot give or take them away. Their icon is shown to
  everyone, as today, unless an administrator sets the app to *administrators only*. That is one generic switch per installed app (section 10).
- The engines stay in the System folder, for administrators.
- The order and the pages of the home screen are each person's own, which is separate work. This paper decides which apps can be on it.

## 9. Without Mattiverse, without MatIdentity

- **Without Mattiverse:**
  - MatIdentity's own pages do everything the People area does: People, Groups, and *Apps → Access*, which gets the permissions and quotas.
  - The claims are the same.
  - The `people.scim` binding can be made by hand, like any key capability.
- **Without MatIdentity:**
  - Every app works exactly as before: its own users, roles and limits. MatDrive's quota per user is set on its own user page.
  - No claim comes, so nothing is fixed.
- **An app with MatIdentity** shows on its own user page that the role, permissions and quotas come from MatIdentity, as fixed values, like the `MATSMTP_*` settings.
  MatPlatform writes the sentence (`MatAccess.Describe`).
- **Mattiverse without MatIdentity:**
  - The People area manages only Mattiverse's own accounts, which can sign in to Mattiverse.
  - It says that rights in the apps need MatIdentity, and offers *Install MatIdentity*.
  - The standard setup installs MatIdentity anyway.

## 10. Decisions to make

1. **Where people live.**
   - *Recommended:* only in MatIdentity. Mattiverse is a front end of it and keeps a mirror for its own foreign keys.
   - *Or:* Mattiverse keeps its own people and copies them to MatIdentity. That means two places that can disagree.
2. **The administration API of MatIdentity.**
   - *Recommended:* SCIM 2.0 with the extension `access`. It is a standard, and Entra ID, Okta and Google can use it too.
   - *Or:* a small API of our own, `/api/v1/people` and `/groups`. It is less work at first, but it is a format only we speak.
3. **How a changed grant reaches an app.**
   - *Recommended:* a back-channel logout of that person in that app, then a silent sign-in. It is already there in every app, and a change takes effect in seconds.
   - *Or:* CAEP *token claims change*, a push of the new claims without a new sign-in. Every app needs a receiver for it. This can come later, on top.
4. **Apps that need people before their first sign-in.**
   - *Recommended:* they read MatIdentity through SCIM (`people.scim`), and only the apps that need it.
   - *Or:* MatIdentity pushes every person to every app (SCIM push). Every app would need a SCIM server, and MatIdentity would have to track what it sent.
5. **Rights from groups.**
   - *Recommended:* permissions only add up. For quotas the person's own value wins, else the largest value of their groups.
   - *Or:* also *deny* entries. They are more flexible and much harder to understand.
6. **Apps that do not sign in through MatIdentity.**
   - *Recommended:* shown to everyone as today, with a switch per app for *administrators only*.
   - *Or:* shown to administrators only, until the app signs in through MatIdentity.
7. **The meaning of `audience: admin`.**
   - *Recommended:* the person has the app's first role (for an app without roles: an administrator of Mattiverse).
   - *Or:* it keeps meaning an administrator of Mattiverse.

## 11. Order of work

1. **This paper.** Agree on it.
2. **PLATFORM.md and MatPlatform 2.4.0** (MatDesign; 2.3.0 is kept for MatAi's `ai.openai`):
   - `permissions` and `quotas` in the definition (`o.Permission`, `o.Quota`, the validator);
   - the claims `entitlements` and `mat_quotas`, `MatIdentityPerson.Permissions` and `.Quotas`, and `MatAccess` with the store of the last values;
   - `GET /api/quotas/{subject}` with `IMatPlatformQuotas`;
   - `MatPeople` (the SCIM reader of `people.scim`) and `MatAccess.Describe`.
3. **MatIdentity:**
   - reads `permissions` and `quotas` along with `roles`;
   - grants with permissions and quotas, and `DecideAsync` extended;
   - the claims;
   - the Access tab with permissions and quotas;
   - a back-channel logout and refresh-token revocation whenever access, a role, a permission or a quota changes;
   - SCIM 2.0 with the extension, and `people.scim` as a key capability;
   - the actions under `/api/v1/people/{id}/actions`, and permissions and quotas in `/api/v1/clients`.
4. **Mattiverse**, after the home screen work that is going on now is merged:
   - the People area (people, groups, the section Apps, the tab People of an app);
   - the home screen per person (`access`, `audience` by role, the switch *administrators only*);
   - its own roles in its `/api`;
   - the user table as a mirror.
5. **The apps, by need:**
   - First **MatDrive**: the quota `storage` over its `User.QuotaBytes`, and its first permissions.
   - Then **MatShape** and **MatAi**: AI tokens per person and month.
   - Every app gets the update of MatPlatform anyway. One without permissions or quotas declares nothing new and works as before.

Each step is tested end to end in a test Mattiverse of its own, with the test account `admin@example.com` and a second person who has only the role `user`.
