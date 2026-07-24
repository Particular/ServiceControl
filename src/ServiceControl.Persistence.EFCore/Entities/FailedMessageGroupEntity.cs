namespace ServiceControl.Persistence.EFCore.Entities;

// Association between a failed message and the failure groups it belongs to. The rows for a
// message are replaced wholesale on every processing attempt.
public class FailedMessageGroupEntity
{
    public Guid FailedMessageUniqueId { get; set; }

    public required string GroupId { get; set; }

    public required string Title { get; set; }

    public required string Type { get; set; }
}
