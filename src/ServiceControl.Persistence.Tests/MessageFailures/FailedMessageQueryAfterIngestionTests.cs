namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

class FailedMessageQueryAfterIngestionTests : PersistenceTestBase
{
    [Test]
    public async Task Ingested_failure_is_returned_by_the_query_store()
    {
        var failure = new IngestedFailure
        {
            ExceptionSource = "MyCompany.Sales.Handlers",
            ExceptionStackTrace = "   at MyCompany.Sales.Handlers.OrderPlacedHandler.Handle()"
        };

        await Ingest(failure);

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(), new SortInfo());

        var view = result.Results.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(view.MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(view.MessageType, Is.EqualTo(failure.MessageType));
            Assert.That(view.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(view.TimeSent, Is.EqualTo(failure.TimeSent));
            Assert.That(view.TimeOfFailure, Is.EqualTo(failure.TimeOfFailure));
            Assert.That(view.NumberOfProcessingAttempts, Is.EqualTo(1));
            Assert.That(view.SendingEndpoint.Name, Is.EqualTo(failure.SendingEndpoint.Name));
            Assert.That(view.ReceivingEndpoint.Name, Is.EqualTo(failure.ReceivingEndpoint.Name));
            Assert.That(view.Exception.ExceptionType, Is.EqualTo(failure.ExceptionType));
            Assert.That(view.Exception.Message, Is.EqualTo(failure.ExceptionMessage));
            Assert.That(view.Exception.Source, Is.EqualTo(failure.ExceptionSource));
            Assert.That(view.Exception.StackTrace, Is.EqualTo(failure.ExceptionStackTrace));
        }
    }

    [Test]
    public async Task Ingestion_stores_the_failing_endpoint_address()
    {
        var failure = new IngestedFailure { FailingEndpointAddress = "Sales@MACHINE" };

        await Ingest(failure);

        var view = await FailedMessageQueryStore.GetLatestFailedMessageView(failure.UniqueMessageIdString);

        Assert.That(view.QueueAddress, Is.EqualTo("Sales@MACHINE"));
    }

    [Test]
    public async Task Ingested_failure_can_be_filtered_by_its_failing_endpoint_address()
    {
        var matching = new IngestedFailure { FailingEndpointAddress = "Sales@MACHINE" };
        var other = new IngestedFailure { FailingEndpointAddress = "Billing@MACHINE" };

        await Ingest(matching, other);

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, "Sales@MACHINE", new PagingInfo(), new SortInfo());

        Assert.That(
            result.Results.Select(view => view.Id),
            Is.EqualTo(new[] { matching.UniqueMessageIdString }));
    }

    [Test]
    public async Task Ingested_failure_is_returned_by_id()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.UniqueMessageId, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(message.ProcessingAttempts.Last().MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(
                message.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint,
                Is.EqualTo(failure.QueueAddress));
            Assert.That(message.FailureGroups.Select(group => group.Id), Is.EquivalentTo(failure.Groups.Select(group => group.Id)));
        }
    }

    [Test]
    public async Task Repeated_failures_are_counted_as_attempts()
    {
        var failure = new IngestedFailure();
        var secondAttempt = failure.NextAttempt(failure.AttemptedAt.AddMinutes(5));

        await Ingest(failure);
        await Ingest(secondAttempt);

        var view = await FailedMessageQueryStore.GetLatestFailedMessageView(failure.UniqueMessageIdString);
        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.NumberOfProcessingAttempts, Is.EqualTo(2));
            Assert.That(message.ProcessingAttempts, Has.Count.EqualTo(2));
        }
    }

    async Task Ingest(params IngestedFailure[] failures)
    {
        await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
        {
            foreach (var failure in failures)
            {
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            }

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        }

        await CompleteDatabaseOperation();
    }
}
