namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Persistence;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;

    class LicenseCheckHostedService(ActiveLicense activeLicense, ILicenseLicenseMetadataProvider licenseLicenseMetadataProvide, IAsyncTimer scheduler) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var due = TimeSpan.FromHours(8);
            timer = scheduler.Schedule(async _ =>
            {
                activeLicense.Refresh();

                if (activeLicense.Details.LicenseType.Equals("trial", StringComparison.OrdinalIgnoreCase))
                {
                    var metadata = await licenseLicenseMetadataProvide.GetLicenseMetadata(cancellationToken);
                    if (metadata == null)
                    {
                        metadata = new TrialMetadata
                        {
                            TrialStartDate = DateOnly.FromDateTime(activeLicense.Details.ExpirationDate.Value.AddDays(-14))
                        };

                        await licenseLicenseMetadataProvide.InsertLicenseMetadata(metadata, cancellationToken);
                    }
                    else if (DateOnly.FromDateTime(activeLicense.Details.ExpirationDate ?? DateTime.MinValue) != metadata.TrialStartDate.AddDays(14))
                    {
                        activeLicense.Details = LicenseDetails.TrialFromStartDate(metadata.TrialStartDate);
                    }
                    else if (metadata.TrialStartDate >= DateOnly.FromDateTime(DateTime.Now))
                    {
                        // Someone has tampered with the date, set the license to expired
                        activeLicense.Details = LicenseDetails.TrialFromStartDate(DateOnly.FromDateTime(DateTime.Now.AddDays(-15)));
                    }
                }

                return TimerJobExecutionResult.ScheduleNextExecution;
            }, TimeSpan.FromTicks(0), due, ex => Logger.Error("Unhandled error while refreshing the license.", ex));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => timer.Stop();

        TimerJob timer;

        static readonly ILog Logger = LogManager.GetLogger<LicenseCheckHostedService>();
    }
}