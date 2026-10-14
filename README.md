# Northwind Support Desk

A support ticket system: raise tickets, filter them, assign them to agents and escalate them. **.NET 10 Web API** +
**React 19 / TypeScript** + **SQL Server**, in one monorepo.

## What I built

| # | Task | In this README | Write-up |
| --- | --- | --- | --- |
| 1 | Fix DEFECT-117, filter in the database | [The defect](#the-defect-defect-117-task-1) · [Filtering](#filtering-task-1) | [defect](docs/defect-117.md), [filtering](docs/filtering.md) |
| 2 | Triage, escalation, SLA status | [Escalation and triage](#escalation-and-triage-task-2) | [escalation](docs/escalation.md) |
| 3 | Agent sign-in with JWT | [Authentication](#authentication-task-3) | [authentication](docs/authentication.md) |
| 4 | Dockerise the API | [Docker](#docker-task-4) | [docker](docs/docker.md) |

All four tasks are done. The assignment is [ASSIGNMENT_CANDIDATE.md](ASSIGNMENT_CANDIDATE.md), my approach is [APPROACH.md](APPROACH.md).

## Quick start

**Prerequisites:** .NET SDK 10, Node.js 22 (20.19+ works), Docker running.

```bash
npm install
```

**Option A: Docker** (SQL Server and the API in containers, API on http://localhost:5080):

```bash
docker compose up --build
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web    # web app on http://localhost:5173
```

PowerShell: `$env:VITE_API_PROXY_TARGET="http://localhost:5080"; npm run web`

**Option B: local** (API on your machine, only the database in Docker):

```bash
npm run db:up        # SQL Server on localhost:1433
npm run api          # terminal 1: migrates, seeds, API on http://localhost:5043
npm run web          # terminal 2: web app on http://localhost:5173
```

`.env` is optional; every value has a local default. Other commands, the API list and troubleshooting are in [docs/running.md](docs/running.md).

## Development login

Local use only. Every active seeded agent shares this password, hashed when the data is seeded.

- Email: `alex.turner@northwind-support.example`
- Password: `LocalDevOnly-Password-1`

Yuki Tanaka is inactive and cannot sign in.

## Design decisions

**The defect**

- **Chosen:** the request is driven by one set of applied filters: dropdowns apply at once, the search text 300 ms after typing stops.
- **Rejected:** adding the filters to the existing dependency array, because the debounce wrapped the whole effect and the dropdowns would wait; adding
  `load` as lint suggests, because it changes every render and the effect would re-run; and a remount key or a reload, which hide the cause.

**Filtering**

- **Chosen:** one `ApplyFilters` with a `Where` per filter sent. **Rejected:** filtering in C#, which breaks the count and paging, and a specification class.
- **Chosen:** tests on in-memory SQLite, because mocks do not prove the filters become real SQL.

**Escalation and triage**

- **Chosen:** small domain classes (`SlaPolicy`, `PriorityResolver`, `AgentSelector`, `Ticket.Escalate`) shared through `TicketTriage`.
  **Rejected:** the rules in the handlers, where create and escalate would each keep a copy.
- **Chosen:** the at-risk window from the policy for the priority and tier. **Rejected:** due date minus created date, which is wrong after an escalation.
- **Chosen:** an escalated ticket is not counted against its own agent's limit. **Rejected:** counting it, because an agent at their limit would lose every ticket they hold.
- **Chosen:** ties go to the lowest id. **Rejected:** random, which is not repeatable.
- **Chosen:** history foreign keys use `Restrict`. **Rejected:** cascade, because an audit trail should not vanish by accident.
- **Chosen:** the SLA filter runs in SQL; only the window lengths are worked out in C# and passed in as constants. **Rejected:** date arithmetic in SQL, which differs
  between SQL Server and SQLite.
- **Chosen:** the `Sla` section in `appsettings.json`, so it reaches the Docker image. **Rejected:** leaving it in the Development file, which the image must not contain.

**Authentication**

- **Chosen:** a nullable `PasswordHash` on `Agent`. **Rejected:** a users table; every user is an agent and there are no roles, so it only adds a join.
- **Chosen:** 60 minute tokens. **Rejected:** longer, because without refresh tokens a short life limits a stolen one.
- **Chosen:** `sessionStorage`. **Rejected:** `localStorage`, which keeps the token after the browser closes.
- **Chosen:** one fallback authorization policy, `[AllowAnonymous]` only on login. **Rejected:** `[Authorize]` per controller, where a new endpoint could be left open.

**Docker**

- **Chosen:** the container runs as `Production`, with the key and connection string passed in. **Rejected:** Development, which would ship Swagger and a dev key.
- **Chosen:** migrate and seed behind their own flag, and compose waiting for a healthy SQL Server. **Rejected:** a retry loop in the API, which hides a real outage.

**Patterns I used**

- **Policy objects behind one service:** `SlaPolicy`, `PriorityResolver` and `AgentSelector`, used through `TicketTriage`, so create and escalate share one rule.
- **A query object with one step per filter:** `TicketQuery`, `ApplyFilters` and `SlaStatusFilter` add one condition each, so a new filter is one more step.
- **A factory for validated configuration:** `SlaPolicyFactory` builds the policy once and fails at start-up on a bad value, so it never reaches a request.

## The tasks

### The defect (DEFECT-117, Task 1)

**Root cause:** the effect that loads tickets in `useTicketList` depended only on `search`, `page` and `pageSize`, so changing another filter updated state but never
sent a request. An `eslint-disable` hid the missing dependency.

**Fix:** every filter change sends a request at once; only the search text is debounced.

**Verify:** change Status and a `GET /api/tickets?status=...` appears in the Network tab. The regression tests assert the request and fail on the original hook.

More: [docs/defect-117.md](docs/defect-117.md).

### Filtering (Task 1)

**Where:** `TicketQueries.ApplyFilters`. Each filter sent adds one `Where` before the count and paging, so `totalCount` and the pager match the filtered list.

**Checked:** the tests run on SQLite, so the filters have to become SQL, and they fail on the old query; I also read the SQL the API logs on SQL Server.

**Next filter:** add the field to `TicketQuery` and one more `if` in `ApplyFilters`.

More: [docs/filtering.md](docs/filtering.md).

### Escalation and triage (Task 2)

**Rules as I understood them:**

- **Priority and due date:** the requested priority (Medium if none), Critical for a `ForcesCriticalPriority` category. Windows are 4, 8, 24 and 72 hours, halved for Premium
  with a 1 hour floor, from the `Sla` section of `appsettings.json`.
- **Owner:** active, below their limit, specialised where needed; fewest open tickets, then lowest id. Nobody fits: `201`, unassigned, with a reason.
- **Escalation and status:** one level up with a reason (5 to 500 characters), due date restarted, history written; Critical, Resolved and Closed give a `409`. SLA status
  is derived, never stored.

**Checked:** boundary tests with a fixed clock, SQLite for the history and the SLA filter, the SLA filter on the real SQL Server, and the flows by hand.

More: [docs/escalation.md](docs/escalation.md).

### Authentication (Task 3)

**Credentials:** `PasswordHash` on `Agent`, salted and hashed with ASP.NET Identity's PBKDF2 hasher. I wrote no hashing myself.

**Token:** `POST /api/auth/login` returns a signed JWT, its expiry and the agent's name. A wrong email, a wrong password and an inactive agent give the same `401`. It lasts
**60 minutes**, and the key comes from `Jwt__SigningKey`, never from code (the app will not start without it). `escalatedBy` comes from the token.

**Storage:** the web app keeps it in `sessionStorage` and sends it from `api/client.ts`. Any script on the page can read it, so a cross-site scripting bug would expose it.

**For production:** an `httpOnly` cookie with short tokens, refresh and revocation, a key from a secret store, and lockout or rate limiting.

More: [docs/authentication.md](docs/authentication.md).

### Docker (Task 4)

**Image:** multi-stage `apps/api/Dockerfile`, built from the repo root: the SDK image builds, the ASP.NET image runs it as the non-root `app` user on HTTP 8080. Project files are
restored first, so a code change reuses the restore layer. The API image is **455 MB**; the runtime base is 340 MB and the SDK image 1.3 GB.

**Environment:** the image holds no connection string or secret, and `.dockerignore` keeps `appsettings.Development.json` out. Compose passes `ConnectionStrings__Default`,
`Jwt__SigningKey` (from `JWT_SIGNING_KEY`), `ASPNETCORE_ENVIRONMENT` and `Database__MigrateAndSeed` at run time, with local defaults when there is no `.env`; see `.env.example`.

**Migrations:** here the API migrates and seeds itself on an empty volume. In a real deployment migrations would be a separate release step, such as an EF Core migration bundle, so
instances do not migrate at once, the app's database login does not need schema rights, and a failed migration stops the release.

**Checked:** `docker compose down -v && docker compose up --build` ended with `/health` returning 200 and 40 seeded tickets at `/api/tickets` (401 without a token); a one-line `.cs` change
reused the cached restore layer; `whoami` prints `app`; the web app works against the container. See [docs/docker.md](docs/docker.md).

## Across all tasks

### Database

**Added:** the `TicketEscalations` table (`AddTicketEscalations`) and a nullable `PasswordHash` column on `Agent` (`AddAgentPasswordHash`).

**Index:** `(TicketId, EscalatedAtUtc DESC)` serves "one ticket's history, newest first" and doubles as the ticket foreign key index; the two agent indexes EF added back the foreign key checks.

### Testing

```bash
npm test         # dotnet test, then the web tests (Vitest). No database or Docker needed
```

**Tested:** 332 backend unit tests, 4 architecture tests and 133 web tests, covering SLA boundaries with a fixed clock, agent choice, escalation, filters on SQLite, login, 401 and 200 over real HTTP,
`/health`, and the web screens.

**Not tested, on purpose:** SQL Server itself (SQLite stands in; I read its SQL by hand and ran the SLA filter on it once), two tickets raised at once, the Docker build, and a browser end to end run.

## AI usage

**Tools and purpose.** Claude Code, as a pair programmer, one small step at a time: to plan each task, explain the existing code when I was stuck, run builds and checks, and draft code and
documents. I read each change before committing it. Most of my time went into planning and settling the architecture first, then developing small step by small step and checking each one myself.

**A workflow that worked.** Small steps, test first, then the result shown for real (API, browser, SQL, Docker commands) and one focused commit.

**Accepted as-is.** The restore-first Dockerfile layout, which the brief all but prescribes; I proved it with a one-line change and the `CACHED` build log.

**Changed or rejected.** For DEFECT-117 it offered adding `load` to the dependencies or a remount key, which I rejected. An early SLA filter used an instance method EF cannot translate, so it
became an expression of columns and constants. A sign-out that raced the route guard now only clears the session.

**Bugs.** It registered the login handler before the token issuer existed (the app would not start; fixed before the commit) and wrote a test that read a response body twice. It also helped me spot
the `eslint-disable` that hid DEFECT-117, and one claim in my write-up (the history stores an id) was wrong when I checked the code.

**How I verified it.** Tests red then green, regression tests that fail on the original code, the SLA filter on the real SQL Server, and the main flows by hand (screenshots in `docs/`).

## Known limitations

**Task 1:** `useAsyncData` has no guard against a slow response arriving after a newer one, and `/tickets/abc` sends one pointless history request.

**Task 2:** two tickets raised at once could both go to an agent with room for one. Changing the `Sla` section or a customer's tier changes the status of existing tickets.

**Task 3:** no refresh tokens or revocation, so a deactivated agent's token works until it expires; no lockout or rate limiting; no roles; all seeded agents share one development password, and the seed must never
run against a real database.

**Task 4:** no HTTPS in the container (TLS belongs in front of it), no image `HEALTHCHECK`, and migrate-on-start is for local use only.

**Not built:** the optional SLA summary report; registration, password reset, email verification, OAuth/SSO, MFA and roles; notifications, background jobs, real-time updates, microservices, event sourcing,
multi-tenancy, internationalisation, caching, CI/CD and a visual redesign; and, as Task 4 says, Kubernetes/Helm, registry pushes, dockerising the React app, Docker secrets or a vault, multi-architecture builds
and any deployment beyond the Task 4 image.
