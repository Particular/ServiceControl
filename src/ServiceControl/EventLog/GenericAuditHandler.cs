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
        public EventLogMappings EventLogMappings { get; set; }
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        static string[] EmptyArray = new string[0];

        public void Handle(IEvent message)
        {
            //to prevent a infinite loop
            if (message is EventLogItemAdded)
            {
                return;
            }
            if (!EventLogMappings.HasMapping(message))
            {
                return;
            }

            var messageId = Bus.GetMessageHeader(message, Headers.MessageId);
            var logItem = EventLogMappings.ApplyMapping(messageId, message);

            Session.Store(logItem);

            Bus.Publish<EventLogItemAdded>(m =>
            {
                m.RaisedAt = logItem.RaisedAt;
                m.Severity = logItem.Severity;
                m.Description = logItem.Description;
                m.Id = logItem.Id;
                m.Category = logItem.Category;
                // Yes this is on purpose.
                // The reason is because this data is not useful for end users, so for now we just empty it.
                // At the moment too much data is being populated in this field, and this has significant down sides to the amount of data we are sending down to ServicePulse (it actually crashes it).
                m.RelatedTo = EmptyArray; 

            });
        }
    }
}