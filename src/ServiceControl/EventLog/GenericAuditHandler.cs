namespace ServiceControl.EventLog
{
    using System.Threading.Tasks;
    using Contracts.EventLog;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    /// <summary>
    /// Only for events that have been defined (under EventLog\Definitions), a logentry item will 
    /// be saved in Raven and an event will be raised. 
    /// </summary>
    public class GenericAuditHandler : IHandleMessages<IEvent>
    {
        public EventLogMappings EventLogMappings { get; set; }
        public IDocumentSession Session { get; set; }

        public async Task Handle(IEvent message, IMessageHandlerContext context)
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

            Logger.InfoFormat("Event: {0} emitted", message.GetType().Name);
            var messageId = context.MessageHeaders[Headers.MessageId];
            var logItem = EventLogMappings.ApplyMapping(messageId, message);

            Session.Store(logItem);

            await context.Publish<EventLogItemAdded>(m =>
            {
                m.RaisedAt = logItem.RaisedAt;
                m.Severity = logItem.Severity;
                m.Description = logItem.Description;
                m.Id = logItem.Id;
                m.Category = logItem.Category;
                m.RelatedTo = logItem.RelatedTo;
            });

            
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(GenericAuditHandler));
    }
}