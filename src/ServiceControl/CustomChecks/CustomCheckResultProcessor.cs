namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;

    class CustomCheckResultProcessor
    {
        public CustomCheckResultProcessor(IDomainEvents domainEvents, ICustomChecksDataStore store)
        {
            this.domainEvents = domainEvents;
            this.store = store;
        }

        public async Task ProcessResult(CustomCheckDetail checkDetail)
        {
            try
            {
                var statusChange = await store.UpdateCustomCheckStatus(checkDetail);
                await RaiseEvents(statusChange, checkDetail);

                var numberOfFailedChecks = await store.GetNumberOfFailedChecks();

                if (lastCount == numberOfFailedChecks)
                {
                    return;
                }
                lastCount = numberOfFailedChecks;

                await domainEvents.Raise(new CustomChecksUpdated
                {
                    Failed = numberOfFailedChecks
                });
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to update periodic check status.", ex);
            }
        }

        async Task RaiseEvents(CheckStateChange state, CustomCheckDetail detail)
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
                    });
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
                    });
                }
            }
        }

        readonly IDomainEvents domainEvents;
        readonly ICustomChecksDataStore store;

        int lastCount;

        static ILog Logger = LogManager.GetLogger<CustomCheckResultProcessor>();
    }
}