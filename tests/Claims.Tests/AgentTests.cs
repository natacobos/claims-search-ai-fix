namespace Claims.Tests;

// Tests written by the AI agent for MYT-3108.
public class AgentTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());

    [Fact]
    public void Search_FindsClaimByDescription()
    {
        var ids = Repo().Search(1, "Dental cleaning").Select(c => c.Id);
        Assert.Equal(new long[] { 101 }, ids);
    }

    [Fact]
    public void Search_SortsByAmount()
    {
        var ids = Repo().Search(1, "City Pharmacy", "amount", "asc").Select(c => c.Id);
        Assert.Equal(new long[] { 104, 105 }, ids);
    }

    [Fact]
    public void Search_InjectionInText_ReturnsNothing()
    {
        Assert.Empty(Repo().Search(1, "' OR 1=1 --"));
    }

    // Was Search_ExactMatchOnly_BlocksWildcardEnumeration, which asserted Search(1, "dental") is empty.
    // That contradicts the product rule in FINDING.md (contains match, any case). Members may only see
    // their own claims anyway, so partial matching is not an enumeration risk.
    [Fact]
    public void Search_PartialMatch_FindsContainingClaims()
    {
        var ids = Repo().Search(1, "dental").Select(c => c.Id).OrderBy(x => x);
        Assert.Equal(new long[] { 101, 103 }, ids);
    }
}
