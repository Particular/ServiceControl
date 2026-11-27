namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Recoverability.ExternalIntegration;

[TestFixture]
[NonParallelizable]
class PendingIntegrationEventsMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task A_queued_event_is_copied_and_the_product_dispatches_it_as_the_type_it_was_raised_as()
    {
        var id = $"ExternalIntegrationDispatchRequests/{Guid.NewGuid()}";
        await SeedSource((id, new ExternalIntegrationDispatchRequest { DispatchContext = new FailedMessageArchivedPublisher.DispatchContext { FailedMessageId = "FailedMessages/1" } }));

        var result = await CopyCategory("PendingIntegrationEvents");

        object[] dispatched = [];
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            dispatched = contexts;
            return Task.CompletedTask;
        });
        await ExternalIntegrationStore.DispatchNow();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(dispatched.Single(), Is.InstanceOf<FailedMessageArchivedPublisher.DispatchContext>());
            Assert.That(((FailedMessageArchivedPublisher.DispatchContext)dispatched.Single()).FailedMessageId, Is.EqualTo("FailedMessages/1"));
        }
    }
}
