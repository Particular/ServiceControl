namespace ServiceControl.Recoverability
{
    public class MessageTypeFailureClassifier : IFailureClassifier
    {
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            return failureDetails.MessageType;
        }

        public const string Id = "Message Type";
    }
}