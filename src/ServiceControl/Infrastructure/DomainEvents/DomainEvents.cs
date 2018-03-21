namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Collections.Generic;
    using Autofac;

    public class DomainEvents : IDomainEvents
    {
        IContainer container;

        public void SetContainer(IContainer container)
        {
            this.container = container;
        }

        public void Raise<T>(T domainEvent) where T : IDomainEvent
        {
            if (container == null)
            {
                return;
            }

            var domainEventType = domainEvent.GetType();
            var enumerableOfDomainHandlers = typeof(IEnumerable<>).MakeGenericType(typeof(IDomainHandler<>).MakeGenericType(domainEventType));

            object outHandlers;
            container.TryResolve(enumerableOfDomainHandlers, out outHandlers);
            foreach (var handler in (IEnumerable<dynamic>)outHandlers)
            {
                handler.Handle((dynamic) domainEvent);
            }

            IEnumerable<IDomainHandler<IDomainEvent>> ieventHandlers;
            container.TryResolve(out ieventHandlers);
            foreach (var handler in ieventHandlers)
            {
                handler.Handle(domainEvent);
            }
        }
    }
}
