namespace ServiceControl.Persistence.EFCore.Entities;

public class FailedMessageRetryEntity
{
    public Guid UniqueMessageId { get; set; }

    public string? RetryId { get; set; }
}
