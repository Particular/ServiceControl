namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.Recoverability;

/// <summary>
/// Persisted record of an in-progress archive or unarchive operation, enabling resume-after-crash.
/// A single row per (RequestId, ArchiveType, IsArchive) combination; the unique index prevents
/// duplicate concurrent operations.
/// </summary>
public class ArchiveOperationEntity
{
    /// <summary>Deterministic string PK (e.g. <c>ArchiveOperations/2/{groupId}</c>).</summary>
    public string Id { get; set; } = null!;

    /// <summary>The group id (or other request id) being archived/unarchived.</summary>
    public string RequestId { get; set; } = null!;

    /// <summary>Display name of the group, captured at operation start.</summary>
    public string GroupName { get; set; } = null!;

    /// <summary>The type of archive operation (FailureGroup, SingleMessage, etc.).</summary>
    public ArchiveType ArchiveType { get; set; }

    /// <summary>Distinguishes archive (<c>true</c>) from unarchive (<c>false</c>).</summary>
    public bool IsArchive { get; set; }

    /// <summary>Total number of messages in the group at operation start.</summary>
    public int TotalNumberOfMessages { get; set; }

    /// <summary>Number of messages processed so far (resume checkpoint).</summary>
    public int NumberOfMessagesProcessed { get; set; }

    /// <summary>Total number of batches planned.</summary>
    public int NumberOfBatches { get; set; }

    /// <summary>Current batch number (resume checkpoint, 0-based).</summary>
    public int CurrentBatch { get; set; }

    /// <summary>When the operation started (UTC).</summary>
    public DateTime Started { get; set; }

    /// <summary>Audit attribution: the id of the user who initiated the operation.</summary>
    public string? InitiatedById { get; set; }

    /// <summary>Audit attribution: the name of the user who initiated the operation.</summary>
    public string? InitiatedByName { get; set; }

    /// <summary>Audit attribution: the operation id used to correlate per-message audit entries.</summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Builds a deterministic primary key for the operation row.
    /// </summary>
    public static string MakeId(string requestId, ArchiveType archiveType, bool isArchive)
    {
        var prefix = isArchive ? "ArchiveOperations" : "UnarchiveOperations";
        return $"{prefix}/{(int)archiveType}/{requestId}";
    }
}