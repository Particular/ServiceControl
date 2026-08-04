namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;

    class RetryBatch
    {
        public string Id { get; set; }
        public string Context { get; set; }
        public string RetrySessionId { get; set; }
        public string StagingId { get; set; }
        public string Originator { get; set; }
        public string Classifier { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? Last { get; set; }
        public string RequestId { get; set; }
        public int InitialBatchSize { get; set; }
        public RetryType RetryType { get; set; }
        public RetryBatchStatus Status { get; set; }
        public IList<string> FailureRetries { get; set; } = [];
        public string InitiatedById { get; set; }
        public string InitiatedByName { get; set; }
        public string OperationId { get; set; }

        public Persistence.RetryBatch ToContract() => new()
        {
            Id = Id,
            Context = Context,
            StagingId = StagingId,
            Originator = Originator,
            Classifier = Classifier,
            StartTime = StartTime,
            Last = Last,
            RequestId = RequestId,
            InitialBatchSize = InitialBatchSize,
            RetryType = RetryType,
            Status = Status,
            MessageCount = FailureRetries.Count,
            InitiatedById = InitiatedById,
            InitiatedByName = InitiatedByName,
            OperationId = OperationId
        };
    }
}
