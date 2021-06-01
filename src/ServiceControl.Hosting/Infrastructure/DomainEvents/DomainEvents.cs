namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;

    public class DomainEvents : IDomainEvents
    {
        readonly IComponentContext container;
        public DomainEvents(IComponentContext container) => this.container = container;

        public async Task Raise<T>(T domainEvent) where T : IDomainEvent
        {
            container.TryResolve(out IEnumerable<IDomainHandler<T>> handlers);
            foreach (var handler in handlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }

            container.TryResolve(out IEnumerable<IDomainHandler<IDomainEvent>> ieventHandlers);
            foreach (var handler in ieventHandlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }
        }
    }
}