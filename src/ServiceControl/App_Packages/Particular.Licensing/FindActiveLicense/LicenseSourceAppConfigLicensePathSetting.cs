#if APPCONFIGLICENSESOURCE
namespace Particular.Licensing
{
    using System.Configuration;
    using System.IO;

    class LicenseSourceAppConfigLicensePathSetting : LicenseSource
    {
        public LicenseSourceAppConfigLicensePathSetting()
            : base("app config 'NServiceBus/LicensePath' setting")
        { }

        public override LicenseSourceResult Find(string applicationName)
        {
            var licensePath = ConfigurationManager.AppSettings["NServiceBus/LicensePath"];

            if (!string.IsNullOrEmpty(licensePath))
            {
                if (File.Exists(licensePath))
                {
                    return ValidateLicense(NonBlockingReader.ReadAllTextWithoutLocking(licensePath), applicationName);
                }
            }

            return new LicenseSourceResult
            {
                Location = location,
                Result = $"License file not found in path supplied by app config 'NServiceBus/LicensePath' setting. Value was '{licensePath}'"
            };
        }
    }
}
#endif
