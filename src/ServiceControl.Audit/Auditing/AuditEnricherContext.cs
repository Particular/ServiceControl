namespace ServiceControl.Audit.Auditing
{
    using NServiceBus;
    using System.Collections.Generic;

    class AuditEnricherContext
    {
        public AuditEnricherContext(IReadOnlyDictionary<string, string> headers, IList<IEvent> outgoingEvents, IDictionary<string, object> metadata)
        {
            Headers = headers;
            this.outgoingEvents = outgoingEvents;
            Metadata = metadata;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IDictionary<string, object> Metadata { get; }

        public void AddForPublish(IEvent @event)
        {
            outgoingEvents.Add(@event);
        }

        IList<IEvent> outgoingEvents;
    }
}