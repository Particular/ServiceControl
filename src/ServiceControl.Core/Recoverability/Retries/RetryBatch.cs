namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Collections.Generic;

    public class RetryBatch
    {
        public string Id { get; set; }
        public DateTimeOffset Started { get; set; }
        public RetryBatchStatus Status { get; set; }

        public IList<string> FailureRetries { get; set; }

        public RetryBatch()
        {
            FailureRetries = new List<string>();
        }

        public static string MakeId(string uniqueId)
        {
            return "RetryBatches/" + uniqueId;
        }
    }
}