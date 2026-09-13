namespace Darhous.Backup.Local;

public enum BackupType
{
    Metadata,
    Full,
    Configuration,
}

internal static class BackupTypeNames
{
    public static string ToStorageName(this BackupType value) => value switch
    {
        BackupType.Metadata => "metadata",
        BackupType.Full => "full",
        BackupType.Configuration => "configuration",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static BackupType Parse(string value) => value switch
    {
        "metadata" => BackupType.Metadata,
        "full" => BackupType.Full,
        "configuration" => BackupType.Configuration,
        _ => throw new InvalidDataException($"Unsupported backup type '{value}'."),
    };
}
