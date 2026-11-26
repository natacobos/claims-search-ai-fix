using Microsoft.Data.Sqlite;

namespace Claims;

public class ClaimsRepository
{
    private readonly SqliteConnection _db;

    public ClaimsRepository(SqliteConnection db) => _db = db;

    // Search the claims of one member. Called by the claims page of the member website and by the support tool.
    public IReadOnlyList<Claim> Search(int memberId, string? text, string? sortBy = "date", string? sortDir = "desc", int page = 0)
    {
        // MYT-3108: search text and column are both query parameters; sortBy is allowlisted.
        var column = (sortBy ?? "date") switch
        {
            "date" => "submitted_at",
            "amount" => "amount",
            "status" => "status",
            _ => throw new ArgumentException($"Invalid sortBy: {sortBy}", nameof(sortBy))
        };
        var direction = sortDir?.ToLowerInvariant() == "asc" ? "ASC" : "DESC";

        // Escape LIKE wildcards in the user's text so '%' and '_' are matched literally,
        // then wrap with '%' to get "contains" matching.
        var escaped = (text ?? "").Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var pattern = $"%{escaped}%";

        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT id, provider, description, amount, status, submitted_at FROM claims " +
            "WHERE member_id = @member AND (description LIKE @text ESCAPE '\\' OR provider LIKE @text ESCAPE '\\') " +
            $"ORDER BY {column} {direction} LIMIT 20 OFFSET @offset";
        cmd.Parameters.AddWithValue("@member", memberId);
        cmd.Parameters.AddWithValue("@text", pattern);
        cmd.Parameters.AddWithValue("@offset", page * 20);

        var results = new List<Claim>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(new Claim(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                                  reader.GetDouble(3), reader.GetString(4), reader.GetString(5)));
        return results;
    }
}
