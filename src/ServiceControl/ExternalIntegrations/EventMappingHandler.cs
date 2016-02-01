namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    public class EventMappingHandler : IHandleMessages<IEvent>
    {
        public IDocumentSession Session { get; set; }
        public IEnumerable<IEventPublisher> EventPublishers { get; set; }

        public Task Handle(IEvent message, IMessageHandlerContext context)
        {
            var dispatchContexts = EventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message));

            foreach (var dispatchContext in dispatchContexts)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Storing dispatch request.");
                }
                var dispatchRequest = new ExternalIntegrationDispatchRequest
                {
                    DispatchContext = dispatchContext
                };
                Session.Store(dispatchRequest);    
            }

            return Task.Delay(0);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventMappingHandler));
    }
}