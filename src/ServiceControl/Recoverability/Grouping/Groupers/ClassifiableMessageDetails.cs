namespace ServiceControl.Recoverability
{
    using System.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public struct ClassifiableMessageDetails
    {
        public FailureDetails Details { get; private set; }
        public string MessageType { get; private set; }

        public ClassifiableMessageDetails(FailedMessage message)
        {
            var last = message.ProcessingAttempts.Last();

            Details = last.FailureDetails;
            MessageType = (string)last.MessageMetadata["MessageType"];
        }

        public ClassifiableMessageDetails(string messageType, FailureDetails failureDetails)
        {
            Details = failureDetails;

            MessageType = messageType;
        }
    }
}