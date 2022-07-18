namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Infrastructure;
    using Infrastructure.BackgroundTasks;
    using Infrastructure.DomainEvents;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;

    class InternalCustomCheckManager
    {
        public InternalCustomCheckManager(
            ICustomChecksDataStore store,
            ICustomCheck check,
            EndpointDetails localEndpointDetails,
            IAsyncTimer scheduler,
            IDomainEvents domainEvents)
        {
            this.store = store;
            this.check = check;
            this.localEndpointDetails = localEndpointDetails;
            this.scheduler = scheduler;
            this.domainEvents = domainEvents;
        }

        public void Start()
        {
            timer = scheduler.Schedule(
                Run,
                TimeSpan.Zero,
                check.Interval ?? TimeSpan.MaxValue,
                e => { /* Should not happen */ }
            );
        }

        async Task<TimerJobExecutionResult> Run(CancellationToken cancellationToken)
        {
            CheckResult result;
            try
            {
                result = await check.PerformCheck()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var reason = $"`{check.GetType()}` implementation failed to run.";
                result = CheckResult.Failed(reason);
                Logger.Error(reason, ex);
            }

            try
            {
                var detail = new CustomCheckDetail
                {
                    OriginatingEndpoint = localEndpointDetails,
                    ReportedAt = DateTime.UtcNow,
                    CustomCheckId = check.Id,
                    Category = check.Category,
                    HasFailed = result.HasFailed,
                    FailureReason = result.FailureReason
                };

                var statusChange = await store.UpdateCustomCheckStatus(detail).ConfigureAwait(false);
                await RaiseEvents(statusChange, detail).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to update periodic check status.", ex);
            }

            return check.Interval.HasValue
                ? TimerJobExecutionResult.ScheduleNextExecution
                : TimerJobExecutionResult.DoNotContinueExecuting;
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

        public Task Stop() => timer?.Stop() ?? Task.CompletedTask;

        TimerJob timer;
        readonly ICustomChecksDataStore store;
        readonly ICustomCheck check;
        readonly EndpointDetails localEndpointDetails;
        readonly IAsyncTimer scheduler;
        readonly IDomainEvents domainEvents;

        static ILog Logger = LogManager.GetLogger<InternalCustomCheckManager>();
    }
}