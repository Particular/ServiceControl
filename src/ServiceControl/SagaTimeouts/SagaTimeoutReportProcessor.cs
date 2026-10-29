namespace ServiceControl.SagaTimeouts
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Persistence.SagaTimeouts;

    class SagaTimeoutReportProcessor(ISagaTimeoutDataStore store, TimeProvider timeProvider, ILogger<SagaTimeoutReportProcessor> logger)
    {
        public async Task Apply(SagaTimeoutChangeMessage message, SagaTimeoutChangeKind kind, Func<SagaTimeoutChange, SagaTimeoutChange> addKindFields, CancellationToken cancellationToken = default)
        {
            var change = addKindFields(new SagaTimeoutChange
            {
                Kind = kind,
                TimeoutId = message.TimeoutId,
                TimeoutType = message.TimeoutType,
                Version = message.Version,
                EndpointName = message.EndpointName,
                SagaType = message.SagaType,
                SagaId = message.SagaId,
                CorrelationValue = message.CorrelationPropertyValue,
                ChangedAt = message.ChangedAt,
                ReceivedAt = timeProvider.GetUtcNow(),
                Host = message.Host,
                HostId = message.HostId,
                ManagementAddress = message.ManagementAddress
            });

            await Apply(change, cancellationToken);
        }

        public Task ApplyRejection(SagaTimeoutChangeRejected message, CancellationToken cancellationToken = default) =>
            Apply(new SagaTimeoutChange
            {
                Kind = SagaTimeoutChangeKind.Rejected,
                TimeoutId = message.TimeoutId,
                EndpointName = message.EndpointName,
                SagaType = message.SagaType,
                SagaId = message.SagaId,
                CorrelationValue = message.CorrelationPropertyValue,
                OperationId = message.OperationId,
                RequestedChange = message.RequestedChange,
                RequestedDueTime = message.RequestedDueTime,
                Reason = message.Reason,
                ChangedAt = message.ChangedAt,
                ReceivedAt = timeProvider.GetUtcNow(),
                Host = message.Host,
                HostId = message.HostId,
                ManagementAddress = message.ManagementAddress
            }, cancellationToken);

        public async Task ApplySagaCompleted(SagaCompleted message, CancellationToken cancellationToken = default)
        {
            var outcome = await store.ApplySagaCompleted(
                new SagaCompletedChange(message.EndpointName, message.SagaType, message.SagaId, message.ChangedAt, timeProvider.GetUtcNow()),
                cancellationToken);

            logger.LogDebug("Saga {SagaType} {SagaId} completed: {Outcome}", message.SagaType, message.SagaId, outcome);
        }

        async Task Apply(SagaTimeoutChange change, CancellationToken cancellationToken)
        {
            var outcome = await store.Apply(change, cancellationToken);

            logger.LogDebug("Saga timeout {TimeoutId} {Kind} version {Version}: {Outcome}", change.TimeoutId, change.Kind, change.Version, outcome);
        }
    }
}
