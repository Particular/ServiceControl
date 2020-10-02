namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    using System;
    using Particular.Licensing;

    public class LicenseDetails
    {
        public DateTime? ExpirationDate { get; private set; }
        public DateTime? UpgradeProtectionExpiration { get; private set; }

        public bool IsTrialLicense { get; private set; }
        public bool IsCommercialLicense { get; private set; }
        public bool IsExtendedTrial { get; private set; }
        public string LicenseType { get; private set; }
        public string Edition { get; set; }
        public string RegisteredTo { get; private set; }
        public bool ValidForServiceControl { get; private set; }
        public int? DaysUntilSubscriptionExpires { get; private set; }
        public int? DaysUntilUpgradeProtectionExpires { get; private set; }
        public bool WarnUserTrialIsExpiring { get; private set; }
        public bool WarnUserTrialHasExpired { get; private set; }
        public bool WarnUserSubscriptionIsExpiring { get; private set; }
        public bool WarnUserSubscriptionHasExpired { get; private set; }
        public bool WarnUserUpgradeProtectionIsExpiring { get; private set; }
        public bool WarnUserUpgradeProtectionHasExpired { get; private set; }

        internal static LicenseDetails FromLicense(License license)
        {
            var details = new LicenseDetails
            {
                UpgradeProtectionExpiration = license.UpgradeProtectionExpiration,
                //If expiration date is greater that 50 years treat is as no expiration date
                ExpirationDate = license.ExpirationDate.HasValue
                    ? (license.ExpirationDate.Value > DateTime.Now.AddYears(50) ? null : license.ExpirationDate)
                    : license.ExpirationDate,
                RegisteredTo = license.RegisteredTo,
                IsCommercialLicense = license.IsCommercialLicense,
                IsExtendedTrial = license.IsExtendedTrial,
                IsTrialLicense = license.IsTrialLicense,
                LicenseType = license.LicenseType,
                Edition = license.Edition,
                ValidForServiceControl = license.ValidForApplication("ServiceControl"),
                DaysUntilSubscriptionExpires = license.GetDaysUntilLicenseExpires(),
                DaysUntilUpgradeProtectionExpires = license.GetDaysUntilUpgradeProtectionExpires()
            };

            switch (license.GetLicenseStatus())
            {
                case LicenseStatus.Valid:
                    break;
                case LicenseStatus.ValidWithExpiredUpgradeProtection:
                    details.WarnUserUpgradeProtectionHasExpired = true;
                    break;
                case LicenseStatus.ValidWithExpiringTrial:
                    details.WarnUserTrialIsExpiring = true;
                    break;
                case LicenseStatus.ValidWithExpiringSubscription:
                    details.WarnUserSubscriptionIsExpiring = true;
                    break;
                case LicenseStatus.ValidWithExpiringUpgradeProtection:
                    details.WarnUserUpgradeProtectionIsExpiring = true;
                    break;
                case LicenseStatus.InvalidDueToExpiredTrial:
                    details.WarnUserTrialHasExpired = true;
                    break;
                case LicenseStatus.InvalidDueToExpiredSubscription:
                    details.WarnUserSubscriptionHasExpired = true;
                    break;
                case LicenseStatus.InvalidDueToExpiredUpgradeProtection:
                    details.WarnUserUpgradeProtectionHasExpired = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            return details;
        }

        public bool HasLicenseExpired()
        {
            return ExpirationDate.HasValue && HasLicenseDateExpired(ExpirationDate.Value);
        }

        public bool ReleaseNotCoveredByMaintenance(DateTime buildTimeStamp)
        {
            return buildTimeStamp > UpgradeProtectionExpiration;
        }

        static bool HasLicenseDateExpired(DateTime licenseDate)
        {
            var oneDayGrace = licenseDate;

            if (licenseDate < DateTime.MaxValue.AddDays(-1))
            {
                oneDayGrace = licenseDate.AddDays(1);
            }

            return oneDayGrace < DateTime.UtcNow.Date;
        }
    }
}