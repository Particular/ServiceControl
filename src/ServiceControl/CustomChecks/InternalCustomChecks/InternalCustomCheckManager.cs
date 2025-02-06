namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.BackgroundTasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Operations;

    class InternalCustomCheckManager
    {
        public InternalCustomCheckManager(
            ICustomCheck check,
            EndpointDetails localEndpointDetails,
            IAsyncTimer scheduler,
            CustomCheckResultProcessor checkResultProcessor)
        {
            this.check = check;
            this.localEndpointDetails = localEndpointDetails;
            this.scheduler = scheduler;
            this.checkResultProcessor = checkResultProcessor;
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
            CheckResult result = default;
            try
            {
                result = await check.PerformCheck(cancellationToken);
            }
            catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
            {
                Logger.Info("Cancelled", e);
            }
            catch (Exception ex)
            {
                var reason = $"`{check.GetType()}` implementation failed to run.";
                result = CheckResult.Failed(reason);
                Logger.Error(reason, ex);
            }

            var detail = new CustomCheckDetail
            {
                OriginatingEndpoint = localEndpointDetails,
                CustomCheckId = check.Id,
                Category = check.Category,
                HasFailed = result.HasFailed,
                FailureReason = result.FailureReason
            };

            await checkResultProcessor.ProcessResult(detail);

            return check.Interval.HasValue
                ? TimerJobExecutionResult.ScheduleNextExecution
                : TimerJobExecutionResult.DoNotContinueExecuting;
        }

        public Task Stop() => timer?.Stop() ?? Task.CompletedTask;

        TimerJob timer;
        readonly ICustomCheck check;
        readonly EndpointDetails localEndpointDetails;
        readonly IAsyncTimer scheduler;
        readonly CustomCheckResultProcessor checkResultProcessor;

        static ILog Logger = LogManager.GetLogger<InternalCustomCheckManager>();
    }
}