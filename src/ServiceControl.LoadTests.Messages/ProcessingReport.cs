namespace ServiceControl.LoadTests.Messages
{
    using NServiceBus;

    [TimeToBeReceived("00:00:10")]
    public class ProcessingReport : IMessage
    {
        public string HostId { get; set; }
        public string AuditQueue { get; set; }
        public long Audits { get; set; }
    }
}