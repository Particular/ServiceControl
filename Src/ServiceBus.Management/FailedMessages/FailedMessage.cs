namespace ServiceBus.Management.FailedMessages
{
    using System.Collections.Generic;

    public class FailedMessage
    {
        public string Id { get; set; }

        public string MessageType { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public string Body { get; set; }

        public byte[] BodyRaw { get; set; }

        public string RelatedToMessageId { get; set; }

        public string CorrelationId { get; set; }

        public int NumberOfTimesFailed { get; set; }

        public FailedMessageStatus Status { get; set; }
    }
}