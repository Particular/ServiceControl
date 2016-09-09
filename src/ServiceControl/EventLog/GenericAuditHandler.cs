namespace ServiceControl.EventLog
{
    using Contracts.EventLog;
    using NServiceBus;
    using Raven.Client;

    /// <summary>
    /// Only for events that have been defined (under EventLog\Definitions), a logentry item will 
    /// be saved in Raven and an event will be raised. 
    /// </summary>
    public class GenericAuditHandler : IHandleMessages<IEvent>
    {
        static string[] emptyArray = new string[0];
        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly EventLogMappings mappings;

        public GenericAuditHandler(IBus bus, IDocumentStore store, EventLogMappings mappings)
        {
            this.bus = bus;
            this.store = store;
            this.mappings = mappings;
        }

        public void Handle(IEvent message)
        {
            //to prevent a infinite loop
            if (message is EventLogItemAdded)
            {
                return;
            }
            if (!mappings.HasMapping(message))
            {
                return;
            }

            var messageId = bus.GetMessageHeader(message, Headers.MessageId);
            var logItem = mappings.ApplyMapping(messageId, message);

            using (var session = store.OpenSession())
            {
                session.Store(logItem);
                session.SaveChanges();
            }

            bus.Publish(new EventLogItemAdded
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
            });
        }
    }
}