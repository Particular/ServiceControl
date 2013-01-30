namespace ServiceBus.Management.FailedMessages
{
    using System.Collections.Generic;

    public class FailedMessage
    {
        public string Id { get; set; }

        public string MessageType { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public string BodyText { get; set; }

        public byte[] BodyRaw { get; set; }

        public string MessageId { get; set; }

        public string IdForCorrelation { get; set; }

        public string RelatedToMessageId { get; set; }
    }
}