namespace ServiceControl.Recoverability
{
    public class MessageTypeFailureClassifier : IFailureClassifier
    {
        public const string Id = "Message Type";
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failureDetails)
        {
            return failureDetails.MessageType.ToString();
        }
    }
}