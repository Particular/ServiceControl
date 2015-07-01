namespace ServiceControl.Recoverability.Retries
{
    public class MessageFailureRetry
    {
        public const string CollectionName = "MessageFailureRetries";

        public string Id { get; set; }
        public string RetryBatchId { get; set; }
        public string FailureMessageId { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return CollectionName + "/" + messageUniqueId;
        }
    }
}