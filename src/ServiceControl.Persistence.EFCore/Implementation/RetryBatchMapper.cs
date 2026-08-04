namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence.EFCore.Entities;

static class RetryBatchMapper
{
    public static RetryBatch ToRetryBatch(this RetryBatchEntity entity, int messageCount) =>
        new()
        {
            Id = entity.Id.ToString(),
            Status = entity.Status,
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
            MessageCount = messageCount
        };
}
