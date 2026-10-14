# Approach

How I worked on each task, and why. The details, screenshots and checks are in `docs/`, and the decisions and
limitations are in the README.

I took one small step at a time, checked it, and only then committed it, so the history follows the order I did things in.

## Task 1: the defect and the filtering

**The defect.** I reproduced it before reading any code: in the Network tab, changing Status sent nothing, and typing in search
then sent the old status. So the value was being stored but the request was not being sent, and I followed it from the dropdown
to the effect that loads the tickets. Its dependency array held three of the eleven filter fields, and a lint suppression hid the
warning. I did not just add the missing fields: the debounce wrapped every change, and the lint suggestion (add `load`) would run
the effect after every render. I changed what drives the request instead. The list loads for one set of applied filters, any change
sends a request at once, and only the search text waits 300 ms. I wrote the tests first, asserting the request that is sent, and
checked that 9 of the 17 fail on the original hook.

**The filtering.** The API read the filters and ignored them. I added one step, `ApplyFilters`, with a `Where` per supplied filter,
before the count and the paging, so `totalCount` and the pager match the filtered list; each filter is one `if`, so the SLA filter in
Task 2 is one more. I tested it on in-memory SQLite instead of a mock, because a mock would pass whatever the query looked like (16 of
20 failed before the change), then read the SQL on SQL Server and compared the API counts with direct counts.

Details: `docs/defect-117.md` and `docs/filtering.md`.

## Task 2: triage and escalation

The brief said the challenge was where the rules go, so I started there. Each rule became a small class with no database or web code in
it (`SlaPolicy`, the priority rule, the choice of owner, `Ticket.Escalate`), and one service, `TicketTriage`, puts them together so
creating and escalating share the same logic. That let me test the rules, with the brief's worked examples as acceptance tests, before
there was an endpoint or a screen.

Then I went in this order, committing each step: the rules and their tests, the history table and its migration, the create and escalate
endpoints, the SLA filter in SQL, the screens, the write-up. I read the generated SQL after each backend step.

Where the spec left room I decided and wrote it down: how the at-risk window is worked out, who keeps a ticket at an agent's limit, how equal
agents are split, and what happens when nobody is eligible. Each is in the README under "Design decisions". The SLA filter was the one place a
rule had to exist twice (in C# for the status a row shows, in SQL for the filter), so its main test builds tickets either side of every
boundary and checks the filter selects exactly the rows that show each status. I checked it on SQL Server as well as SQLite, because the two
can translate a query differently.

Details: `docs/escalation.md`.

## Task 3: signing in

I started from what the brief says must be true: a request without a valid token fails everywhere, and the person recorded on an escalation is
the person who was signed in. Then I looked for the smallest design that makes both hard to get wrong.

The password hash is a column on `Agent` (every user is an agent and there are no roles), hashed with ASP.NET Identity's hasher. The token lasts
60 minutes and the app will not start without a 32-byte key from configuration. I closed the API with one fallback policy instead of
`[Authorize]` on each controller, so a new endpoint is closed by default. Every login failure gives the same 401, and the password is
checked even when there is no such agent so the response time gives nothing away. Who escalated now comes from the token.

On the web side, `client.ts` attaches the token and handles any 401 in one place, a route guard remembers the page the agent wanted, and sign-out
only clears the session and lets the guard redirect, so there is one place that navigates. I checked it with tests that send real HTTP requests and by
hand in the browser, including a token I edited myself.

Details: `docs/authentication.md`.

## Task 4: Docker

The brief gives the checks up front (a clean `up`, a cached restore, the size, `whoami`), so I worked backwards from them and treated each as
something to prove with a command. The Dockerfile restores from the project files before copying the source, and I confirmed it by changing one
`.cs` line and reading the build log, where the restore step said `CACHED`. Nothing goes into the image: `.dockerignore` keeps the Development settings file
(a SQL password and a dev key) out, and I checked `docker history` and the image's files.

I ran the container as Production, not Development: Development would pass every check and still be wrong, with Swagger and a development key. It also forced
migrate-and-seed onto its own flag. Compose waits for a healthy SQL Server instead of the API retrying, so a real outage is not hidden, and I added an open
`/health` endpoint. I checked it with `docker compose ps`, `/health`, `whoami` (it prints `app`), and the web app working against the container.

Details: `docs/docker.md`.

## How I used AI

I used Claude Code as a pair programmer, one small step at a time. For each step I read the change, ran the tests and tried it myself before committing.

Most of my time went into planning: working out a suitable architecture, settling it, and only then starting the development, small step by small step,
checking each one myself. I started each task by asking for a plan and reading it against the brief, not by asking for code, and a few decisions (the at-risk
window, ties going to the lowest id) came out of arguing with it. When I was blocked I asked it to explain the existing code, and I let it run the builds, tests
and Docker commands so I could show the output. Nothing counted as done until I could show it: a test that failed first, a response from the running API, a screen in
the browser, a line of the build log. That caught mistakes, including a claim in my own write-up that was wrong when I checked it against the code. The details are
in the README under *AI usage*.
