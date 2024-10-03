namespace Particular.ServiceControl.Licensing
{
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Persistence;
    using NServiceBus.Logging;

    public class ActiveLicense
    {
        public ActiveLicense() => Refresh();

        public bool IsValid { get; set; }

        public LicenseDetails Details { get; set; }

        public void Refresh()
        {
            Logger.Debug("Refreshing ActiveLicense");

            var detectedLicense = LicenseManager.FindLicense();

            IsValid = !detectedLicense.Details.HasLicenseExpired();

            Details = detectedLicense.Details;
        }

        public async Task EnsureTrialLicenseIsValid(ILicenseLicenseMetadataProvider licenseLicenseMetadataProvider, CancellationToken cancellationToken)
        {
            Details = await EnsureTrialLicenseIsValid(Details, licenseLicenseMetadataProvider, cancellationToken);
        }

        internal static async Task<LicenseDetails> EnsureTrialLicenseIsValid(LicenseDetails licenseDetails, ILicenseLicenseMetadataProvider licenseLicenseMetadataProvider, CancellationToken cancellationToken)
        {
            if (licenseDetails.LicenseType.Equals("trial", StringComparison.OrdinalIgnoreCase))
            {
                var metadata = await licenseLicenseMetadataProvider.GetLicenseMetadata(cancellationToken);
                if (metadata == null)
                {
                    //No start date stored in the database, store one.
                    metadata = new TrialMetadata
                    {
                        //The trial period is 14 days
                        TrialStartDate = DateOnly.FromDateTime(licenseDetails.ExpirationDate.Value.AddDays(14))
                    };

                    await licenseLicenseMetadataProvider.InsertLicenseMetadata(metadata, cancellationToken);
                    return licenseDetails;
                }
                if (metadata.TrialStartDate >= DateOnly.FromDateTime(DateTime.Now))
                {
                    // Someone has tampered with the date stored in RavenDB, set the license to expired
                    return LicenseDetails.TrialExpired();
                }
                if (DateOnly.FromDateTime(licenseDetails.ExpirationDate ?? DateTime.MinValue) != metadata.TrialStartDate.AddDays(14))
                {
                    //The trial end date stored by the license component has been tempered with or reset, use the RavenDB value
                    return LicenseDetails.TrialFromEndDate(metadata.TrialStartDate.AddDays(14));
                }
                //Otherwise use the trial date stored by the licensing component
            }
            return licenseDetails;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
}