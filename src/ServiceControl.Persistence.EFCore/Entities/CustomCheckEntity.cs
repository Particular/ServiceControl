namespace ServiceControl.Persistence.EFCore.Entities;

public class CustomCheckEntity
{
    public required Guid Id { get; set; }
    public string CustomCheckId { get; set; }
    public string Category { get; set; }
    public Status Status { get; set; }
    public DateTime ReportedAt { get; set; }
    public string? FailureReason { get; set; }

    public string OriginatingEndpointName { get; set; }

    public Guid OriginatingEndpointHostId { get; set; }

    public string  OriginatingEndpointHost { get; set; }
}