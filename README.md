# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## What I built

All four tasks are done, in order. The full requirements are in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md); how I
approached each is in [`APPROACH.md`](APPROACH.md), and each task has a write-up with screenshots in `docs/`.

| # | Task | What it is now | Write-up |
| --- | --- | --- | --- |
| 1 | Defect and filtering | DEFECT-117 fixed at its cause with a regression test; every list filter applied in the database, with the count and paging matching | [defect](docs/defect-117.md), [filtering](docs/filtering.md) |
| 2 | Escalation and triage | New tickets get a priority, due date and owner from rules; a ticket can be escalated with a history; SLA status is derived and filterable; UI for all of it | [escalation](docs/escalation.md) |
| 3 | Login and authentication | Agents sign in with a JWT; every endpoint except login and `/health` is closed centrally; the actor comes from the token; login page and route guard | [authentication](docs/authentication.md) |
| 4 | Docker | `docker compose up --build` gives a migrated, seeded API with no baked-in secret, running as a non-root user | [docker](docs/docker.md) |

**To run it:** [Quick start](#quick-start) for the local way, or [Docker](#docker-task-4). **Development login:**
`alex.turner@northwind-support.example` / `LocalDevOnly-Password-1`.

**Where the spec was ambiguous,** I decided and wrote it down next to the rule: see each task section and
[Design decisions](#design-decisions).

---

## The defect (DEFECT-117, Task 1)

**Root cause:** the effect that loads tickets in `useTicketList` depended only on `search`, `page` and `pageSize`, so
changing any other filter updated state without sending a request. An `eslint-disable` comment hid the missing dependency.

**Fix:** the list loads from one set of applied filters, so every filter change requests at once; only the search text
is debounced (300 ms, the value already in the code).

**Verify:** change Status and a `GET /api/tickets?status=...` appears in the Network tab straight away. The regression
tests in `useTicketList.test.ts` fail on the original hook (9 of them) and pass now. More: [docs/defect-117.md](docs/defect-117.md).

## Filtering (Task 1)

**Where:** `TicketQueries.ApplyFilters`. Each filter that is sent adds one `Where` before the count and the paging, so
`totalCount` and the pager match the filtered list. Nothing is filtered in C#.

**Checked:** `TicketQueriesFilterTests` run on in-memory SQLite, so the filters must become SQL (16 of 20 fail on the old
query); I also read the SQL the API logs on SQL Server and compared the counts with direct `COUNT(*)` queries.

**Next filter:** add the field to `TicketQuery` and one more `if` in `ApplyFilters`. More: [docs/filtering.md](docs/filtering.md).

## Escalation and triage (Task 2)

**The rules as I understood them:**

- **Priority and due date:** the requested priority (Medium if none), Critical for a `ForcesCriticalPriority` category.
  Windows are 4, 8, 24 and 72 hours, halved for Premium with a 1 hour floor, read from the `Sla` section of
  `appsettings.json`. No category name is in the code.
- **Owner:** active, strictly below their limit, specialised where needed; fewest open tickets, then lowest id. Nobody
  eligible: the ticket is created unassigned (`201`) with a reason.
- **Escalation:** one level up with a reason (5 to 500 characters), due date restarted from now, owner re-checked, history
  row written. Critical, Resolved and Closed give a `409` that says which.
- **SLA status:** derived, never stored; 25% or less of the window left is At risk.

**Where:** small domain classes (`SlaPolicy`, `PriorityResolver`, `AgentSelector`, `Ticket.Escalate`, `SlaEvaluator`)
shared through `TicketTriage`, so create and escalate use the same rules. The SLA filter (`?slaStatus=`) runs in SQL.
More: [docs/escalation.md](docs/escalation.md).

## Signing in (Task 3)

- **Credentials:** a nullable `Agents.PasswordHash`, hashed and salted with ASP.NET Identity's PBKDF2 hasher; never plain text.
- **Token:** `POST /api/auth/login` returns a JWT (HMAC-SHA256) valid for **60 minutes**, signed with a key from
  configuration (`Jwt__SigningKey`, at least 32 bytes; the app will not start without it).
- **Generic failure:** unknown email, wrong password, inactive agent and no password all give the same `401` and message.
- **Protection:** one fallback policy closes every endpoint; only login and `/health` are open. Escalations are recorded
  against the agent in the token.
- **Web:** login page, a route guard that returns you to the page you wanted, the token in `sessionStorage` sent as a Bearer
  header, and a `401` ends the session. **Trade-off:** any script on the page can read `sessionStorage`, so a cross-site
  scripting bug would expose the token; `localStorage` is no safer, and an `httpOnly` cookie needs server support and CSRF protection.
- **For production:** the key from a secret store, an `httpOnly` cookie, a refresh flow or revocation, sign-in throttling, HTTPS.
- **Development login:** `alex.turner@northwind-support.example` / `LocalDevOnly-Password-1` (every active seeded agent
  has it; Yuki is inactive). It is set only by the seed. More: [docs/authentication.md](docs/authentication.md).

## Docker (Task 4)

```bash
docker compose up --build        # SQL Server, then the API on http://localhost:5080
```

- **Image:** two stages (.NET 10 SDK builds, ASP.NET runtime runs). Project files are restored before the source is copied,
  so a code change keeps the cached restore layer. Runs as the unprivileged `app` user on HTTP 8080, no certificate,
  no configuration baked in; it runs as `Production` (no Swagger).
- **Environment variables** (all in `.env.example`): `ConnectionStrings__Default`, `Jwt__SigningKey`,
  `ASPNETCORE_ENVIRONMENT`, `Database__MigrateAndSeed`.
- **Compose:** the `api` service reaches SQL Server as `sqlserver`, waits for it to be healthy, and on an empty volume
  applies migrations and seeds the demo data. `GET /health` is open and reports database reachability.
- **Migrations in a real deployment:** not at start-up. They would run once as their own release step (an EF migration
  bundle): several instances starting together would race, the API's login should not be able to change the schema,
  and a failed migration should stop the release.
- **Web app against the container:** `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`.
- **Checked:** `docker compose down -v && up --build` ended healthy with 40 seeded tickets at `/api/tickets`; a one-line
  `.cs` change kept `dotnet restore` as `CACHED`; the image is **455 MB** (runtime base 340 MB, SDK 1.3 GB); `whoami`
  prints `app`. More: [docs/docker.md](docs/docker.md).

## Database

- **`TicketEscalations`** (migration `AddTicketEscalations`): the escalation history. Foreign keys are `Restrict` so the
  audit trail cannot vanish by accident. Its index on `(TicketId, EscalatedAtUtc DESC)` serves the only way it is read,
  one ticket's history newest first, and doubles as the ticket foreign key index. EF also added two agent indexes that
  keep the `Restrict` checks cheap; I kept them.
- **`Agents.PasswordHash`** (migration `AddAgentPasswordHash`): nullable, so an agent can exist without a password.
- Reads use `AsNoTracking` and narrow projections; the filters and the SLA status run in SQL before the count and paging.

## Design decisions

**The rules live in the domain, once.** `SlaPolicy`, `PriorityResolver`, `AgentSelector` and `Ticket.Escalate` are small
classes shared by create and escalate through `TicketTriage`; I rejected putting the rules in the handlers, where each use
case would hold its own copy. Where the brief left a gap I chose: the at-risk window is the policy window for the priority
and tier (I rejected due date minus created date, which is wrong after an escalation, at the cost that changing the `Sla`
section moves the status of existing tickets); an escalated ticket does not count against its own agent's limit (otherwise
an agent at 6 of 6 would lose every ticket they hold); ties go to the lowest agent id (repeatable, unlike random); the history's
foreign keys are `Restrict`, so an audit trail cannot vanish by accident; and the `Sla` section moved to `appsettings.json`
so it also reaches the Docker image, which has no Development file.

**The database does the work.** One `ApplyFilters` adds a `Where` per filter sent, before the count and the paging; I
rejected filtering in C# (it breaks the count and pager) and a specification class (more than a few filters need). The SLA
filter is an expression of columns and constants worked out per priority and tier, because date arithmetic in SQL differs
between SQL Server and SQLite. Tests run on SQLite, because mocks would not prove the filters become SQL. For the defect I
applied every filter at once and debounced only the search text; I rejected adding the filters to the dependency array (the
debounce would delay the dropdowns), adding `load` as the lint rule suggested (it re-runs on every render), and a remount
key or reload, which hide the cause.

**Security is structural.** The password hash is a column on `Agents`, not a users table, because every user is an agent and there
are no roles. One fallback policy closes every endpoint and only login and `/health` opt out; I rejected `[Authorize]` on each
controller, where one forgotten attribute leaves a hole. Every refused sign-in gets the same message, so it cannot be used to find
valid emails. The token lasts 60 minutes with no refresh token (the cost: a deactivated agent's token works until it expires),
and the escalating agent comes from the token: `escalatedBy` is no longer in the request body.

**Docker is run like production.** The container runs as `Production`, with the key and connection string passed in; I rejected
running it as Development, which would ship Swagger and a development key. Migrate and seed have their own flag
(`Database__MigrateAndSeed`) instead of being tied to Development. Compose waits for a healthy SQL Server with `depends_on`
rather than a retry loop in the API, which would hide a real outage, and publishes only on `127.0.0.1`.

## Design patterns

Three that fit, each used because a rule or a failure mode asked for it, not to name a pattern.

- **Policy objects shared through one service** (`SlaPolicy`, `PriorityResolver`, `AgentSelector`, called by `TicketTriage`).
  *Why:* the brief's four-way risk is create and escalate each working out the SLA their own way. Each rule is a small
  class with no database or web knowledge, so it is tested with a fixed clock, and both use cases go through `TicketTriage`.
  *Not chosen:* the rules inside the handlers, which would copy them.
- **A query object and one step per filter** (`TicketQuery` bound from the URL, `ApplyFilters`, `SlaStatusFilter`).
  *Why:* a new filter is one more `if`, and the SQL does the filtering, counting and paging. `SlaStatusFilter` builds an
  expression of columns and constants, so SQL Server, not C#, decides each row's status. *Not chosen:* a specification-class
  hierarchy, which is more machinery than a few filters need.
- **A factory for validated configuration** (`SlaPolicyFactory`, and the `Jwt` settings validated at start-up).
  *Why:* the domain class takes plain numbers and knows nothing about configuration, and a bad value (a negative window,
  a short signing key) stops the app at start-up instead of failing later. *Not chosen:* reading configuration inside the rules.

Deliberately **not** added: MediatR or AutoMapper for a handful of endpoints, and interfaces other than at the real seams
(`IClock`, `IPasswordHasher`, `ITokenIssuer`, the repositories).

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. To run the API in Docker
instead, see [Docker](#docker-task-4). **Never commit real credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Testing

```bash
npm test                         # backend tests, then web tests (no database needed)
```

Last run: **332** backend unit tests, **4** architecture tests and **133** web tests pass; `dotnet format`, `npm run lint`
and `npm run typecheck` are clean.

**Covered:** the rules as small units with a fixed clock (SLA windows, forced Critical, agent selection, escalation, SLA
status at the exact boundaries, login); the filters, the SLA filter and the history on in-memory SQLite, so they must
become real SQL; the whole API over HTTP (every endpoint `401` without a token and working with one, wrong-key and
expired tokens refused, escalation recorded against the token's agent, `/health` open while the rest stays closed);
the layer and migration architecture tests; and in the web app the DEFECT-117 regression tests, the escalation form,
every SLA state, the login page, the route guard and the token header.

**Deliberately not tested:** browser end-to-end flows (I walked them through by hand); SQL Server (tests use SQLite; I read
the SQL there by hand and ran the SLA filter on it once); Docker (checked with the commands in
[docs/docker.md](docs/docker.md), as the brief says); load and concurrency, so the race under Known limitations is described, not measured.

## AI usage

**Tools and purpose.** Claude Code, as a pair programmer: to plan each task and the architecture, to explain the existing
code when I was stuck, to run builds, tests and checks, and to draft code and documents. The decisions and the checking
were mine.

**A workflow that worked.** Small steps, test first: a failing test, then the code, then the result shown for real (the
API, a browser, the SQL in the console, the Docker commands), and one focused commit.

**Accepted as-is.** The restore-first layout of the Dockerfile, which the brief all but prescribes; I proved it by
changing one `.cs` line and reading the build log, where the restore step said `CACHED`.

**Changed or rejected.** For DEFECT-117 it offered adding `load` to the effect's dependencies, or a remount key; I rejected
both because they hide the cause. An early SLA filter called an instance method inside the query, which EF cannot
translate, so it became an expression over columns and constants. A sign-out that navigated while the route guard also
redirected raced; sign-out now only clears the session.

**Bugs.** It registered the login handler before the token issuer it needs existed, so the app would not start (found
before the commit and moved), and it wrote a test that read a response body twice. It also helped me spot the
`eslint-disable` that hid DEFECT-117. A write-up claimed the escalation history stores an agent id; checking it against the code
showed it stores the name, and I fixed the doc.

**How I verified it.** The backend and web tests, regression tests that fail on the original code (9 for the hook, 16
of 20 filter tests on the old query), the SLA filter on the real SQL Server, and the main flows by hand in a browser and
against the API (screenshots in `docs/`).

## Known limitations

- **Concurrent tickets.** Open-ticket counts are read and then used, so two tickets raised at the same moment could both
  go to an agent who has room for only one. A fix needs a lock or a conditional update.
- **SLA status follows configuration.** It is derived from the current `Sla` section and the customer's tier, so changing
  either changes the status of tickets that already exist.
- **Tokens cannot be revoked.** A deactivated agent's token works for up to 60 minutes on endpoints other than
  `/api/auth/me`. There is no refresh token, no lockout or rate limiting on sign-in, and no roles: any signed-in agent
  can do everything. All of these are out of scope in the brief.
- **The token is in `sessionStorage`,** so a cross-site scripting bug would expose it (see Signing in). A production
  version would use an `httpOnly` cookie with CSRF protection.
- **One development password** is shared by every seeded agent. It is a local fixture and the seed (which sets it) must never
  run against a real database.
- **Docker.** No HTTPS (TLS belongs in front of the container), no `HEALTHCHECK` in the image, migrate-on-start is for
  local use (see Docker), no registry push, CI or multi-architecture build. All out of scope.
- **Web app.** `/tickets/abc` sends one pointless history request, and `useAsyncData` has no guard against a slow
  response arriving after a newer one.
- **Not built:** the optional SLA summary report (stretch 2.5).

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. Filters are applied in the database. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket. It is triaged: priority, due date and owner are chosen, and a `triage` object says why. `201` even when no agent is eligible |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Raise the priority one level with a `reason`; it is recorded against the signed-in agent. `400` bad input, `404` unknown ticket, `409` Critical, Resolved or Closed |
| `GET /api/tickets/{id}/escalations` | The escalation history, newest first |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |
| `POST /api/auth/login` · `GET /api/auth/me` | Sign in and get a token / the signed-in agent |
| `GET /health` | Whether the API can reach its database. Open, no token |

Every endpoint except `POST /api/auth/login` and `GET /health` needs `Authorization: Bearer <token>`.
Errors are `ProblemDetails`: `400` validation, `401` no or bad token, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
