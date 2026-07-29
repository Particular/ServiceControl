namespace ServiceControl.EventLog
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using ServiceControl.Persistence;

    /// <summary>
    /// Only for events with an <see cref="IEventLogMappingDefinition"/> registered via
    /// AddEventLogMapping is a log entry item persisted.
    /// </summary>
    class AuditEventLogWriter : IDomainHandler<IDomainEvent>
    {
        public AuditEventLogWriter(IEventLogDataStore dataStore, EventLogMappings mappings)
        {
            this.dataStore = dataStore;
            this.mappings = mappings;
        }

        public async Task Handle(IDomainEvent message, CancellationToken cancellationToken)
        {
            if (!mappings.HasMapping(message))
            {
                return;
            }

            var logItem = mappings.ApplyMapping(message);
            var eventId = Guid.CreateVersion7();

            await dataStore.Add(logItem, eventId);
        }

        readonly IEventLogDataStore dataStore;
        readonly EventLogMappings mappings;
    }
}
