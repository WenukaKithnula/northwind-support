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

*(to be written)*

## Task 3: signing in

*(to be written)*

## Task 4: Docker

*(to be written)*

## How I used AI

*(to be written at the end: tools, what worked, what I changed or rejected, and what I verified)*
