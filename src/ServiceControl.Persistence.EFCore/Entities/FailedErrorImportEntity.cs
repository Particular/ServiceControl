namespace ServiceControl.Persistence.EFCore.Entities;

public class FailedErrorImportEntity
{
    public Guid UniqueMessageId { get; set; }

    public DateTime FailedAt { get; set; }

    public required string MessageId { get; set; }

    public required string HeadersJson { get; set; }

    // Holds the inline body, or an empty array when the body was spilled to external storage.
    // BodyStoredExternally, not the contents here, decides where the body lives.
    public required byte[] Body { get; set; }

    public bool BodyStoredExternally { get; set; }

    public required string ExceptionInfo { get; set; }

    public static string ExternalBodyId(Guid uniqueMessageId) => $"failedimport-{uniqueMessageId}";
}
