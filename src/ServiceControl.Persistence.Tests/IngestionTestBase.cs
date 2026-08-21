namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.UnitOfWork;

/// <summary>
/// Writes failures through the real ingestion path.
/// </summary>
abstract class IngestionTestBase : PersistenceTestBase
{
    protected async Task InBatch(Func<IIngestionUnitOfWork, Task> record)
    {
        await using var unitOfWork = await UnitOfWorkFactory.StartNew();

        await record(unitOfWork);

        await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
    }

    protected Task Ingest(params IngestedFailure[] failures) =>
        InBatch(async unitOfWork =>
        {
            foreach (var failure in failures)
            {
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            }
        });

    protected Task ConfirmRetry(params string[] uniqueMessageIds) => ConfirmRetryAt(Now, uniqueMessageIds);

    protected Task ConfirmRetryAt(DateTime succeededAt, params string[] uniqueMessageIds) =>
        InBatch(async unitOfWork =>
        {
            foreach (var uniqueMessageId in uniqueMessageIds)
            {
                await unitOfWork.Recoverability.RecordSuccessfulRetry(uniqueMessageId, succeededAt);
            }
        });
}
