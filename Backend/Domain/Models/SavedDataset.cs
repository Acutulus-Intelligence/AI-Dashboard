namespace Domain.Models;

/// <summary>
/// User-uploaded tabular data (currently CSV; XLSX later). Rows are stored
/// row-major as a JSON string so the same shape can back charts without a
/// database connection.
/// </summary>
public class SavedDataset
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;

    /// <summary>Sanitized identifier used as the table name in generated SQL.</summary>
    public string TableName { get; set; } = "data";

    public string[] ColumnNames { get; set; } = [];

    /// <summary>Per column: "string" | "integer" | "number" | "boolean".</summary>
    public string[] ColumnTypes { get; set; } = [];

    /// <summary>JSON array of arrays (row-major string values).</summary>
    public string RowsJson { get; set; } = "[]";

    public int RowCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}