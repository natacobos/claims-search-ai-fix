namespace Claims.Tests;

// T3: tests that pass on the fixed ClaimsRepository but fail on the agent's version.
public class MyTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());

    [Fact]
    public void Search_PartialTextIsCaseInsensitiveContains()
    {
        // FINDING.md: "dental" must find both "Dental cleaning" and "Dental X-ray".
        // The agent's version does exact matching only, so this returns empty there.
        var ids = Repo().Search(1, "dental").Select(c => c.Id).OrderBy(x => x);
        Assert.Equal(new long[] { 101, 103 }, ids);
    }

    [Fact]
    public void Search_ApostropheInTextStillMatches()
    {
        // FINDING.md: O'Brien must find "O'Brien Eye Clinic". The agent's version
        // strips apostrophes from the search text, so it never matches.
        var ids = Repo().Search(1, "O'Brien").Select(c => c.Id);
        Assert.Equal(new long[] { 102 }, ids);
    }

    [Fact]
    public void Search_InvalidSortBy_ThrowsArgumentException()
    {
        // FINDING.md: sortBy must be one of date/amount/status, anything else rejected.
        // The agent's version only strips quotes/semicolons and passes the rest straight
        // into the ORDER BY clause, so no ArgumentException is thrown here.
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "City Pharmacy", "amount, (SELECT name FROM members)"));
    }

    [Fact]
    public void Search_PercentInTextIsLiteralNotWildcard()
    {
        // A literal '%' in the search text must not act as a SQL wildcard.
        // Only the claim whose description really contains "100%" should match.
        var ids = Repo().Search(1, "100%").Select(c => c.Id);
        Assert.Equal(new long[] { 104 }, ids);
    }
}
