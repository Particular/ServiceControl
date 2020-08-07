namespace ServiceControl.Recoverability
{
    public class FailedMessageRetry
    {
        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }
        public int StageAttempts { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return CollectionName + "/" + messageUniqueId;
        }

        public static string MakeDocumentIdFromFailedMessageId(string failedMessageId)
        {
            return CollectionName + "/" + failedMessageId.Substring(ServiceControl.MessageFailures.FailedMessage.CollectionName.Length + 1);
        }

        public const string CollectionName = "FailedMessageRetries";
    }
}