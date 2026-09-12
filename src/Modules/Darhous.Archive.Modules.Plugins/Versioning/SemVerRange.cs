namespace Darhous.Archive.Modules.Plugins.Versioning;

/// <summary>
/// Plugin SDK §42 range syntax: space-separated comparator clauses, ANDed together —
/// e.g. <c>">=1.0.0 &lt;2.0.0"</c>. Deliberately not full npm-style range syntax (no
/// <c>^</c>/<c>~</c>/OR ranges) — the spec's own example only ever needs this much, and a
/// bigger parser would be speculative complexity nothing in the manifest schema asks for.
/// </summary>
public static class SemVerRange
{
    public static bool IsSatisfiedBy(string range, SemVer version)
    {
        foreach (var clause in range.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryEvaluateClause(clause, version))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryEvaluateClause(string clause, SemVer version)
    {
        (string Op, int Length)[] operators = [(">=", 2), ("<=", 2), (">", 1), ("<", 1), ("=", 1)];

        foreach (var (op, length) in operators)
        {
            if (!clause.StartsWith(op, StringComparison.Ordinal))
            {
                continue;
            }

            var versionPart = clause[length..];
            if (!SemVer.TryParse(versionPart, out var boundary))
            {
                throw new FormatException($"Invalid version in range clause '{clause}'.");
            }

            return op switch
            {
                ">=" => version >= boundary,
                "<=" => version <= boundary,
                ">" => version > boundary,
                "<" => version < boundary,
                "=" => version.CompareTo(boundary) == 0,
                _ => false,
            };
        }

        throw new FormatException($"Invalid range clause '{clause}' — expected a comparator (>=, <=, >, <, =).");
    }
}
