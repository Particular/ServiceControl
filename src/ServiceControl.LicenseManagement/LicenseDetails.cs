namespace ServiceControl.LicenseManagement
{
    using System;
    using System.Collections.Generic;
    using Particular.Licensing;

    public class LicenseDetails
    {
        public DateTime? ExpirationDate { get; private init; }
        public DateTime? UpgradeProtectionExpiration { get; private init; }

        public bool IsTrialLicense { get; private init; }
        public bool IsCommercialLicense { get; private init; }
        public bool IsExtendedTrial { get; private init; }
        public string LicenseType { get; private init; }
        public string Edition { get; private init; }
        public string RegisteredTo { get; private init; }
        public bool ValidForServiceControl { get; private init; }
        public int? DaysUntilSubscriptionExpires { get; private init; }
        public int? DaysUntilUpgradeProtectionExpires { get; private init; }
        public bool WarnUserTrialIsExpiring { get; private init; }
        public bool WarnUserTrialHasExpired { get; private init; }
        public bool WarnUserSubscriptionIsExpiring { get; private init; }
        public bool WarnUserSubscriptionHasExpired { get; private init; }
        public bool WarnUserUpgradeProtectionIsExpiring { get; private init; }
        public bool WarnUserUpgradeProtectionHasExpired { get; private init; }
        public string Status { get; private init; }

        public static LicenseDetails TrialFromEndDate(DateOnly endDate)
        {
            return FromLicense(new License
            {
                LicenseType = "Trial",
                ExpirationDate = endDate.ToDateTime(TimeOnly.MinValue),
                IsExtendedTrial = false,
                ValidApplications = new List<string> { "All" }
            });
        }

        public static LicenseDetails TrialExpired()
        {
            return FromLicense(new License
            {
                LicenseType = "Trial",
                ExpirationDate = DateTime.UtcNow.Date.AddDays(-2), //HasLicenseDateExpired uses a grace period of 1 day
                IsExtendedTrial = false,
                ValidApplications = new List<string> { "All" }
            });
        }

        internal static LicenseDetails FromLicense(License license)
        {
            LicenseStatus licenseStatus = license.GetLicenseStatus();

            var details = new LicenseDetails
            {
                UpgradeProtectionExpiration = license.UpgradeProtectionExpiration,
                //If expiration date is greater that 50 years treat is as no expiration date
                ExpirationDate = license.ExpirationDate.HasValue
                    ? license.ExpirationDate.Value > DateTime.UtcNow.AddYears(50) ? null : license.ExpirationDate
                    : license.ExpirationDate,
                RegisteredTo = license.RegisteredTo,
                IsCommercialLicense = license.IsCommercialLicense,
                IsExtendedTrial = license.IsExtendedTrial,
                IsTrialLicense = license.IsTrialLicense,
                LicenseType = license.LicenseType,
                Edition = license.Edition,
                ValidForServiceControl = license.ValidForApplication("ServiceControl"),
                DaysUntilSubscriptionExpires = license.GetDaysUntilLicenseExpires(),
                DaysUntilUpgradeProtectionExpires = license.GetDaysUntilUpgradeProtectionExpires(),
                WarnUserUpgradeProtectionHasExpired = licenseStatus is LicenseStatus.ValidWithExpiredUpgradeProtection or LicenseStatus.InvalidDueToExpiredUpgradeProtection,
                WarnUserTrialIsExpiring = licenseStatus == LicenseStatus.ValidWithExpiringTrial,
                WarnUserSubscriptionIsExpiring = licenseStatus == LicenseStatus.ValidWithExpiringSubscription,
                WarnUserUpgradeProtectionIsExpiring = licenseStatus == LicenseStatus.ValidWithExpiringUpgradeProtection,
                WarnUserTrialHasExpired = licenseStatus == LicenseStatus.InvalidDueToExpiredTrial,
                WarnUserSubscriptionHasExpired = licenseStatus == LicenseStatus.InvalidDueToExpiredSubscription,
                Status = licenseStatus.ToString()
            };
            return details;
        }

        public bool HasLicenseExpired() => ExpirationDate.HasValue && HasLicenseDateExpired(ExpirationDate.Value);

        public bool ReleaseNotCoveredByMaintenance(DateTime buildTimeStamp) =>
            buildTimeStamp > UpgradeProtectionExpiration;

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