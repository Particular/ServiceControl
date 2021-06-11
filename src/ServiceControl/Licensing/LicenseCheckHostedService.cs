namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;

    class LicenseCheckHostedService : IHostedService
    {
        public LicenseCheckHostedService(ActiveLicense activeLicense, IAsyncTimer scheduler)
        {
            this.activeLicense = activeLicense;
            this.scheduler = scheduler;
            ScheduleNextExecutionTask = Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var due = TimeSpan.FromHours(8);
            timer = scheduler.Schedule(_ =>
            {
                activeLicense.Refresh();
                return ScheduleNextExecutionTask;
            }, due, due, ex => { log.Error("Unhandled error while refreshing the license.", ex); });
            return Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return timer.Stop();
        }

        ActiveLicense activeLicense;
        readonly IAsyncTimer scheduler;
        TimerJob timer;

        static ILog log = LogManager.GetLogger<LicenseCheckHostedService>();
        static Task<TimerJobExecutionResult> ScheduleNextExecutionTask;
    }
}