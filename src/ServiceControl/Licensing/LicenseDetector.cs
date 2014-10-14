namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.IO;
    using Microsoft.Win32;
    using NServiceBus;
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class LicenseDetector : INeedInitialization
    {
        public void Init()
        {
            var activeLicense = DetermineActiveLicense();

            //valid license
            Configure.Component(() => activeLicense, DependencyLifecycle.SingleInstance);
        }

        static ActiveLicense DetermineActiveLicense()
        {
            var activeLicense = new ActiveLicense
            {
                IsValid = false
            };

            var licenseText = TryFindLicense();

            if (string.IsNullOrEmpty(licenseText))
            {
                Logger.Warn("No valid license could be found, falling back to trial license");

                activeLicense.Details = License.TrialLicense(TrialStartDateStore.GetTrialStartDate());
            }
            else
            {
                Exception validationFailure;

                if (!LicenseVerifier.TryVerify(licenseText, out validationFailure))
                {
                    Logger.WarnFormat("Found license was not valid: {0}", validationFailure);
                    return activeLicense;
                }


                var licenseDetails = LicenseDeserializer.Deserialize(licenseText);

                if (!licenseDetails.ValidForApplication("ServiceControl"))
                {
                    Logger.WarnFormat("Found license was is not valid for ServiceControl. Valid apps: '{0}'", string.Join(",", licenseDetails.ValidApplications));
                    return activeLicense;
                }

                activeLicense.Details = licenseDetails;                
            }

            activeLicense.HasExpired = LicenseExpirationChecker.HasLicenseExpired(activeLicense.Details);

            if (activeLicense.HasExpired)
            {
                Logger.WarnFormat("Found license has expired");
            }
            else
            {
                activeLicense.IsValid = true;
            }

            return activeLicense;
        }

        static string TryFindLicense()
        {
            //look for a license file in /bin/license/license.xml
            var localLicenseFileInLicenseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"License", @"License.xml");
            if (File.Exists(localLicenseFileInLicenseDir))
            {
                Logger.InfoFormat(@"Using license in current folder ({0}).", localLicenseFileInLicenseDir);
                return NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFileInLicenseDir);
            }


            //look for a license file in /bin
            var localLicenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"ParticularPlatformLicense.xml");
            if (File.Exists(localLicenseFile))
            {
                Logger.InfoFormat(@"Using license in current folder ({0}).", localLicenseFile);
                return NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFile);
            }

            
            string regLicense;

            //try HKCU
            if (new RegistryLicenseStore().TryReadLicense(out regLicense))
            {
                Logger.InfoFormat("Using license in HKCU");
                return regLicense;
            }

            //try HKLM
            if (new RegistryLicenseStore(Registry.LocalMachine).TryReadLicense(out regLicense))
            {
                Logger.InfoFormat("Using license in HKLM");
                return regLicense;
            }

            return null;
        }


        static readonly ILog Logger = LogManager.GetLogger(typeof(LicenseDetector));
    }
}