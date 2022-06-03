namespace ServiceControl.EventLog
{
    using System.Threading.Tasks;
    using Contracts.EventLog;
    using Infrastructure.DomainEvents;
    using Raven.Client;

    /// <summary>
    /// Only for events that have been defined (under EventLog\Definitions), a logentry item will
    /// be saved in Raven and an event will be raised.
    /// </summary>
    class AuditEventLogWriter : IDomainHandler<IDomainEvent>
    {
        public AuditEventLogWriter(IDocumentStore store, EventLogMappings mappings, IDomainEvents domainEvents)
        {
            this.store = store;
            this.mappings = mappings;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(IDomainEvent message)
        {
            if (!mappings.HasMapping(message))
            {
                return;
            }

            var logItem = mappings.ApplyMapping(message);

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(logItem)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            await domainEvents.Raise(new EventLogItemAdded
            {
                RaisedAt = logItem.RaisedAt,
                Severity = logItem.Severity,
                Description = logItem.Description,
                Id = logItem.Id,
                Category = logItem.Category,
                // Yes this is on purpose.
                // The reason is because this data is not useful for end users, so for now we just empty it.
                // At the moment too much data is being populated in this field, and this has significant down sides to the amount of data we are sending down to ServicePulse (it actually crashes it).
                RelatedTo = emptyArray
            }).ConfigureAwait(false);
        }

        readonly IDocumentStore store;
        readonly EventLogMappings mappings;
        readonly IDomainEvents domainEvents;
        static string[] emptyArray = new string[0];
    }
}