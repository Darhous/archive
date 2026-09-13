namespace Darhous.Backup.Local.Jobs;

internal sealed record BackupJobPayload(BackupType Type, string DestinationDirectory, Guid? RequestedBy);
