namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Collections.Generic;
    using Autofac;
    using NServiceBus;

    public class DomainEvents : IDomainEvents
    {
        IContainer container;

        public void SetContainer(IContainer container)
        {
            this.container = container;
        }

        public void Raise<T>(T domainEvent) where T : IEvent
        {
            if (container == null)
            {
                return;
            }

            IEnumerable<IDomainHandler<T>> handlers;
            container.TryResolve(out handlers);
            foreach (var handler in handlers)
            {
                handler.Handle(domainEvent);
            }

            IEnumerable<IDomainHandler<IEvent>> ieventHandlers;
            container.TryResolve(out ieventHandlers);
            foreach (var handler in ieventHandlers)
            {
                handler.Handle(domainEvent);
            }
        }
    }

    public interface IDomainEvents
    {
        void Raise<T>(T domainEvent) where T : IEvent;
    }

    public interface IDomainHandler<in T> where T : IEvent
    {
        void Handle(T domainEvent);
    }
}
