namespace Darhous.Backup.Local;

/// <summary>
/// The live state may already have changed and automatic rollback was unavailable or failed.
/// The caller must stop normal application use and recover from <see cref="SafetyBackupPath"/>.
/// </summary>
public sealed class RestoreRecoveryRequiredException(
    string message,
    string safetyBackupPath,
    Exception innerException) : Exception(message, innerException)
{
    public string SafetyBackupPath { get; } = safetyBackupPath;
}
