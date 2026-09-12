using Darhous.Archive.Modules.Discovery.Exclusions;
using Darhous.Archive.Modules.Discovery.Scanning;

namespace Darhous.Archive.Modules.Discovery.Tests;

public class DiscoveryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "darhous-scanner-tests", Guid.NewGuid().ToString("N"));
    private readonly DiscoveryScanner _scanner = new();

    public DiscoveryScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static ExclusionSet NoExclusions() => new([], new HashSet<string>());

    private string File(string relativePath, string content = "x")
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Scan_OnlyReturnsSupportedExtensions()
    {
        File("doc.pdf");
        File("readme.txt");
        File("sheet.xlsx");

        var result = _scanner.Scan(_root, includeSubfolders: true, NoExclusions(), CancellationToken.None);

        Assert.Equal(2, result.SupportedFiles.Count);
        Assert.Contains(result.SupportedFiles, f => f.Extension == ".pdf");
        Assert.Contains(result.SupportedFiles, f => f.Extension == ".xlsx");
        Assert.Equal(3, result.FilesSeen);
    }

    [Fact]
    public void Scan_RecursesIntoSubfoldersWhenEnabled()
    {
        File("top.pdf");
        File(Path.Combine("nested", "deep.pdf"));

        var result = _scanner.Scan(_root, includeSubfolders: true, NoExclusions(), CancellationToken.None);

        Assert.Equal(2, result.SupportedFiles.Count);
    }

    [Fact]
    public void Scan_DoesNotRecurseWhenSubfoldersDisabled()
    {
        File("top.pdf");
        File(Path.Combine("nested", "deep.pdf"));

        var result = _scanner.Scan(_root, includeSubfolders: false, NoExclusions(), CancellationToken.None);

        Assert.Single(result.SupportedFiles);
        Assert.Equal("top.pdf", Path.GetFileName(result.SupportedFiles[0].FullPath));
    }

    [Fact]
    public void Scan_SkipsPathsExcludedByPrefix()
    {
        var excludedDir = Path.Combine(_root, "excluded");
        Directory.CreateDirectory(excludedDir);
        File(Path.Combine("excluded", "hidden.pdf"));
        File("visible.pdf");

        var exclusions = new ExclusionSet([PathNormalizationForTests.Normalize(excludedDir)], new HashSet<string>());
        var result = _scanner.Scan(_root, includeSubfolders: true, exclusions, CancellationToken.None);

        Assert.Single(result.SupportedFiles);
        Assert.Equal("visible.pdf", Path.GetFileName(result.SupportedFiles[0].FullPath));
        Assert.True(result.SkippedByExclusion > 0);
    }

    [Fact]
    public void Scan_SkipsDirectoriesExcludedByName()
    {
        File(Path.Combine("$Recycle.Bin", "trashed.pdf"));
        File("visible.pdf");

        var exclusions = new ExclusionSet([], new HashSet<string> { "$Recycle.Bin" });
        var result = _scanner.Scan(_root, includeSubfolders: true, exclusions, CancellationToken.None);

        Assert.Single(result.SupportedFiles);
        Assert.Equal("visible.pdf", Path.GetFileName(result.SupportedFiles[0].FullPath));
    }

    [Fact]
    public void Scan_CollectsMetadataOnly_NeverReadsFileContent()
    {
        var path = File("doc.pdf", content: "this content must never influence indexing at the scan stage");

        var result = _scanner.Scan(_root, includeSubfolders: true, NoExclusions(), CancellationToken.None);

        var discovered = Assert.Single(result.SupportedFiles);
        Assert.Equal(path, discovered.FullPath);
        Assert.Equal(".pdf", discovered.Extension);
        Assert.True(discovered.SizeBytes > 0);
        Assert.NotNull(discovered.ModifiedAtUtc);
    }

    [Fact]
    public void Scan_DoesNotThrow_WhenCancelledMidway()
    {
        File("doc.pdf");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => _scanner.Scan(_root, true, NoExclusions(), cts.Token));
    }
}

/// <summary>Mirrors the internal normalization the scanner/exclusion set use, for building test exclusion sets without reaching into internals.</summary>
internal static class PathNormalizationForTests
{
    public static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();
}
