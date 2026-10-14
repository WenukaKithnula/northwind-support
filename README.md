# Northwind Support Desk

A support ticket system: **.NET 10 Web API**, **React 19 / TypeScript** and **SQL Server** in one monorepo. All four tasks
are done. How I approached each is in [APPROACH.md](APPROACH.md), and each task has a write-up with screenshots in [docs/](docs/).

## What I built

| # | Task | Result | Write-up |
| --- | --- | --- | --- |
| 1 | Defect and filtering | DEFECT-117 fixed at its cause with a regression test; every list filter runs in the database | [defect](docs/defect-117.md), [filtering](docs/filtering.md) |
| 2 | Escalation and triage | New tickets get priority, due date and owner from rules; escalation with history; derived, filterable SLA status; the UI for it | [escalation](docs/escalation.md) |
| 3 | Login | JWT sign-in; every endpoint except login and `/health` closed centrally; the actor comes from the token | [authentication](docs/authentication.md) |
| 4 | Docker | `docker compose up --build` gives a migrated, seeded API with no baked-in secret, running as non-root | [docker](docs/docker.md) |

## Run it

**Prerequisites:** .NET SDK 10, Node.js 22 (20.19+ works) and Docker.

```bash
cp .env.example .env     # optional, every value has a default
npm install
npm run db:up            # SQL Server in Docker
npm run api              # terminal 1: migrates, seeds, serves on http://localhost:5043
npm run web              # terminal 2: http://localhost:5173
```

Or in Docker: `docker compose up --build` serves the API on http://localhost:5080 (check `/health`). To point the web
app at the container: `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`. Other commands, the API list and
troubleshooting are in [docs/running.md](docs/running.md).

## Development login

`alex.turner@northwind-support.example` / `LocalDevOnly-Password-1`. Every active seeded agent has the same password
(Yuki is inactive and cannot sign in). It is a local fixture, set only by the development seed.

## Design decisions

**The rules live in the domain, once.** `SlaPolicy`, `PriorityResolver`, `AgentSelector` and `Ticket.Escalate` are small classes
shared by create and escalate through `TicketTriage`; I rejected putting them in the handlers, where each use case would
keep its own copy. Where the brief left gaps I chose: the at-risk window is the policy window for the priority and tier
(not due date minus created date, which is wrong after an escalation); an escalated ticket does not count against its
own agent's limit; ties go to the lowest agent id; history foreign keys are `Restrict` so an audit trail cannot vanish; and
the `Sla` section moved to `appsettings.json` so it reaches the Docker image.

**The database does the work.** One `ApplyFilters` adds a `Where` per filter, before the count and paging; I rejected filtering
in C# and a specification class. The SLA filter compares columns with per-priority constants, because date maths differs
between SQL Server and SQLite. For the defect I applied every filter at once and debounced only the search text, and
rejected the quick fixes (adding `load` or a remount key) because they hide the cause.

**Security is structural.** The hash is a column on `Agents`, not a users table, because every user is an agent. One fallback
policy closes every endpoint; I rejected `[Authorize]` on each controller, where one missed attribute leaves a hole. Every refused
sign-in gets the same message. The token lasts 60 minutes with no refresh token, and the escalating agent comes from the
token: `escalatedBy` is gone from the request body.

**Docker is run like production.** The container runs as `Production` with the key and connection string passed in, not as
Development (which would ship Swagger and a dev key). Migrate and seed have their own flag, compose waits for a healthy SQL
Server instead of retrying inside the API, and the port is published on `127.0.0.1` only.

## The tasks

**The defect (DEFECT-117).** *Root cause:* the effect that loads tickets in `useTicketList` depended only on `search`, `page` and
`pageSize`, so another filter changed state without sending a request, and an `eslint-disable` hid it. *Fix:* the list loads from one
set of applied filters; only the search text is debounced (300 ms). *Verify:* change Status and the request shows in the Network
tab at once; the regression tests fail on the original hook (9 of them) and pass now.

**Filtering.** `TicketQueries.ApplyFilters`; no filtering in C#. Checked with in-memory SQLite tests (16 of 20 fail on the old query), the SQL the API
logs on SQL Server, and direct `COUNT(*)` comparisons. *The next filter* is one more field on `TicketQuery` and one more `if`.

**Escalation and triage.** *Priority:* the requested one (Medium if none), Critical for a `ForcesCriticalPriority` category. *Due date:* 4, 8, 24
or 72 hours, halved for Premium with a 1 hour floor, read from the `Sla` section. *Owner:* active, below their limit, specialised where
needed; fewest open tickets, then lowest id; nobody eligible gives a `201` unassigned with a reason. *Escalation:* one level up with a
5 to 500 character reason, due date restarted, owner re-checked, history row written; Critical, Resolved and Closed give a `409`. *SLA
status* is derived and never stored (25% or less of the window left is At risk).

**Authentication.** Passwords are hashed and salted with ASP.NET Identity's PBKDF2 hasher (`Agents.PasswordHash`). `POST /api/auth/login` returns
a JWT (HMAC-SHA256, **60 minutes**) signed with a key from configuration (`Jwt__SigningKey`, 32 bytes at least; the app will not start without
it). Unknown email, wrong password and inactive agent all get the same `401`. The web app keeps the token in `sessionStorage` and sends it from
`api/client.ts`; the **trade-off** is that any script on the page can read it (a cross-site scripting bug would expose it), and an `httpOnly`
cookie would need server support and CSRF protection. *For production:* a secret store for the key, a cookie, revocation or refresh, sign-in
throttling, HTTPS.

**Docker.** Two stages (.NET 10 SDK builds, ASP.NET runtime runs); project files are restored before the source is copied so a code change keeps
the cached restore layer. It runs as the non-root `app` user on HTTP 8080 with no configuration baked in. *Environment variables* (all in
`.env.example`): `ConnectionStrings__Default`, `Jwt__SigningKey`, `ASPNETCORE_ENVIRONMENT`, `Database__MigrateAndSeed`. *Image size:* **455 MB**
(runtime base 340 MB, SDK image 1.3 GB). *Checked:* a clean `down -v && up --build` ended healthy with 40 seeded tickets; a one-line `.cs`
change left `dotnet restore` `CACHED`; `whoami` prints `app`. *Migrations in a real deployment:* not at start-up but as their own release step (an
EF migration bundle), because several instances starting together would race, the API's login should not be able to change the schema,
and a failed migration should stop the release.

## Database

`TicketEscalations` (migration `AddTicketEscalations`) holds the history. Its foreign keys are `Restrict`, and its index on `(TicketId, EscalatedAtUtc DESC)`
serves the only way it is read, one ticket's history newest first, and doubles as the ticket foreign key index. `Agents.PasswordHash`
(migration `AddAgentPasswordHash`) is nullable. Reads use `AsNoTracking` and narrow projections, and the filters and SLA status run in SQL.

## Design patterns

- **Policy objects behind one service** (`SlaPolicy`, `PriorityResolver`, `AgentSelector` via `TicketTriage`): the rule is written once and tested with a fixed clock.
- **A query object with one step per filter** (`TicketQuery`, `ApplyFilters`, `SlaStatusFilter`): a new filter is one more `if`, and SQL does the work.
- **A factory for validated configuration** (`SlaPolicyFactory`, the `Jwt` settings checked at start-up): a bad value stops the app instead of failing later.

Not added: MediatR or AutoMapper for a handful of endpoints.

## Testing

`npm test` runs the backend and web tests (no database needed): **332** backend unit, **4** architecture and **133** web tests pass, and
`dotnet format`, `npm run lint` and `npm run typecheck` are clean. They cover the rules with a fixed clock, the filters and history on SQLite (real SQL),
the whole API over HTTP (401 without a token, escalation recorded against the token's agent, `/health` open), and the web app (the DEFECT-117
regression, escalation form, SLA indicator, login, route guard). *Deliberately not tested:* browser end-to-end flows (I walked them by hand),
SQL Server (tests use SQLite; I read its SQL by hand), Docker (checked with the commands in [docs/docker.md](docs/docker.md)), and load or concurrency.

## AI usage

**Tools and purpose.** Claude Code, as a pair programmer, one small step at a time: to plan each task, explain the existing code when I was stuck,
run builds and checks, and draft code and documents. For each step I read the change before committing it. Most of my time went into planning:
settling on a suitable architecture first, then developing small step by small step and checking each one myself. The decisions and the
checking were mine. **A workflow that worked:** small steps, test first, then the result shown for real (API, browser, SQL, Docker commands) and one focused commit. **Accepted as-is:** the restore-first Dockerfile layout, which the brief all but
prescribes; I proved it with a one-line change and the `CACHED` build log. **Changed or rejected:** for DEFECT-117 it offered adding `load` to
the dependencies or a remount key, which I rejected; an early SLA filter used an instance method EF cannot translate, so it became an expression of
columns and constants; a sign-out that raced the route guard now only clears the session. **Bugs:** it registered the login handler before the token
issuer existed (the app would not start; fixed before the commit) and wrote a test that read a response body twice; it also helped me spot the `eslint-disable`
behind DEFECT-117, and one claim in my write-up (the history stores an id) was wrong when I checked the code. **How I verified:** tests red then green,
regression tests that fail on the original code, the SLA filter on the real SQL Server, and the main flows by hand (screenshots in `docs/`).

## Known limitations

- **Concurrent tickets:** open counts are read then used, so two simultaneous tickets could both go to an agent with room for one.
- **SLA status follows configuration:** changing the `Sla` section or a tier changes existing tickets' status.
- **Tokens cannot be revoked** for their 60 minutes; no refresh token, sign-in throttling or roles (all out of scope). The token is in `sessionStorage`.
- **One development password** for every seeded agent; the seed must never run against a real database.
- **Docker:** no HTTPS (TLS belongs in front), no image `HEALTHCHECK`, migrate-on-start is for local use, no registry push or CI.
- **Web app:** `/tickets/abc` sends one pointless history request, and `useAsyncData` has no guard against a slow response arriving after a newer one.
- **Not built:** the optional SLA summary report.
