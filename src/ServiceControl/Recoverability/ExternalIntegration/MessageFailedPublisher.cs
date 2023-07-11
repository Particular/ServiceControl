namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using ExternalIntegrations;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence;

    class MessageFailedPublisher : EventPublisher<MessageFailed, MessageFailedPublisher.DispatchContext>
    {
        readonly IServiceProvider serviceProvider;

        public MessageFailedPublisher(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override DispatchContext CreateDispatchRequest(MessageFailed @event)
        {
            return new DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override async Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts)
        {
            // TODO: MessageFailedPublisher is registered as a singleton so it cannot take a dependency on IErrorMessageDataStore.
            // Alternative is to have a IErrorMessageDataStore as an argument and have it flow with the `PublishEvents` except
            // that created coupling between the the publisher and the subscriber which likely is unwanted.

            using (var scope = serviceProvider.CreateScope())
            {
                var dataStore = scope.ServiceProvider.GetRequiredService<IErrorMessageDataStore>();

                var ids = contexts.Select(x => x.FailedMessageId).ToArray();
                var results = await dataStore.FailedMessagesFetch(ids)
                    .ConfigureAwait(false);
                return results.Select(x => x.ToEvent());
            }
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
        }
    }
}