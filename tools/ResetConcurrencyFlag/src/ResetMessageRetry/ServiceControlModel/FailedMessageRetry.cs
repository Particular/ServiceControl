namespace ResetMessageRetry
{
    public class FailedMessageRetry
    {
        public static string MakeDocumentId(string messageUniqueId)
        {
            return "FailedMessageRetries/" + messageUniqueId;
        }

        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }
    }
}