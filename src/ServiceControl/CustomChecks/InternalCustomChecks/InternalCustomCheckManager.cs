namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Operations;

    class InternalCustomCheckManager
    {
        public InternalCustomCheckManager(
            ICustomCheck check,
            EndpointDetails localEndpointDetails,
            IAsyncTimer scheduler,
            CustomCheckResultProcessor checkResultProcessor,
            ILogger logger)
        {
            this.check = check;
            this.localEndpointDetails = localEndpointDetails;
            this.scheduler = scheduler;
            this.checkResultProcessor = checkResultProcessor;
            this.logger = logger;
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
                logger.LogInformation(e, "Cancelled");
            }
            catch (Exception ex)
            {
                var customCheckType = check.GetType();
                var reason = $"`{customCheckType}` implementation failed to run.";
                result = CheckResult.Failed(reason);
                logger.LogError(ex, "`{CustomCheckType}` implementation failed to run", customCheckType);
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

        public Task Stop() => timer?.Stop(CancellationToken.None) ?? Task.CompletedTask;

        TimerJob timer;
        readonly ICustomCheck check;
        readonly EndpointDetails localEndpointDetails;
        readonly IAsyncTimer scheduler;
        readonly CustomCheckResultProcessor checkResultProcessor;
        readonly ILogger logger;
    }
}