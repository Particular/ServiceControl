#if APPCONFIGLICENSESOURCE
namespace Particular.Licensing
{
    using System.Configuration;

    class LicenseSourceAppConfigLicenseSetting : LicenseSource
    {
        public LicenseSourceAppConfigLicenseSetting()
            : base("app config 'NServiceBus/License' setting")
        { }

        public override LicenseSourceResult Find(string applicationName)
        {
            var license = ConfigurationManager.AppSettings["NServiceBus/License"];

            if (!string.IsNullOrEmpty(license))
            {
                return ValidateLicense(license, applicationName);
            }

            return new LicenseSourceResult
            {
                Location = location,
                Result = $"License not found in app config 'NServiceBus/License' setting"
            };
        }
    }
}
#endif
