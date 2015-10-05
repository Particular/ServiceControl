// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    using System;
    using Particular.Licensing;

    public class LicenseDetails
    {
        public DateTime? ExpirationDate { get; private set; }
        public DateTime? UpgradeProtectionExpiration { get; private set; }

        public bool IsTrialLicense { get; private set;}
        public bool IsCommercialLicense { get; private set; }
        public bool IsExtendedTrial { get; private set; }
        public string LicenseType { get; private set; }
        public string RegisteredTo { get; private set; }
        public bool Valid { get; private set; }

        internal static LicenseDetails FromLicense(License license)
        {
            return new LicenseDetails
            {
                UpgradeProtectionExpiration = license.UpgradeProtectionExpiration,
                //If expiration date is greater that 50 years treat is as no expiration date
                ExpirationDate = license.ExpirationDate.HasValue ? 
                    (license.ExpirationDate.Value > DateTime.Now.AddYears(50) ? null : license.ExpirationDate)
                    : license.ExpirationDate,   
                RegisteredTo = license.RegisteredTo,
                IsCommercialLicense = license.IsCommercialLicense,
                IsExtendedTrial = license.IsExtendedTrial,
                IsTrialLicense = license.IsTrialLicense,
                LicenseType = license.LicenseType,
                Valid = true
            };
        }
    }
}
