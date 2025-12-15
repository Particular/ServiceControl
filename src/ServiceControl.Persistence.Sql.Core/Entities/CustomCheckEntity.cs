namespace ServiceControl.Persistence.Sql.Core.Entities;

public class CustomCheckEntity
{
    public Guid Id { get; set; }
    public string CustomCheckId { get; set; } = null!;
    public string? Category { get; set; }
    public int Status { get; set; } // 0 = Pass, 1 = Fail
    public DateTime ReportedAt { get; set; }
    public string? FailureReason { get; set; }
    public string EndpointName { get; set; } = null!;
    public Guid HostId { get; set; }
    public string Host { get; set; } = null!;
}
