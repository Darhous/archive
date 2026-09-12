namespace Darhous.Archive.Workers.Protocol;

/// <summary>
/// Payload for <see cref="WorkerMessageTypes.LargeDataReference"/>. The large content is
/// stored at <see cref="Path"/> and is deliberately not embedded as Base64 in IPC JSON.
/// </summary>
public sealed record LargeDataReference(string Path);

public sealed class ManagedTempPathValidator
{
    private readonly string _rootWithSeparator;
    private readonly StringComparison _pathComparison;

    public ManagedTempPathValidator(WorkerProtocolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ManagedTempStorageRoot);

        var normalizedRoot = Path.GetFullPath(options.ManagedTempStorageRoot);
        _rootWithSeparator = Path.EndsInDirectorySeparator(normalizedRoot)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public bool IsWithinManagedTempRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return normalizedPath.StartsWith(_rootWithSeparator, _pathComparison);
    }

    public string ValidateAndNormalize(string path)
    {
        if (!IsWithinManagedTempRoot(path))
        {
            throw new WorkerProtocolException(
                $"Large-data path '{path}' is outside the configured managed temp-storage root.");
        }

        return Path.GetFullPath(path);
    }
}
