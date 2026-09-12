using System.Text.RegularExpressions;

namespace Darhous.Archive.Modules.Plugins.Versioning;

/// <summary>Plugin SDK §12 — MAJOR.MINOR.PATCH only; no pre-release/build-metadata suffixes are used anywhere in this app's manifests, so that's all this needs to parse.</summary>
public readonly partial record struct SemVer(int Major, int Minor, int Patch) : IComparable<SemVer>
{
    public static bool TryParse(string value, out SemVer version)
    {
        var match = VersionRegex().Match(value);
        if (!match.Success)
        {
            version = default;
            return false;
        }

        version = new SemVer(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        return true;
    }

    public static SemVer Parse(string value) =>
        TryParse(value, out var version) ? version : throw new FormatException($"'{value}' is not a valid MAJOR.MINOR.PATCH version.");

    public int CompareTo(SemVer other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;
    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex VersionRegex();
}
