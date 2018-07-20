namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;

    public class DomainEvents : IDomainEvents
    {
        public async Task Raise<T>(T domainEvent) where T : IDomainEvent
        {
            if (container == null)
            {
                return;
            }

            IEnumerable<IDomainHandler<T>> handlers;
            container.TryResolve(out handlers);
            foreach (var handler in handlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }

            IEnumerable<IDomainHandler<IDomainEvent>> ieventHandlers;
            container.TryResolve(out ieventHandlers);
            foreach (var handler in ieventHandlers)
            {
                await handler.Handle(domainEvent)
                    .ConfigureAwait(false);
            }
        }

        public void SetContainer(IContainer container)
        {
            this.container = container;
        }

        IContainer container;
    }
}