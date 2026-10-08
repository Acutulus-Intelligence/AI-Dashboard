namespace Application.Services;

/// <summary>
/// Case-insensitive schema names with conservative fuzzy repair for unique
/// near-misses (typos, compact forms like <c>soldat</c> → <c>sold_at</c>).
/// Ambiguous matches are rejected so the model is not guessed into the wrong column.
/// </summary>
internal sealed class SchemaIdentifierSet
{
    private readonly Dictionary<string, string> _canonical = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _compact = new(StringComparer.OrdinalIgnoreCase);

    public SchemaIdentifierSet(IEnumerable<string> names)
    {
        foreach (var name in names)
            Add(name);
    }

    public int Count => _canonical.Count;

    public IEnumerable<string> CanonicalNames => _canonical.Values;

    public SchemaIdentifierSet With(IEnumerable<string> extra)
    {
        var copy = new SchemaIdentifierSet(_canonical.Values);
        foreach (var name in extra)
            copy.Add(name);
        return copy;
    }

    public void Add(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = SqlSchemaGrounder.Unquote(name.Trim());
        if (trimmed.Length == 0) return;
        if (!_canonical.ContainsKey(trimmed))
            _canonical[trimmed] = trimmed;
        var compact = Compact(trimmed);
        if (compact.Length > 0 && !_compact.ContainsKey(compact))
            _compact[compact] = trimmed;
    }

    public bool Contains(string name) =>
        _canonical.ContainsKey(SqlSchemaGrounder.Unquote(name.Trim()));

    public bool TryCanonical(string name, out string canonical) =>
        _canonical.TryGetValue(SqlSchemaGrounder.Unquote(name.Trim()), out canonical!);

    public bool TryResolve(string name, out string canonical)
    {
        if (TryCanonical(name, out canonical))
            return true;
        return TryFuzzy(name, out canonical);
    }

    public bool TryFuzzy(string name, out string canonical)
    {
        canonical = string.Empty;
        var trimmed = SqlSchemaGrounder.Unquote(name.Trim());
        if (trimmed.Length == 0) return false;

        if (_compact.TryGetValue(Compact(trimmed), out canonical!))
            return true;

        string? best = null;
        var bestDistance = int.MaxValue;
        var ties = 0;
        foreach (var candidate in _canonical.Values)
        {
            var distance = Levenshtein(trimmed.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (!IsCloseEnough(trimmed.Length, candidate.Length, distance))
                continue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
                ties = 1;
            }
            else if (distance == bestDistance)
            {
                ties++;
            }
        }

        if (best is null || ties != 1) return false;
        canonical = best;
        return true;
    }

    public string FormatAllowed() =>
        string.Join(", ", _canonical.Values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).Take(40));

    private static string Compact(string value) =>
        new([.. value.Where(c => c is not '_' and not '-' and not ' ')]);

    private static bool IsCloseEnough(int fromLength, int toLength, int distance)
    {
        if (distance <= 0) return true;
        var maxLen = Math.Max(fromLength, toLength);
        if (maxLen < 4) return false;
        if (distance == 1) return true;
        // Transpositions like categroy/category are distance 2; only allow on longer names.
        return distance == 2 && maxLen >= 7;
    }

    private static int Levenshtein(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (var j = 0; j <= m; j++) prev[j] = j;

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }
}
