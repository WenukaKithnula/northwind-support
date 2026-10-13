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

**The filtering.** *(to be written when the filtering is done)*

## Task 2: triage and escalation

*(to be written)*

## Task 3: signing in

*(to be written)*

## Task 4: Docker

*(to be written)*

## How I used AI

*(to be written at the end: tools, what worked, what I changed or rejected, and what I verified)*
