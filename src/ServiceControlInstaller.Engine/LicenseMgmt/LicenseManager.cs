﻿namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    using System;
    using Microsoft.Win32;
    using Particular.Licensing;
    using ServiceControlInstaller.Engine.FileSystem;

    public class LicenseManager
    {
        public static DetectedLicense FindLicense()
        {
            var result = ActiveLicense.Find("ServiceControl", new LicenseSourceHKLMRegKey());

            return new DetectedLicense("HKEY_LOCAL_MACHINE", LicenseDetails.FromLicense(result.License));
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

        static bool TryDeserializeLicense(string licenseText, out License license)
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
    }
}
