namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence.EFCore.Entities;

static class RetryBatchMapper
{
    public static RetryBatch ToRetryBatch(this RetryBatchEntity entity, IList<string> failureRetries) =>
        new()
        {
            Id = entity.Id.ToString(),
            Status = entity.Status,
            RetrySessionId = entity.RetrySessionId,
            RequestId = entity.RequestId,
            RetryType = entity.RetryType,
            InitialBatchSize = entity.InitialBatchSize,
            StartTime = entity.StartTime,
            Last = entity.Last,
            StagingId = entity.StagingId,
            Context = entity.Context,
            Originator = entity.Originator,
            Classifier = entity.Classifier,
            InitiatedById = entity.InitiatedById,
            InitiatedByName = entity.InitiatedByName,
            OperationId = entity.OperationId,
            FailureRetries = failureRetries
        };
}
