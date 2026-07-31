namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Persistence.Infrastructure;

class EventLogWriterTests : PersistenceTestBase
{
    [Test]
    public async Task A_mapped_domain_event_is_persisted_and_can_be_read_back()
    {
        var writer = CreateWriter();

        await writer.Handle(new SomethingHappened { What = "it happened" }, CancellationToken.None);
        await CompleteDatabaseOperation();

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(1));
            Assert.That(result.Results.Single().Description, Is.EqualTo("it happened"));
            Assert.That(result.Results.Single().EventType, Is.EqualTo(nameof(SomethingHappened)));
        }
    }

    [Test]
    public async Task An_unmapped_domain_event_is_ignored()
    {
        var writer = CreateWriter();

        await writer.Handle(new NothingMapsThis(), CancellationToken.None);
        await CompleteDatabaseOperation();

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(result.QueryStats.TotalCount, Is.Zero, "only events with a mapping under EventLog\\Definitions are recorded");
    }

    AuditEventLogWriter CreateWriter() =>
        new(EventLogDataStore, new EventLogMappings([new SomethingHappenedDefinition()]));

    class SomethingHappened : IDomainEvent
    {
        public string What { get; set; }
    }

    class NothingMapsThis : IDomainEvent;

    class SomethingHappenedDefinition : EventLogMappingDefinition<SomethingHappened>
    {
        public SomethingHappenedDefinition() => Description(m => m.What);
    }
}
