namespace ServiceControl.Recoverability
{
    public class FailedMessageRetry
    {
        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return CollectionName + "/" + messageUniqueId;
        }

        public const string CollectionName = "FailedMessageRetries";
    }
}