namespace ServiceControl.Recoverability
{
    using System.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;
    using static ServiceControl.MessageFailures.FailedMessage;

    public struct ClassifiableMessageDetails
    {
        public ProcessingAttempt ProcessingAttempt { get; private set; }
        public FailureDetails Details { get; private set; }
        public string MessageType { get; private set; }

        public ClassifiableMessageDetails(FailedMessage message)
        {
            ProcessingAttempt = message.ProcessingAttempts.Last();

            Details = ProcessingAttempt.FailureDetails;
            MessageType = (string)ProcessingAttempt.MessageMetadata["MessageType"];
        }

        public ClassifiableMessageDetails(string messageType, FailureDetails failureDetails)
        {
            Details = failureDetails;
            ProcessingAttempt = null;
            MessageType = messageType;
        }

        public ClassifiableMessageDetails(string messageType, FailureDetails failureDetails, ProcessingAttempt processingAttempt)
        {
            Details = failureDetails;
            ProcessingAttempt = processingAttempt;
            MessageType = messageType;
        }
    }
}