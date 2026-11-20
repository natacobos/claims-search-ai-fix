namespace Claims.Tests;

// MYT-3108: tests for the product rules in FINDING.md. Each one fails on the agent's version.
public class MyTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());

    [Theory]
    [InlineData("amount, (SELECT name FROM members)")]
    [InlineData("amount;DROP TABLE claims")]
    [InlineData("submitted_at")]
    [InlineData("(CASE WHEN (SELECT COUNT(*) FROM members) > 1 THEN amount ELSE status END)")]
    [InlineData("")]
    public void Search_RejectsSortByOutsideAllowList(string sortBy)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "City Pharmacy", sortBy, "asc"));
    }

    [Fact]
    public void Search_SortsByStatus()
    {
        var ids = Repo().Search(1, "City Pharmacy", "status", "asc").Select(c => c.Id);
        Assert.Equal(new long[] { 105, 104 }, ids);
    }

    [Fact]
    public void Search_ContainsMatch_AnyCase()
    {
        var ids = Repo().Search(1, "dental").Select(c => c.Id).OrderBy(x => x);
        Assert.Equal(new long[] { 101, 103 }, ids);
    }

    [Fact]
    public void Search_NameWithApostrophe_Works()
    {
        var ids = Repo().Search(1, "O'Brien").Select(c => c.Id);
        Assert.Equal(new long[] { 102 }, ids);
    }

    [Fact]
    public void Search_PercentIsLiteral_NotWildcard()
    {
        var ids = Repo().Search(1, "100%").Select(c => c.Id);
        Assert.Equal(new long[] { 104 }, ids);
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("%")]
    [InlineData("")]
    public void Search_NeverReturnsOtherMembersClaims(string text)
    {
        Assert.All(Repo().Search(1, text), c => Assert.True(c.Id < 200));
    }
}
