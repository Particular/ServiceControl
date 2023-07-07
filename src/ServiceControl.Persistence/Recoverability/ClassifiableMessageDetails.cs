namespace ServiceControl.Recoverability
{
    using System.Linq;
    using Contracts.Operations;
    using MessageFailures;
    using static MessageFailures.FailedMessage;

    public struct ClassifiableMessageDetails
    {
        public ProcessingAttempt ProcessingAttempt { get; }
        public FailureDetails Details { get; }
        public string MessageType { get; }

        public ClassifiableMessageDetails(FailedMessage message)
        {
            ProcessingAttempt = message.ProcessingAttempts.Last();

            Details = ProcessingAttempt.FailureDetails;
            MessageType = (string)ProcessingAttempt.MessageMetadata["MessageType"];
        }

        public ClassifiableMessageDetails(string messageType, FailureDetails failureDetails, ProcessingAttempt processingAttempt)
        {
            Details = failureDetails;
            ProcessingAttempt = processingAttempt;
            MessageType = messageType;
        }
    }
}