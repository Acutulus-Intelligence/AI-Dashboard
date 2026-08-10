using System.Text.Json;
using Domain.Models;

namespace Application.Datasets;

public static class DatasetRows
{
    public static List<string[]> Decode(SavedDataset dataset)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string[]>>(dataset.RowsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}