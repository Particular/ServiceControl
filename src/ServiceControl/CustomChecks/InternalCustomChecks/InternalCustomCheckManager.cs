namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Infrastructure.BackgroundTasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;

    class InternalCustomCheckManager
    {
        public InternalCustomCheckManager(ICustomChecksStorage store, ICustomCheck check, EndpointDetails localEndpointDetails, IAsyncTimer scheduler)
        {
            this.store = store;
            this.check = check;
            this.localEndpointDetails = localEndpointDetails;
            this.scheduler = scheduler;
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
                await store.UpdateCustomCheckStatus(
                    new CustomCheckDetail
                    {
                        OriginatingEndpoint = localEndpointDetails,
                        ReportedAt = DateTime.UtcNow,
                        CustomCheckId = check.Id,
                        Category = check.Category,
                        HasFailed = result.HasFailed,
                        FailureReason = result.FailureReason
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to update periodic check status.", ex);
            }

            return check.Interval.HasValue
                ? TimerJobExecutionResult.ScheduleNextExecution
                : TimerJobExecutionResult.DoNotContinueExecuting;
        }

        public Task Stop() => timer?.Stop() ?? Task.CompletedTask;

        TimerJob timer;
        readonly ICustomChecksStorage store;
        readonly ICustomCheck check;
        readonly EndpointDetails localEndpointDetails;
        readonly IAsyncTimer scheduler;

        static ILog Logger = LogManager.GetLogger<InternalCustomCheckManager>();
    }
}