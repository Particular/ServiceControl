using System.Collections.Generic;

namespace Issue558Detector
{
    class FailedMessage
    {
        public string Id { get; set; }
        public List<ProcessingAttempt> ProcessingAttempts { get; set; }

        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }
    }

    public class ProcessingAttempt
    {
        public string MessageId { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public MessageMetadata MessageMetadata { get; set; }
    }

    public class MessageMetadata
    {
        public string MessageType { get; set; }
    }

    public class FailureDetails
    {
        public string AddressOfFailingEndpoint { get; set; }
    }

}