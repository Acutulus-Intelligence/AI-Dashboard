using System.Globalization;

namespace Application.Datasets;

/// <summary>Infers a coarse column type from the uploaded string values.</summary>
public static class DatasetTypeInferrer
{
    public static string Infer(string[] values)
    {
        var sample = values.Where(v => !string.IsNullOrWhiteSpace(v)).Take(50).ToArray();
        if (sample.Length == 0)
            return "string";

        if (sample.All(IsInteger))
            return "integer";

        if (sample.All(IsNumber))
            return "number";

        if (sample.All(IsBoolean))
            return "boolean";

        return "string";
    }

    private static bool IsInteger(string value) =>
        long.TryParse(value.Trim(), out _);

    private static bool IsNumber(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out _);

    private static bool IsBoolean(string value)
    {
        var v = value.Trim();
        return v is "true" or "false" or "TRUE" or "FALSE" or "True" or "False" or "0" or "1";
    }
}