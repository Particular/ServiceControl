namespace ServiceControl.LoadTests.Messages
{
    using NServiceBus;

    [TimeToBeReceived("00:00:10")]
    public class QueueLengthReport : IMessage
    {
        public string Queue { get; set; }
        public string Machine { get; set; }
        public int Length { get; set; }
    }
}