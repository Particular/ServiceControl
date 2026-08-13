namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class ProcessingAttemptTrimmingTests : RavenPersistenceTestBase
{
    const int MaxStoredAttempts = 10;
    const int IngestedAttempts = 15;

    [Test]
    public async Task Only_the_latest_attempts_are_kept()
    {
        var failure = new IngestedFailure();

        for (var i = 0; i < IngestedAttempts; i++)
        {
            await Ingest(failure.NextAttempt(failure.AttemptedAt.AddMinutes(i)));
        }

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.ProcessingAttempts, Has.Count.EqualTo(MaxStoredAttempts));
            Assert.That(
                message.ProcessingAttempts.Select(attempt => attempt.AttemptedAt),
                Is.EqualTo(Enumerable
                    .Range(IngestedAttempts - MaxStoredAttempts, MaxStoredAttempts)
                    .Select(i => failure.AttemptedAt.AddMinutes(i))));
        }
    }

    async Task Ingest(IngestedFailure failure)
    {
        await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
        {
            await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        }

        await CompleteDatabaseOperation();
    }
}
