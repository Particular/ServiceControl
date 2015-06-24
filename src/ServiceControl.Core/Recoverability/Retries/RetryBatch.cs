namespace ServiceControl.Recoverability.Retries
{
    public class RetryBatch
    {
        public string Id { get; set; }
        public RetryBatchStatus Status { get; set; }

        public static string MakeId(string uniqueId)
        {
            return "Retries/" + uniqueId;
        }
    }
}