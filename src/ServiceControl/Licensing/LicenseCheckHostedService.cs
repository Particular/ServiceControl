namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    class LicenseCheckHostedService(ActiveLicense activeLicense, IAsyncTimer scheduler, ILogger<LicenseCheckHostedService> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var due = TimeSpan.FromHours(8);
            timer = scheduler.Schedule(async _ =>
            {
                await activeLicense.Refresh(cancellationToken);

                return TimerJobExecutionResult.ScheduleNextExecution;
            }, TimeSpan.FromTicks(0), due, ex => logger.LogError(ex, "Unhandled error while refreshing the license"));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => timer.Stop(cancellationToken);

        TimerJob timer;
    }
}