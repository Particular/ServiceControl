namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Persistence;
    using Microsoft.Extensions.Logging;

    public class ActiveLicense(ITrialLicenseDataProvider trialLicenseDataProvider, ILogger<ActiveLicense> logger)
    {
        public bool IsValid { get; set; }
        public bool IsEvaluation { get; set; }

        public LicenseDetails Details { get; set; }

        public async Task Refresh(CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Refreshing ActiveLicense");

            var detectedLicense = LicenseManager.FindLicense();

            Details = await ValidateTrialLicense(detectedLicense.Details, trialLicenseDataProvider, cancellationToken);

            IsValid = !Details.HasLicenseExpired();

            IsEvaluation = detectedLicense.IsEvaluationLicense;
        }

        internal static async Task<LicenseDetails> ValidateTrialLicense(LicenseDetails licenseDetails, ITrialLicenseDataProvider trialLicenseDataProvider, CancellationToken cancellationToken = default)
        {
            if (licenseDetails.LicenseType.Equals("trial", StringComparison.OrdinalIgnoreCase))
            {
                //HINT: When dealing with trial license, we want to rely on the value stored in the db only.
                //      The trial period starts when ServiceControl instance gets first connected
                //      to the database. Re/creation of container for the main instance does not affect the trial.

                var trialEndDateInDb = await trialLicenseDataProvider.GetTrialEndDate(cancellationToken);

                //If the trial end date has not been stored in the database, store the value
                if (trialEndDateInDb == null)
                {
                    //Initialize with the default for trial license
                    trialEndDateInDb = DateOnly.FromDateTime(licenseDetails.ExpirationDate.Value);

                    await trialLicenseDataProvider.StoreTrialEndDate(trialEndDateInDb.Value, cancellationToken);
                }

                //If the trial end date in db has been tampered, invalidate the license
#pragma warning disable RS0030 // Do not use banned apis: Runtime events, expiry checks, and monitoring thresholds require current wall-clock time
                if (trialEndDateInDb > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(MaxTrialPeriodInDays))
#pragma warning restore RS0030
                {
                    return LicenseDetails.TrialExpired();
                }

                return LicenseDetails.TrialFromEndDate(trialEndDateInDb.Value);
            }

            return licenseDetails;
        }
        static readonly int MaxTrialPeriodInDays = 14;
    }
}