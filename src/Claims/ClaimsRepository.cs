using Microsoft.Data.Sqlite;

namespace Claims;

public class ClaimsRepository
{
    private const int PageSize = 20;

    // MYT-3108: identifiers cannot be parameterized, so sort input is mapped through an allow-list.
    // User input never reaches the SQL text; only these fixed strings do.
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["date"] = "submitted_at",
        ["amount"] = "amount",
        ["status"] = "status",
    };

    private readonly SqliteConnection _db;

    public ClaimsRepository(SqliteConnection db) => _db = db;

    // Search the claims of one member. Called by the claims page of the member website and by the support tool.
    public IReadOnlyList<Claim> Search(int memberId, string? text, string? sortBy = "date", string? sortDir = "desc", int page = 0)
    {
        if (!SortColumns.TryGetValue(sortBy ?? "date", out var column))
            throw new ArgumentException("sortBy must be one of: date, amount, status.", nameof(sortBy));

        var direction = (sortDir ?? "desc").ToLowerInvariant() switch
        {
            "asc" => "ASC",
            "desc" => "DESC",
            _ => throw new ArgumentException("sortDir must be 'asc' or 'desc'.", nameof(sortDir)),
        };

        if (page < 0)
            throw new ArgumentOutOfRangeException(nameof(page), "page must be 0 or greater.");

        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT id, provider, description, amount, status, submitted_at FROM claims " +
            "WHERE member_id = @member " +
            "AND (LOWER(description) LIKE @text ESCAPE '\\' OR LOWER(provider) LIKE @text ESCAPE '\\') " +
            $"ORDER BY {column} {direction}, id {direction} LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@member", memberId);
        cmd.Parameters.AddWithValue("@text", "%" + EscapeLike((text ?? "").ToLowerInvariant()) + "%");
        cmd.Parameters.AddWithValue("@limit", PageSize);
        cmd.Parameters.AddWithValue("@offset", (long)page * PageSize);

        var results = new List<Claim>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(new Claim(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                                  reader.GetDouble(3), reader.GetString(4), reader.GetString(5)));
        return results;
    }

    // "Contains" search: the user's %, _ and \ are literal characters, not LIKE wildcards.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
