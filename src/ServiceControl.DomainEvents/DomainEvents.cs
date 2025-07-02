namespace ServiceControl.Infrastructure.DomainEvents
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class DomainEvents(IServiceProvider serviceProvider, ILogger<DomainEvents> logger) : IDomainEvents
    {
        public async Task Raise<T>(T domainEvent, CancellationToken cancellationToken) where T : IDomainEvent
        {
            var handlers = serviceProvider.GetServices<IDomainHandler<T>>();
            foreach (var handler in handlers)
            {
                try
                {
                    await handler.Handle(domainEvent, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Unexpected error publishing domain event {EventType}", typeof(T));
                    throw;
                }
            }

            var ieventHandlers = serviceProvider.GetServices<IDomainHandler<IDomainEvent>>();
            foreach (var handler in ieventHandlers)
            {
                try
                {
                    await handler.Handle(domainEvent, cancellationToken)
                    .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Unexpected error publishing domain event {EventType}", typeof(T));
                    throw;
                }
            }
        }
    }
}