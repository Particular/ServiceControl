namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Collections.Generic;
    using Autofac;
    using NServiceBus;

    public static class DomainEvents
    {
        public static IContainer Container { get; set; }

        public static void Raise<T>(T domainEvent) where T : IEvent
        {
            if (Container == null)
            {
                return;
            }

            IEnumerable<IDomainHandler<T>> handlers;
            Container.TryResolve(out handlers);
            foreach (var handler in handlers)
            {
                handler.Handle(domainEvent);
            }

            IEnumerable<IDomainHandler<IEvent>> ieventHandlers;
            Container.TryResolve(out ieventHandlers);
            foreach (var handler in ieventHandlers)
            {
                handler.Handle(domainEvent);
            }
        }
    }

    public interface IDomainHandler<in T> where T : IEvent
    {
        void Handle(T domainEvent);
    }
}
