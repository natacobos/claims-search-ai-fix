# MYT-3108 notes

## What the agent proposed

The agent turned the search text into a query parameter (good), but:

- **Sort is still injectable.** `sortBy` is still joined into `ORDER BY {column}`; the only "sanitizing" is removing `'` and `;`. Payloads without those characters still run as SQL, e.g. `amount, (SELECT name FROM members)` or `CASE WHEN (SELECT ...) THEN amount END`, which allows blind/boolean extraction from other tables. Any column (`submitted_at`, `member_id`) was accepted instead of rejected. A blocklist is the wrong control.
- **Apostrophes removed from the search text**, so `O'Brien` became `OBrien` and "O'Brien Eye Clinic" was no longer found. Not needed once the text is a parameter.
- **Exact match instead of "contains"**, so `dental` found nothing. This breaks the product rule and adds no security: the `member_id` filter is what isolates members.
- Its test `Search_ExactMatchOnly_BlocksWildcardEnumeration` locked in that wrong exact-match behavior, so it fails as soon as the search is fixed. Its 4 tests passed, but never tried a sort payload, an apostrophe or a partial search.

## What I changed and why

`src/Claims/ClaimsRepository.cs`:

- **Sort allow-list.** `ORDER BY` cannot take a parameter, so the column comes from a fixed map (`date → submitted_at`, `amount`, `status`, case-sensitive). Anything else, including `submitted_at` itself, throws `ArgumentException`. User input never reaches the SQL text.
- **Sort direction allow-list.** Only `asc`/`desc` (null means desc); anything else throws `ArgumentException` instead of being silently replaced.
- **"Contains" search restored.** Pattern is `%text%`, bound as a parameter. Apostrophes are kept: `O'Brien` works.
- **LIKE wildcards escaped.** `\`, `%`, `_` in the text are escaped and the query uses `ESCAPE '\'`, so `100%` matches only the claim that really contains "100%".
- **Stable paging.** `ORDER BY ..., id` so ties don't shuffle between pages; negative `page` throws `ArgumentOutOfRangeException`.

Tests:

- `MyTests.cs`: 13 tests (partial match, apostrophe, sort and direction payloads rejected, valid sorts, literal `%`, member isolation). All pass on my code and all fail on the agent's version.
- `AgentTests.cs`: rewrote the trap test as `Search_PartialMatch_FindsDental` (expects 101 and 103), because it asserted behavior that FINDING.md forbids and would block the pipeline.

Caveat for production: SQLite's LIKE ignores case for ASCII letters. On SQL Server, case-insensitivity depends on the column collation; confirm it is a `_CI_` collation there.

## Evidence

`dotnet test tests/Claims.Tests`: 17 passed, 0 failed.

`dotnet run --project check`:

```
T1 Sorting: 4/4 checks pass
T2 Search: 4/4 checks pass
   Extra: "100%" finds only the claim that really contains 100%: yes
T3 Your tests: 13 tests | pass on your code: 13/13 | catch the agent's version: 13 of 13
The agent's tests on your code: 4 passed, 0 failed
Check files: c0cd69d4
```

Payloads rejected with `ArgumentException`: `amount, (SELECT name FROM members)`, `amount;DROP TABLE claims`, `CASE WHEN 1=1 THEN amount END`, `submitted_at`, `member_id`, `Amount`, and sort directions `ASC; DROP TABLE claims` and `sideways`. Searching `' OR 1=1 --` returns nothing, and an empty search returns only member 1's claims (101–105).
