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

*(to be written)*

## How I used AI

*(to be written at the end: tools, what worked, what I changed or rejected, and what I verified)*
