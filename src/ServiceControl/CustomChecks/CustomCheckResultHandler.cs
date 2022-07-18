namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;

    class CustomCheckResultHandler
    {
        public CustomCheckResultHandler(IDomainEvents domainEvents, ICustomChecksDataStore store)
        {
            this.domainEvents = domainEvents;
            this.store = store;
        }

        public async Task HandleResult(CustomCheckDetail checkDetail)
        {
            try
            {
                var statusChange = await store.UpdateCustomCheckStatus(checkDetail).ConfigureAwait(false);
                await RaiseEvents(statusChange, checkDetail).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to update periodic check status.", ex);
            }
        }

        async Task RaiseEvents(CheckStateChange state, CustomCheckDetail detail)
        {
            var id = DeterministicGuid.MakeId(detail.OriginatingEndpoint.Name, detail.OriginatingEndpoint.HostId.ToString(), detail.CustomCheckId);

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
                    }).ConfigureAwait(false);
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
                    }).ConfigureAwait(false);
                }
            }
        }

        readonly IDomainEvents domainEvents;
        readonly ICustomChecksDataStore store;

        static ILog Logger = LogManager.GetLogger<CustomCheckResultHandler>();
    }
}