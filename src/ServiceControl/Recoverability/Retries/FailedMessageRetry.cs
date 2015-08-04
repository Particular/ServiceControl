namespace ServiceControl.Recoverability
{
    public class FailedMessageRetry
    {
        public const string CollectionName = "FailedMessageRetries";

        public static string MakeDocumentId(string messageUniqueId)
        {
            return CollectionName + "/" + messageUniqueId;
        }

        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }
    }
}