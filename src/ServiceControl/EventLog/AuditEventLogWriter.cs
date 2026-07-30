namespace ServiceControl.EventLog
{
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using ServiceControl.Persistence;

    /// <summary>
    /// Only for events that have been defined (under EventLog\Definitions), a logentry item will
    /// be saved in Raven.
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

            await dataStore.Add(logItem);
        }

        readonly IEventLogDataStore dataStore;
        readonly EventLogMappings mappings;
    }
}
