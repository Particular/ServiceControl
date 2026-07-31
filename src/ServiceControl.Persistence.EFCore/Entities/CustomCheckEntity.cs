namespace ServiceControl.Persistence.EFCore.Entities;

public class CustomCheckEntity
{
    public required Guid Id { get; set; }
    public required string CustomCheckId { get; set; }
    public required string Category { get; set; }
    public Status Status { get; set; }
    public DateTime ReportedAt { get; set; }
    public string? FailureReason { get; set; }

    public required string OriginatingEndpointName { get; set; }

    public Guid OriginatingEndpointHostId { get; set; }

    public required string OriginatingEndpointHost { get; set; }
}