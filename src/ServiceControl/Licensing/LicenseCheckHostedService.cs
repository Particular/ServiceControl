namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.Persistence;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;

    class LicenseCheckHostedService(ActiveLicense activeLicense, ILicenseLicenseMetadataProvider licenseLicenseMetadataProvider, IAsyncTimer scheduler) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var due = TimeSpan.FromHours(8);
            timer = scheduler.Schedule(async _ =>
            {
                activeLicense.Refresh();
                await activeLicense.EnsureTrialLicenseIsValid(licenseLicenseMetadataProvider, cancellationToken);

                return TimerJobExecutionResult.ScheduleNextExecution;
            }, TimeSpan.FromTicks(0), due, ex => Logger.Error("Unhandled error while refreshing the license.", ex));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => timer.Stop();

        TimerJob timer;

        static readonly ILog Logger = LogManager.GetLogger<LicenseCheckHostedService>();
    }
}