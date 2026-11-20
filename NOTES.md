# MYT-3108 notes

## What the agent proposed

The agent turned the search text and offset into parameters, which was correct. But it still put
`sortBy` straight into `ORDER BY` and only removed quotes and semicolons. That does not stop
injection: `amount, (SELECT name FROM members)` or a `CASE WHEN (SELECT ...)` expression has no
quotes or semicolons, still runs, and can leak data through the sort order (blind SQL injection).
`submitted_at` and any other column name were also accepted. It also broke product rules: it removed
apostrophes, so `O'Brien` found nothing, and it searched for the exact text instead of "contains",
so `dental` found nothing. Its test `Search_ExactMatchOnly_BlocksWildcardEnumeration` locked in that
broken behavior. Its injection test passed only because exact matching found no rows, not because
the query was safe.

## What I changed and why

- `sortBy` goes through an allow-list (`date` -> `submitted_at`, `amount`, `status`, any case).
  Anything else throws `ArgumentException`. Column names cannot be parameters, so the SQL only ever
  holds these fixed strings, never user input.
- `sortDir` must be `asc` or `desc` (null means desc). Anything else throws `ArgumentException`.
  `page` below 0 is rejected. `id` is added as a tie-breaker so paging stays stable.
- The search text is a parameter wrapped in `%...%` (contains). Apostrophes are left alone, because
  the parameter handles them safely. `%`, `_` and `\` are escaped with `ESCAPE '\'`, so they are
  plain characters (`100%` finds only "Copay 100% covered"). `LOWER()` on both sides makes the
  match case-insensitive no matter the database collation (SQL Server in production).
- The page size (20) and offset are parameters.
- I replaced the agent's exact-match test, which contradicted FINDING.md, with a test for the
  contains rule.

## Evidence

- `dotnet test tests/Claims.Tests`: 16 passed, 0 failed.
- `MyTests.cs` covers sort injection payloads that are rejected, sort by status, contains/any case,
  `O'Brien`, `100%` as a literal, and no claims from other members for `' OR 1=1 --`, `%` and empty
  text. 8 of these 12 fail on the agent's version: the sort injection payloads, `dental`, `O'Brien`
  and `100%`. The other 4 are regression guards that the agent's version also passes.
- `dotnet run --project check`: T1 4/4, T2 4/4, `100%` extra: yes, T3 12/12 pass and catch the
  agent's version in 8 of 12, T4 3/3. Check files fingerprint `c0cd69d4`.
