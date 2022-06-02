namespace ServiceControl.Infrastructure.DomainEvents
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    public class DomainEvents : IDomainEvents
    {
        readonly IServiceProvider container;
        public DomainEvents(IServiceProvider container) => this.container = container;

        public async Task Raise<T>(T domainEvent) where T : IDomainEvent
        {
            var handlers = container.GetServices<IDomainHandler<T>>();
            foreach (var handler in handlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }

            var ieventHandlers = container.GetServices<IDomainHandler<IDomainEvent>>();
            foreach (var handler in ieventHandlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }
        }
    }
}