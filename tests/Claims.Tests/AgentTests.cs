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

    [Fact]
    public void Search_PartialMatch_FindsDental()
    {
        // MYT-3108 review: this test used to assert exact matching ("dental" finds nothing), which
        // contradicts FINDING.md. Partial search is a product rule; member isolation is the security control.
        var ids = Repo().Search(1, "dental").Select(c => c.Id).OrderBy(x => x);
        Assert.Equal(new long[] { 101, 103 }, ids);
    }
}
