namespace ServiceControl.Recoverability.Retries
{
    using System;

    public class RetryBatch
    {
        public string Id { get; set; }
        public DateTimeOffset Started { get; set; }
        public RetryBatchStatus Status { get; set; }

        public static string MakeId(string uniqueId)
        {
            return "Retries/" + uniqueId;
        }
    }
}