using System.Text;

namespace Application.Datasets;

/// <summary>Normalizes a dataset name into a safe SQL identifier for the generated table.</summary>
public static class DatasetTableName
{
    public static string Sanitize(string name)
    {
        var sb = new StringBuilder();
        var seen = false;
        foreach (var ch in name)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
                seen = true;
            }
            else if (seen)
            {
                sb.Append('_');
            }
        }

        var result = sb.ToString().TrimEnd('_');
        if (result.Length == 0)
            return "data";

        if (char.IsDigit(result[0]))
            result = "_" + result;

        return result;
    }
}