namespace Domain.Models;

/// <summary>
/// Structured (non-SQL) query over an uploaded collection file. Used instead of
/// SQL for data collections: the AI returns filters/aggregations/order rather
/// than a SELECT, and the executor applies them in-memory over stored rows.
/// </summary>
public class DataQueryModel
{
    public List<DataFilter> Filters { get; set; } = [];
    public List<string> GroupBy { get; set; } = [];
    public List<DataAggregation> Aggregations { get; set; } = [];
    public List<DataOrderBy> OrderBy { get; set; } = [];
    public int? Limit { get; set; }
}

public class DataFilter
{
    public string Column { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public string? Value { get; set; }
}

public class DataAggregation
{
    public string Column { get; set; } = string.Empty;
    public string Function { get; set; } = "sum";
}

public class DataOrderBy
{
    public string Column { get; set; } = string.Empty;
    public string Direction { get; set; } = "asc";
}