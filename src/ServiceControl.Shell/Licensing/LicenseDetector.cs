namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Win32;
    using NServiceBus;
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class LicenseDetector : INeedInitialization
    {

        static readonly ILog Logger = LogManager.GetLogger(typeof(LicenseDetector));

        public void Init()
        {
            var activeLicense = DetermineActiveLicense();
            Configure.Component(() => activeLicense, DependencyLifecycle.SingleInstance);
        }
    
        static ActiveLicense DetermineActiveLicense()
        {
            var activeLicense = new ActiveLicense
            {
                IsValid = false
            };

            var detectedLicenses = FindLicenses();

            if (detectedLicenses.Count == 0)
            {
                Logger.Warn("No valid license could be found, falling back to trial license");
                activeLicense.Details = License.TrialLicense(TrialStartDateStore.GetTrialStartDate());
                activeLicense.HasExpired = LicenseExpirationChecker.HasLicenseExpired(activeLicense.Details);
            }
            else
            {
                var validLicenses = detectedLicenses.Where(p => p.Valid).ToList();
                if (validLicenses.Count > 0)
                {
                    //Use the first valid and not expired license 
                    var unexpiredLicense = validLicenses.FirstOrDefault(p => !LicenseExpirationChecker.HasLicenseExpired(p.Details));
                    if (unexpiredLicense == null)
                    {
                        var firstValidLicense = validLicenses.First();
                        Logger.InfoFormat("Using License from {0}", firstValidLicense.Location);
                        activeLicense.Details = firstValidLicense.Details;
                    }
                    else
                    {
                        Logger.InfoFormat("Using License from {0}", unexpiredLicense.Location);
                        activeLicense.Details = unexpiredLicense.Details;
                    }
                }
                else
                {
                    Logger.WarnFormat("No valid license was found");
                    return activeLicense;
                }
            }
            activeLicense.HasExpired = LicenseExpirationChecker.HasLicenseExpired(activeLicense.Details);
            if (activeLicense.HasExpired)
            {
                Logger.WarnFormat("License has expired");
            }
            else
            {
                activeLicense.IsValid = true;
            }
            return activeLicense;
        }

        static IList<DetectedLicense> FindLicenses()
        {
            
            var licenseEntries = new List<DetectedLicense>();
            
            //look for a license file in /bin/license/license.xml
            var localLicenseFileInLicenseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"License", @"License.xml");
            if (File.Exists(localLicenseFileInLicenseDir))
            {
                licenseEntries.Add(new DetectedLicense(localLicenseFileInLicenseDir, NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFileInLicenseDir)));
            }

            //look for a license file in /bin
            var localLicenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"ParticularPlatformLicense.xml");
            if (File.Exists(localLicenseFile))
            {
                licenseEntries.Add(new DetectedLicense(localLicenseFileInLicenseDir, NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFileInLicenseDir)));
            }
            string regLicense;

            //try HKCU
            if (new RegistryLicenseStore().TryReadLicense(out regLicense))
            {
                if (!string.IsNullOrWhiteSpace(regLicense))
                {
                    licenseEntries.Add(new DetectedLicense("HKEY_CURRENT_USER", regLicense));
                }
            }
            //try HKLM
            if (new RegistryLicenseStore(Registry.LocalMachine).TryReadLicense(out regLicense))
            {
                if (!string.IsNullOrWhiteSpace(regLicense))
                {
                    licenseEntries.Add(new DetectedLicense("HKEY_LOCAL_MACHINE", regLicense));
                }
            }
            return licenseEntries;
        }

        class DetectedLicense
        {
            public License Details { get; private set; }
            public bool Valid { get; private set; }
            public string Location { get; private set; }

            public DetectedLicense( string licensePath, string licenseText)
            {
                Location = licensePath;
                Details = null;
                Exception validationFailure;
                if (!LicenseVerifier.TryVerify(licenseText, out validationFailure))
                {
                    Logger.WarnFormat("License located at {0} is invalid - {1}", licensePath, validationFailure.Message);
                    return;
                }

                try
                {
                    Details = LicenseDeserializer.Deserialize(licenseText);
                }
                catch
                {
                    Logger.WarnFormat("License located at {0} is invalid - Incorrect format", licensePath);
                    return;
                }
                if (!Details.ValidForApplication("ServiceControl"))
                {
                    Logger.WarnFormat("License located at {0} is invalid for ServiceControl. Valid apps: '{1}'", licensePath, string.Join(",", Details.ValidApplications));
                    return;
                }
                if (LicenseExpirationChecker.HasLicenseExpired(Details))
                {
                    Logger.WarnFormat("License located at {0} has expired", licensePath);
                    //Don't return here - Expired isn't the same as invalid
                }
                Valid = true;
            }
        }
    }
}