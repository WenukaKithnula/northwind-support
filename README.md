# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## The defect (DEFECT-117, Task 1)

**Root cause:** the effect that loads tickets in `useTicketList` depended only on `search`, `page` and
`pageSize`, so changing any other filter updated the state without sending a request, and the next
search request then carried the earlier filters. An `eslint-disable` comment hid the missing dependency.

**Fix:** the list loads from one set of applied filters, so every filter change sends a request at once
and a filter added later needs no extra wiring. Only the search text is debounced (300 ms, the value the
code already used).

**Verify:** change Status and a `GET /api/tickets?status=...` shows up in the Network tab straight away.
The regression tests in `useTicketList.test.ts` assert the request that is sent, and 9 of them fail on
the original hook (`npm test`).

**More:** [docs/defect-117.md](docs/defect-117.md).

## Filtering (Task 1)

**Where:** `TicketQueries.ApplyFilters`. Each filter that is sent adds one `Where` before the count and the
paging, so `totalCount` and the pager match the filtered list. Nothing is filtered in C#.

**Checked:** `TicketQueriesFilterTests` run on in-memory SQLite, so the filters have to become SQL (16 of the
20 tests fail on the old query). I also read the SQL the API logs on SQL Server, and compared the API counts
with direct `COUNT(*)` queries.

**Next filter:** add the field to `TicketQuery` and one more `if` in `ApplyFilters`.

**More:** [docs/filtering.md](docs/filtering.md).

## Escalation and triage (Task 2)

**Rules as I understood them:**

- **Priority and due date:** the requested priority (Medium if none), Critical for a
  `ForcesCriticalPriority` category. The windows are 4, 8, 24 and 72 hours, halved for Premium with a
  1 hour floor, and come from the `Sla` section of `appsettings.json`. No category name is in the code.
- **Owner:** active, strictly below their limit, specialised where the category needs it; fewest open
  tickets, then lowest id. If nobody fits, the ticket is created unassigned (`201`) with a reason.
- **Escalation:** one level up with a reason (5 to 500 characters), due date restarted from now, owner
  checked again, history row written. Critical, Resolved and Closed are refused with a `409` that says
  which. SLA status is derived, never stored, and 25% or less of the window left is At risk.

**Where:** small domain classes (`SlaPolicy`, `PriorityResolver`, `AgentSelector`, `Ticket.Escalate`,
`SlaEvaluator`) shared through `TicketTriage`, so create and escalate use the same rules.

**Database:** one new table, `TicketEscalations`, from the `AddTicketEscalations` migration. Its index on
`(TicketId, EscalatedAtUtc DESC)` serves "one ticket's history, newest first" and doubles as the ticket
foreign key index; the two agent indexes EF added back the `Restrict` foreign key checks. The SLA filter
(`?slaStatus=`) runs in SQL.

**Checked:** the brief's worked examples in unit tests and against the running API, boundary tests with
a fixed clock, SQLite for the history and the SLA filter (which must agree with the status each row
shows), the SQL in the API console, and each screen in a browser.

**More:** [docs/escalation.md](docs/escalation.md).

## Design decisions

**The defect**

- **Chosen:** apply every filter at once and debounce only the search text. **Rejected:** adding every
  filter to the existing dependency array, because the debounce wrapped the whole effect and the
  dropdowns would wait, and adding `load` as lint suggested, which would run after every render; and a
  remount key or a forced reload, which hide the cause.

**Filtering**

- **Chosen:** one `ApplyFilters` with a `Where` per filter sent. **Rejected:** filtering in C#, which
  breaks the count and the paging, and a specification class, which is more than a few filters need.
- **Chosen:** tests on in-memory SQLite, because mocks do not prove the filters become real SQL.

**Escalation and triage**

- **Chosen:** small domain classes shared through `TicketTriage`. **Rejected:** the rules in the
  handlers, where create and escalate would each hold a copy.
- **Chosen:** the at-risk window from the policy for the priority and tier. **Rejected:** due date minus
  created date, which is wrong after an escalation, and a stored window column, which needs a migration
  the configuration makes unnecessary.
- **Chosen:** the escalated ticket is not counted against its own agent's limit. **Rejected:** counting
  it, because an agent at their limit would lose every ticket they hold.
- **Chosen:** ties go to the lowest agent id. **Rejected:** random, which is not repeatable.
- **Chosen:** history foreign keys use `Restrict`. **Rejected:** cascade, because an audit trail should not
  vanish by accident.
- **Chosen:** the `Sla` section in `appsettings.json`, so it reaches the Docker image too. **Rejected:**
  leaving it in `appsettings.Development.json`, which the image must not contain.
- **Chosen:** the SLA filter in SQL, with only the window lengths worked out in C# and passed in as
  constants. **Rejected:** date arithmetic in SQL, which differs between SQL Server and SQLite.

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

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

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
| `POST /api/tickets/{id}/escalate` | Raise the priority one level with a `reason` and `escalatedBy`. `400` bad input, `404` unknown ticket, `409` Critical, Resolved or Closed |
| `GET /api/tickets/{id}/escalations` | The escalation history, newest first |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

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
