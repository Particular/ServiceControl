namespace ServiceControl.Infrastructure.DomainEvents
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    class DomainEvents : IDomainEvents
    {
        readonly IServiceProvider serviceProvider;
        public DomainEvents(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

        public async Task Raise<T>(T domainEvent) where T : IDomainEvent
        {
            var handlers = serviceProvider.GetServices<IDomainHandler<T>>();
            foreach (var handler in handlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }

            var ieventHandlers = serviceProvider.GetServices<IDomainHandler<IDomainEvent>>();
            foreach (var handler in ieventHandlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }
        }
    }
}