namespace Darhous.Archive.Contracts.Documents;

/// <summary>DB Spec §24 (Document Status).</summary>
public enum DocumentStatus
{
    Active,
    Processing,
    NeedsReview,
    NeedsOcr,
    IndexFailed,
    Missing,
    Trashed,
    Quarantined,
}

/// <summary>DB Spec §25 (Source Type).</summary>
public enum DocumentSourceType
{
    Scan,
    Import,
    WatchFolder,
    Manual,
    Outlook,
    Plugin,
    Migration,
}

/// <summary>DB Spec §26 (Storage Mode).</summary>
public enum DocumentStorageMode
{
    Managed,
    IndexedInPlace,
}

/// <summary>DB Spec §30 (Availability Status).</summary>
public enum DocumentAvailabilityStatus
{
    Available,
    Missing,
    Corrupt,
    Offline,
}
