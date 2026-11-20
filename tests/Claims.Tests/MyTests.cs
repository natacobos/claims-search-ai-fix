namespace Claims.Tests;

// MYT-3108: tests for the rules in FINDING.md. Each one fails on the agent's version.
public class MyTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());

    [Fact]
    public void Search_PartialMatch_IgnoresCase()
    {
        var ids = Repo().Search(1, "dental").Select(c => c.Id).OrderBy(x => x);
        Assert.Equal(new long[] { 101, 103 }, ids);
    }

    [Fact]
    public void Search_Apostrophe_FindsOBrien()
    {
        var ids = Repo().Search(1, "O'Brien").Select(c => c.Id);
        Assert.Equal(new long[] { 102 }, ids);
    }

    [Theory]
    [InlineData("amount, (SELECT name FROM members)")]
    [InlineData("amount;DROP TABLE claims")]
    [InlineData("CASE WHEN 1=1 THEN amount END")]
    [InlineData("submitted_at")]
    [InlineData("member_id")]
    [InlineData("Amount")]
    public void Sort_AnythingOutsideAllowList_IsRejected(string sortBy)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "pharmacy", sortBy, "asc"));
    }

    [Theory]
    [InlineData("ASC; DROP TABLE claims")]
    [InlineData("sideways")]
    public void SortDir_AnythingOtherThanAscOrDesc_IsRejected(string sortDir)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "pharmacy", "amount", sortDir));
    }

    [Fact]
    public void Sort_ValidOptions_WorkWithPartialSearch()
    {
        Assert.Equal(new long[] { 105, 104 }, Repo().Search(1, "pharmacy", "status", "asc").Select(c => c.Id));
        Assert.Equal(new long[] { 103, 101 }, Repo().Search(1, "smiles", "date", "desc").Select(c => c.Id));
        Assert.Equal(new long[] { 104, 105 }, Repo().Search(1, "pharmacy", "amount", "asc").Select(c => c.Id));
    }

    [Fact]
    public void Search_PercentIsLiteral()
    {
        var ids = Repo().Search(1, "100%").Select(c => c.Id);
        Assert.Equal(new long[] { 104 }, ids);
    }

    [Fact]
    public void Search_OnlyEverReturnsTheMembersOwnClaims()
    {
        var all = Repo().Search(1, "").Select(c => c.Id).OrderBy(x => x);
        Assert.Equal(new long[] { 101, 102, 103, 104, 105 }, all);
        Assert.Empty(Repo().Search(1, "' OR 1=1 --"));
        Assert.Empty(Repo().Search(1, "crown")); // member 2's claim
    }
}
