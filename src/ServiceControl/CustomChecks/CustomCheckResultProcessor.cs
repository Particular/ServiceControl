namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using PlatformHealth;
    using ServiceControl.Persistence;

    class CustomCheckResultProcessor
    {
        public CustomCheckResultProcessor(IDomainEvents domainEvents, ICustomChecksDataStore store, ILogger<CustomCheckResultProcessor> logger, PlatformHealthState platformHealthState = null)
        {
            this.domainEvents = domainEvents;
            this.store = store;
            this.logger = logger;
            this.platformHealthState = platformHealthState;
        }

        public async Task ProcessResult(CustomCheckDetail checkDetail, CancellationToken cancellationToken = default)
        {
            try
            {
                platformHealthState?.Record(checkDetail);
                var statusChange = await store.UpdateCustomCheckStatus(checkDetail, cancellationToken);
                await RaiseEvents(statusChange, checkDetail, cancellationToken);

                var numberOfFailedChecks = await store.GetNumberOfFailedChecks(cancellationToken);

                if (lastCount == numberOfFailedChecks)
                {
                    return;
                }
                lastCount = numberOfFailedChecks;

                await domainEvents.Raise(new CustomChecksUpdated
                {
                    Failed = numberOfFailedChecks
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update periodic check status");
            }
        }

        async Task RaiseEvents(CheckStateChange state, CustomCheckDetail detail, CancellationToken cancellationToken)
        {
            var id = detail.GetDeterministicId();

            if (state == CheckStateChange.Changed)
            {
                if (detail.HasFailed)
                {
                    await domainEvents.Raise(new CustomCheckFailed
                    {
                        Id = id,
                        CustomCheckId = detail.CustomCheckId,
                        Category = detail.Category,
                        FailedAt = detail.ReportedAt,
                        FailureReason = detail.FailureReason,
                        OriginatingEndpoint = detail.OriginatingEndpoint
                    }, cancellationToken);
                }
                else
                {
                    await domainEvents.Raise(new CustomCheckSucceeded
                    {
                        Id = id,
                        CustomCheckId = detail.CustomCheckId,
                        Category = detail.Category,
                        SucceededAt = detail.ReportedAt,
                        OriginatingEndpoint = detail.OriginatingEndpoint
                    }, cancellationToken);
                }
            }
        }

        readonly IDomainEvents domainEvents;
        readonly ICustomChecksDataStore store;
        int lastCount;

        readonly ILogger<CustomCheckResultProcessor> logger;
        readonly PlatformHealthState platformHealthState;
    }
}