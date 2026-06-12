namespace Domain.Models;

public class TableSchema
{
    public string TableName { get; set; } = string.Empty;
    public List<ColumnSchema> Columns { get; set; } = [];
}

public class ColumnSchema
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
}
