namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

public class FailedAuditImportEntity
{
    public Guid Id { get; set; }
    public string MessageJson { get; set; } = null!;
    public string? ExceptionInfo { get; set; }
}
