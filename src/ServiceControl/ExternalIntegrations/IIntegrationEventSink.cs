namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Receives the integration events (ServiceControl.Contracts types) of every dispatched batch from the external integrations outbox.
    /// Exceptions cause the whole batch to be retried, so implementations must handle delivery failures themselves.
    /// </summary>
    interface IIntegrationEventSink
    {
        Task Dispatch(IReadOnlyCollection<object> integrationEvents, CancellationToken cancellationToken = default);
    }
}
