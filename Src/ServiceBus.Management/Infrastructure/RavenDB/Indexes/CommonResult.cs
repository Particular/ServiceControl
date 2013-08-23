namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System;
    using MessageAuditing;

    public class CommonResult
    {
        public string Id { get; set; }
        public string ReceivingEndpointName { get; set; }
        public string MessageType { get; set; }
        public MessageStatus Status { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime TimeOfFailure { get; set; }
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}