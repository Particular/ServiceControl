namespace ServiceControl.Audit.Persistence.EFCore.Entities;

public class FailedAuditImportEntity
{
    public Guid UniqueMessageId { get; set; }
    public DateTime FailedAt { get; set; }
    public string? MessageId { get; set; }
    public required string HeadersJson { get; set; }
    public required byte[] Body { get; set; }
    public string? ExceptionInfo { get; set; }
}
