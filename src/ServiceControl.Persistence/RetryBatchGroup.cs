namespace ServiceControl.Persistence
{
    using System;

    public class RetryBatchGroup
    {
        public string RequestId { get; set; }

        public RetryType RetryType { get; set; }

        public bool HasStagingBatches { get; set; }

        public bool HasForwardingBatches { get; set; }

        public int InitialBatchSize { get; set; }

        public string Originator { get; set; }

        public string Classifier { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime Last { get; set; }
    }
}