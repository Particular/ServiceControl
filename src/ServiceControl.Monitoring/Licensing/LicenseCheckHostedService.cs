namespace ServiceControl.Monitoring.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using ServiceControl.Infrastructure.BackgroundTasks;

    class LicenseCheckHostedService(ActiveLicense activeLicense, IAsyncTimer scheduler) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var due = TimeSpan.FromHours(8);
            timer = scheduler.Schedule(_ =>
            {
                activeLicense.Refresh();
                return ScheduleNextExecutionTask;
            }, due, due, ex => Logger.Error("Unhandled error while refreshing the license.", ex));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => timer.Stop(cancellationToken);

        TimerJob timer;

        static readonly ILog Logger = LogManager.GetLogger<LicenseCheckHostedService>();
        static readonly Task<TimerJobExecutionResult> ScheduleNextExecutionTask = Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
    }
}