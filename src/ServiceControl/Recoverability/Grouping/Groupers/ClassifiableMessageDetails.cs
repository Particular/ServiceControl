namespace ServiceControl.Recoverability
{
    using System.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public struct ClassifiableMessageDetails
    {
        public FailureDetails Details { get; }
        public string MessageType { get; }

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