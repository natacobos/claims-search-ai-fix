# MYT-3108 notes

## What the agent proposed

The agent parameterized the member id, search text, and page offset, which does close the
original direct SQL injection where those values were spliced into the query as raw strings.
However, three parts of its fix were wrong or incomplete against the product rules in
FINDING.md. First, `sortBy` was only blacklist-sanitized (stripping quotes and semicolons) and
then spliced straight into the `ORDER BY` clause as a raw column name, instead of being checked
against the allowed values `date`, `amount`, `status`. That still lets an attacker pass an
arbitrary SQL expression such as a subquery through sortBy, and it never throws the required
`ArgumentException` for bad input. Second, the agent dropped the `%...%` wildcards when it
parameterized the search text, turning "contains" search into exact-match search, which breaks
the product requirement that `dental` finds "Dental cleaning". Third, as "defense in depth" the
agent stripped apostrophes from the search text, which breaks the explicit requirement that
`O'Brien` must find "O'Brien Eye Clinic" — and is unnecessary once the value is parameterized.

## What I changed and why

I kept the parameterization but fixed the regressions. `sortBy` is now matched against an
allowlist (`date` to `submitted_at`, `amount`, `status`) with a `switch` expression that throws
`ArgumentException` for anything else, closing the second injection point in `ORDER BY` and
satisfying the sort rule in FINDING.md. The search text is wrapped with `%...%` again to restore
"contains" matching, and the apostrophe stripping was removed since parameterization already
makes the value safe as data, so `O'Brien` can match again. I also escaped literal `%` and `_`
characters in the user's text (with a matching `ESCAPE '\'` clause) so a search like `100%`
matches only claims that really contain a percent sign, instead of `%` acting as a SQL wildcard.

## Evidence

`dotnet test tests/Claims.Tests` passes all of `MyTests.cs` and all but one of the agent's own
tests; the one failure (`Search_ExactMatchOnly_BlocksWildcardEnumeration`) is expected, since it
asserted the broken exact-match behavior that FINDING.md says should not exist. `dotnet run
--project check` reports T1 Sorting 4/4, T2 Search 4/4, the "100%" extra check passes, and T3
reports my 4 tests all pass on my code and all 4 catch the agent's version. I also added
`VulnerabilityRegressionTests.cs`, which reproduces the exact pre-fix query from AGENT_NOTES.md
and confirms the finding was a real, exploitable bug: a crafted search string leaks member 2's
claim to member 1, and a stacked statement in the same field drops the claims table outright; a
separate test shows `sortBy` was an independent injection point via a subquery. All of those
exploits fail against the fixed `ClaimsRepository.Search`.
