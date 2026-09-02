namespace ServiceControl.MessageFailures
{
    using Persistence.Infrastructure;

    public class QueueAddress : IVersionedRow
    {
        public string? PhysicalAddress { get; set; }
        public int FailedMessageCount { get; set; }
        object?[] IVersionedRow.GetVersionFields() => [PhysicalAddress, FailedMessageCount];
    }
}