namespace ServiceControl.Recoverability
{
    using System.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public class ClassifiableMessageDetails
    {
        public FailureDetails Details { get; set; }
        public object MessageType { get; set; }

        public ClassifiableMessageDetails()
        { }

        public ClassifiableMessageDetails(FailedMessage message)
        {
            var last = message.ProcessingAttempts.Last();

            Details = last.FailureDetails;
            MessageType = last.MessageMetadata["MessageType"];
        }
    }
}