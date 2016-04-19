namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Win32;
    using NServiceBus;
    using NServiceBus.Logging;
    using Particular.Licensing;
    using License = Particular.Licensing.License;

    public class LicenseDetector : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            var activeLicense = DetermineActiveLicense();
            configuration.RegisterComponents(c => c.RegisterSingleton(activeLicense));
        }

        static ActiveLicense DetermineActiveLicense()
        {
            var activeLicense = new ActiveLicense
            {
                IsValid = false
            };

            var license = TryFindLicense();

            if (license == null)
            {
                Logger.Warn("No valid license could be found, falling back to trial license");
                activeLicense.Details = License.TrialLicense(TrialStartDateStore.GetTrialStartDate());
            }
            else
            {
                activeLicense.Details = license;
                var licenseSummary = new StringBuilder("-------------LICENSE----------------");
                licenseSummary.AppendFormat("\r\nRegistered to: {0}\r\n", license.RegisteredTo);
                if (license.ExpirationDate.HasValue)
                {
                    licenseSummary.AppendFormat("License Expiration: {0:dd MMMM yyyy}\r\n", license.ExpirationDate.Value);
                }
                if (license.UpgradeProtectionExpiration.HasValue)
                {
                    licenseSummary.AppendFormat("Upgrade Protection Expiration: {0:dd MMMM yyyy}\r\n", license.UpgradeProtectionExpiration.Value);
                }
                Logger.InfoFormat("{0}", licenseSummary);
            }

            activeLicense.HasExpired = LicenseExpirationChecker.HasLicenseExpired(activeLicense.Details);

            if (activeLicense.HasExpired)
            {
                Logger.WarnFormat("License Expired");
            }
            else
            {
                activeLicense.IsValid = true;
            }

            return activeLicense;
        }
        
        static License TryFindLicense()
        {
            var validLicenses = new Dictionary<string, License>();
            License license;

            //look for a license file in /bin/license/license.xml
            var localLicenseFileInLicenseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"License", @"License.xml");
            if (File.Exists(localLicenseFileInLicenseDir))
            {
                Logger.InfoFormat(@"License file found: {0}", localLicenseFileInLicenseDir);
                if (ValidateLicense(localLicenseFileInLicenseDir, NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFileInLicenseDir), out license))
                {
                    validLicenses.Add(localLicenseFileInLicenseDir, license);
                }
            }
            else
            {
                Logger.InfoFormat("No license file found: {0}", localLicenseFileInLicenseDir);
            }

            //look for a license file in /bin
            var localLicenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"ParticularPlatformLicense.xml");
            if (File.Exists(localLicenseFile))
            {
                Logger.InfoFormat("License file found: {0}", localLicenseFile);
                if (ValidateLicense(localLicenseFile, NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFile), out license))
                {
                    validLicenses.Add(localLicenseFile, license);
                }
            }
            else
            {
                Logger.InfoFormat("No license found: {0}", localLicenseFile);
            }

            string regLicense;
            
            //try HKLM
            if (new RegistryLicenseStore(Registry.LocalMachine).TryReadLicense(out regLicense))
            {
                Logger.InfoFormat("License found in registry key: {0}", Registry.LocalMachine.Name + @"\SOFTWARE\ParticularSoftware");
                if (ValidateLicense(Registry.LocalMachine.Name, regLicense, out license))
                {
                    validLicenses.Add(Registry.LocalMachine.Name, license);
                }
            }
            else
            {
                Logger.InfoFormat("No license found in registry key: {0}", Registry.LocalMachine.Name + @"\SOFTWARE\ParticularSoftware");
            }

            if (validLicenses.Any())
            {
                var licenseToUse = validLicenses.OrderByDescending(p => p.Value.ExpirationDate).FirstOrDefault();
                Logger.InfoFormat("Using license from {0}", licenseToUse.Key);
                return licenseToUse.Value;
            }
            return null;
        }
        
        static bool ValidateLicense(string location, string licenseText, out License license)
        {
            Exception validationFailure;
            license = null;

            if (!LicenseVerifier.TryVerify(licenseText, out validationFailure))
            {
                Logger.WarnFormat("License found at '{0}' is not valid: {1}", location, validationFailure);
                return false;
            }

            try
            {
                license = LicenseDeserializer.Deserialize(licenseText);
                if (license.ValidForApplication("ServiceControl"))
                {
                    return true;
                }
                Logger.WarnFormat("License found at '{0}' was not valid for ServiceControl. Valid apps: '{0}'", string.Join(",", license.ValidApplications));
            }
            catch
            {
                Logger.WarnFormat("License found at '{0}' could not be deserialized", location);
            }
            return false;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(LicenseDetector));
    }
}