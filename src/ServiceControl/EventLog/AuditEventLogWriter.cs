namespace ServiceControl.EventLog
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.EventLog;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;
    using ServiceControl.Persistence;

    /// <summary>
    /// Only for events that have been defined (under EventLog\Definitions), a logentry item will
    /// be saved in Raven and an event will be raised.
    /// </summary>
    class AuditEventLogWriter : IDomainHandler<IDomainEvent>
    {
        public AuditEventLogWriter(GlobalEventHandler broadcaster, IErrorMessageDataStore dataStore, EventLogMappings mappings)
        {
            this.broadcaster = broadcaster;
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

            await dataStore.StoreEventLogItem(logItem);

            await broadcaster.Broadcast(new EventLogItemAdded
            {
                RaisedAt = logItem.RaisedAt,
                Severity = logItem.Severity,
                Description = logItem.Description,
                Id = logItem.Id,
                Category = logItem.Category,
                // Yes this is on purpose.
                // The reason is because this data is not useful for end users, so for now we just empty it.
                // At the moment too much data is being populated in this field, and this has significant down sides to the amount of data we are sending down to ServicePulse (it actually crashes it).
                RelatedTo = []
            }, cancellationToken);
        }

        readonly GlobalEventHandler broadcaster;
        readonly IErrorMessageDataStore dataStore;
        readonly EventLogMappings mappings;
    }
}