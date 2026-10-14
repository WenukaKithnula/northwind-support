# Approach

How I worked on each task, and why. The details, screenshots and checks are in `docs/`, and the
decisions and limitations are in the README.

I take one small step at a time, check it, and only then commit it, so the history follows the order I did
things in.

## Task 1: the defect and the filtering

**The defect.** I reproduced it before reading any code. In the Network tab, changing Status sent
nothing, and typing in the search box then sent the old status. That told me the value was being stored and
the request was not being sent, so I followed the value from the dropdown to the effect that loads the
tickets. Its dependency array held three of the eleven filter fields, and a lint suppression was hiding the
warning about it.

I did not add the missing fields to the array. The suggestion lint gives (add `load`) would have made the
effect run after every render, in a loop, and the debounce wrapped every change, not just typing. So I
changed what drives the request: the list is loaded for one set of applied filters, any change to it sends a
request at once, and only the text typed in the search box waits 300 ms. I wrote the tests first, to assert
the request that is sent, and checked that they fail on the original hook (9 of 17 do) before fixing it.

Details: `docs/defect-117.md`.

**The filtering.** The API read the filter values and ignored them: `priority=High` still returned all 40
tickets. I looked at where the list query is built and added one step, `ApplyFilters`, between building the
query and counting it, with one `Where` per filter that was supplied. Putting it before the count is what
makes `totalCount` and the pager right, and since each filter is one `if`, the SLA filter in Task 2 is one
more.

I wrote the tests first and ran them on a real in-memory SQLite database instead of a mock, because a mock
would pass whatever the query looked like. Sixteen of the twenty failed before the change. Then I checked
the generated SQL on the real SQL Server, to see the `WHERE` in both the count and the paged query, and
compared the API's counts with direct counts in the database.

Details: `docs/filtering.md`.

## Task 2: triage and escalation

The brief said the challenge was where the rules go, so I started there. Each rule became a small class
with no database or web code in it: the SLA windows (`SlaPolicy`), the priority rule, the choice of owner,
and `Ticket.Escalate`. One service, `TicketTriage`, brings them together, so raising a ticket and
escalating one use exactly the same logic. That let me write and test the rules, with the brief's worked
examples as acceptance tests, before there was any endpoint or screen.

After that I went in this order, committing each step: the rules and their tests, the history table and its
migration, the create and escalate endpoints, the SLA filter in SQL, then the screens, then the write-up. I
ran the real API and read the SQL it generated after each backend step, because the brief says it will be
read.

The places where the brief left room, I decided and wrote down: how the at-risk window is worked out
(from the policy for the priority and tier, because due date minus created date is wrong once a ticket has
been escalated), who keeps a ticket that is already at an agent's limit (its own agent, because an escalation
adds no load), how equal agents are split (the lowest id), and what happens when nobody is eligible (the
ticket is created unassigned with a reason, and the same after an escalation). Each is in the README under
"Design decisions".

The SLA filter was the one place the rules had to exist twice, once in C# for the status a row shows and once
in SQL for the filter. So the main test builds about 580 tickets either side of every boundary and checks the
filter selects exactly the rows that show each status. I checked it on the real SQL Server as well as SQLite,
because the two can translate a query differently.

For the screens, the rules went into plain functions (`escalationRules.ts`) so that no component decides
anything, and I used the browser to check each flow against the running API before writing it up.

Details: `docs/escalation.md`.

## Task 3: signing in

I began with what the brief asked to be true, not with the library: a request without a valid token must
fail everywhere, and the person recorded on an escalation must be the person who was signed in. Then I
worked backwards to the smallest design that makes both hard to get wrong.

**Where the credential lives.** Every user is an agent and there are no roles, so a password hash on
`Agents` is enough. A users table would have meant keeping the same person in two places. I used ASP.NET
Identity's hasher rather than writing anything of my own, behind a small interface so the application layer
does not know about it.

**Closing everything.** The risky mistake is forgetting to protect one endpoint, so I did not protect
endpoints one by one. A fallback authorization policy closes all of them and login opts out. I called all
eleven without a token to check, and a test does the same through the real pipeline.

**Login.** Four things can go wrong (no such email, wrong password, inactive, no password) and they get one
answer. The password is checked even when there is no agent, so the response time says nothing either. I
wrote the tests for those four cases before the handler.

**The token.** A JWT with the agent's id, name and email, an hour long, signed with a key from
configuration. The app refuses to start without a key of at least 32 bytes, because a weak key fails
silently: logins work and anyone can forge tokens. The start-up check makes the mistake loud.

**Who escalated.** I removed `escalatedBy` from the request and read the name from the token. The form no
longer asks, and it says who it will be recorded as.

**The web app.** A session store in `sessionStorage` that clears itself at expiry, a guard that remembers
the page the agent wanted, and a client that clears the session on a 401. Signing out only clears the
session and lets the guard do the redirect, instead of navigating from two places. I checked each flow in
the browser, including a token I had edited by hand.

Details: `docs/authentication.md`.

## Task 4: Docker

The brief gives the checks up front (a clean `up`, a cached restore, the size, `whoami`), so I worked
backwards from them and treated each as something I could prove with a command.

**The Dockerfile.** Restore is the slow, rarely-changing step and source is the fast, always-changing one, so
the Dockerfile copies the project files and the three shared build files first and restores, and only then
copies the source. I proved it instead of trusting it: I changed one `.cs` line and read the build log, where
the restore step said `CACHED`. The runtime stage is the ASP.NET image running as its built-in `app` user.

**Configuration.** Nothing goes into the image. The risky part is not the secrets I know about but the ones
that arrive by accident, such as the Development settings file that holds a SQL password and a signing
key, so `.dockerignore` keeps that file out and I checked `docker history` and the files in the image.

**Production, not Development.** If the container ran as Development it would pass every check and be wrong:
it would have Swagger and a development key. Running it as Production turned out to be the better test,
because the API refuses to start unless the key and connection string are supplied, which shows the image
really holds no configuration. That meant migrate-and-seed could not depend on the environment being
Development, so it got its own flag.

**The database.** Compose waits for SQL Server to be healthy before it starts the API, instead of the API
retrying in a loop, so a real outage is not hidden. For a real deployment I would run migrations as a
separate step, for the reasons in the README.

**Health.** `/health` reports database reachability, and I made it the only anonymous endpoint besides
login, opened where it is mapped so the fallback policy still closes everything else.

Details: `docs/docker.md`.

## How I used AI

I used Claude Code throughout, and the way I used it changed what I trusted. I started each task by asking for a plan
and reading it against the brief, not by asking for code: where the design came from, which rules go in which class,
what the checklist in the brief would look at. That made me argue with the plan before any code existed, and a few
decisions (the at-risk window, ties going to the lowest id, the escalated ticket not counting against its own agent)
came out of that argument and are written down in the README.

When I was blocked, I asked it to explain the existing code instead of to change it, then made the change myself or
reviewed it line by line. For the commands (builds, tests, the live API, Docker) I let it run them, because the point
was the output, which I could then show.

The rule I held it to was that nothing counts as done until I can show it: a test that failed first, a response from
the running API, a screen in the browser, a line of the build log. Several times that caught a mistake in what it had
written, including a claim in my own write-up that turned out to be wrong when I checked it against the code.

The details (what I accepted, what I rejected, the bugs and how they were found) are in the README under *AI usage*.
