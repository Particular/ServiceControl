namespace ServiceControl.Infrastructure.DomainEvents
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Logging;

    public class DomainEvents : IDomainEvents
    {
        static readonly ILog Log = LogManager.GetLogger<DomainEvents>();

        readonly IServiceProvider serviceProvider;
        public DomainEvents(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

        public async Task Raise<T>(T domainEvent, CancellationToken cancellationToken) where T : IDomainEvent
        {
            var handlers = serviceProvider.GetServices<IDomainHandler<T>>();
            foreach (var handler in handlers)
            {
                try
                {
                    await handler.Handle(domainEvent, cancellationToken);
                }
                catch (Exception e)
                {
                    Log.Error($"Unexpected error publishing domain event {typeof(T)}", e);
                    throw;
                }
            }

            var ieventHandlers = serviceProvider.GetServices<IDomainHandler<IDomainEvent>>();
            foreach (var handler in ieventHandlers)
            {
                try
                {
                    await handler.Handle(domainEvent, cancellationToken);
                }
                catch (Exception e)
                {
                    Log.Error($"Unexpected error publishing domain event {typeof(T)}", e);
                    throw;
                }
            }
        }
    }
}