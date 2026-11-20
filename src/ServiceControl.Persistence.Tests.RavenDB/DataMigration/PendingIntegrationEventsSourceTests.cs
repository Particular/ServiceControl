namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.DataMigration;

class PendingIntegrationEventsSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_each_queued_request_with_its_context_as_the_concrete_type()
    {
        await ExternalIntegrationStore.StoreDispatchRequest(
        [
            new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "one" } },
            new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "two" } }
        ]);
        await CompleteDatabaseOperation();

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.PendingIntegrationEvents);

        await using var source = await OpenMigrationSource();
        var contexts = (await CollectBatches(source, category)).SelectMany(batch => batch.Rows).Select(row => ((ExternalIntegrationDispatchRequest)row.Document).DispatchContext).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(contexts, Has.All.InstanceOf<TestDispatchContext>(), "an untyped context would serialize as the JSON library's own type and never dispatch");
            Assert.That(contexts.Cast<TestDispatchContext>().Select(context => context.Value), Is.EquivalentTo(new[] { "one", "two" }));
            Assert.That(await source.Count(category), Is.EqualTo(2));
        }
    }

    public class TestDispatchContext
    {
        public string Value { get; set; }
    }
}
