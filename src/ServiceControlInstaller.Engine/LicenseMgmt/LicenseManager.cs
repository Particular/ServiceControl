namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Win32;
    using Particular.Licensing;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;

    public class LicenseManager
    {
        public static IList<DetectedLicense> FindLicenses()
        {
            var licenseEntries = new List<DetectedLicense>();

            foreach (var binFolder in ServiceControlInstance.Instances().Select(p => p.InstallPath))
            {
                //look for a license file in /bin/license/license.xml
                var localLicenseFileInLicenseDir = Path.Combine(binFolder, "License", @"License.xml");
                if (File.Exists(localLicenseFileInLicenseDir))
                {
                    licenseEntries.Add(new DetectedLicense(localLicenseFileInLicenseDir, NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFileInLicenseDir)));
                }

                //look for a license file in /bin
                var localLicenseFile = Path.Combine(binFolder, @"ParticularPlatformLicense.xml");
                if (File.Exists(localLicenseFile))
                {
                    licenseEntries.Add(new DetectedLicense(localLicenseFileInLicenseDir, NonLockingFileReader.ReadAllTextWithoutLocking(localLicenseFileInLicenseDir)));
                }
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
            using (var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default))
            {
                if (new RegistryLicenseStore(localMachine).TryReadLicense(out regLicense))
                {
                    if (!string.IsNullOrWhiteSpace(regLicense))
                    {
                        licenseEntries.Add(new DetectedLicense("HKEY_LOCAL_MACHINE", regLicense));
                    }
                }
            }

            return licenseEntries;
        }

        public static bool TryImportLicense(string licenseFile, out string errorMessage)
        {
            Exception validationFailure;
            License license;
            var licenseText = NonLockingFileReader.ReadAllTextWithoutLocking(licenseFile);

            if (!LicenseVerifier.TryVerify(licenseText, out validationFailure))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            if (!TryDeserializeLicense(licenseText, out license))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            if (!license.ValidForApplication("ServiceControl"))
            {
                errorMessage = "License is not for ServiceControl";
                return false;
            }

            try
            {
                using (var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default))
                {
                    new RegistryLicenseStore(localMachine).StoreLicense(licenseText);
                }
            }
            catch (Exception)
            {
                errorMessage = "Failed to import license into the registry";
                return false;
            }

            errorMessage = null;
            return true;
        }

        internal static bool TryDeserializeLicense(string licenseText, out License license)
        {
            license = null;
            try
            {
                license = LicenseDeserializer.Deserialize(licenseText);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DetectedLicense FindTrialLicense()
        {
            using (var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default))
            {
                License license;
                if (new RegistryLicenseStore(localMachine).TryReadTrialLicense(out license))
                {
                    return new DetectedLicense
                    {
                        Details = LicenseDetails.FromLicense(license),
                        Location = "HKEY_LOCAL_MACHINE"
                    };
                }
            }
            return null;
        }
    }
}
