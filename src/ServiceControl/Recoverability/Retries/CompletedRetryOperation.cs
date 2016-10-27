namespace ServiceControl.Recoverability
{
    public class CompletedRetryOperation
    {
        public string Id { get; set; }
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }

        public static string MakeDocumentId(string requestId, RetryType retryType)
        {
            return $"CompletedRetryBatches/{retryType}/{requestId}";
        }
    }
}