using Microsoft.Data.Sqlite;

namespace Claims;

public class ClaimsRepository
{
    private readonly SqliteConnection _db;

    private const int PageSize = 20;

    // MYT-3108: ORDER BY cannot be parameterized, so the column comes from this allow-list only.
    // Keys are the public sort options; values are SQL fragments we own. User input never reaches the SQL text.
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.Ordinal)
    {
        ["date"] = "submitted_at",
        ["amount"] = "amount",
        ["status"] = "status",
    };

    public ClaimsRepository(SqliteConnection db) => _db = db;

    // Search the claims of one member. Called by the claims page of the member website and by the support tool.
    public IReadOnlyList<Claim> Search(int memberId, string? text, string? sortBy = "date", string? sortDir = "desc", int page = 0)
    {
        if (!SortColumns.TryGetValue(sortBy ?? "date", out var column))
            throw new ArgumentException("Invalid sort option. Use date, amount or status.", nameof(sortBy));
        var direction = (sortDir ?? "desc") switch
        {
            "asc" => "ASC",
            "desc" => "DESC",
            _ => throw new ArgumentException("Invalid sort direction. Use asc or desc.", nameof(sortDir)),
        };
        if (page < 0)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be zero or greater.");

        // "Contains" search. The text is a bound parameter (apostrophes are safe); LIKE wildcards typed
        // by the user (% and _) are escaped so they match literally.
        var pattern = "%" + EscapeLike(text ?? "") + "%";

        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT id, provider, description, amount, status, submitted_at FROM claims " +
            "WHERE member_id = @member AND (description LIKE @text ESCAPE '\\' OR provider LIKE @text ESCAPE '\\') " +
            $"ORDER BY {column} {direction}, id LIMIT {PageSize} OFFSET @offset";
        cmd.Parameters.AddWithValue("@member", memberId);
        cmd.Parameters.AddWithValue("@text", pattern);
        cmd.Parameters.AddWithValue("@offset", page * PageSize);

        var results = new List<Claim>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(new Claim(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                                  reader.GetDouble(3), reader.GetString(4), reader.GetString(5)));
        return results;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
