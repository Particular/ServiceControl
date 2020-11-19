namespace ServiceControl.Recoverability
{
    using System.Linq;

    public class FailedMessageRetry
    {
        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }
        public int StageAttempts { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return CollectionName + "/" + messageUniqueId.Split('/').Last();
        }

        public const string CollectionName = "FailedMessageRetries";
    }
}