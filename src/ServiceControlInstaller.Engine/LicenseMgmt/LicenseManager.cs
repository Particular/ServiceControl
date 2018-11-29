﻿namespace ServiceControlInstaller.Engine.LicenseMgmt
{
    using System;
    using Microsoft.Win32;
    using Particular.Licensing;

    public class LicenseManager
    {
        public static DetectedLicense FindLicense()
        {
            var sources = LicenseSource.GetStandardLicenseSources().ToArray();
            var result = ActiveLicense.Find("ServiceControl", sources);

            return new DetectedLicense("HKEY_LOCAL_MACHINE", LicenseDetails.FromLicense(result.License));
        }

        public static bool TryImportLicense(string licenseFile, out string errorMessage)
        {
            var licenseText = NonBlockingReader.ReadAllTextWithoutLocking(licenseFile);

            if (!LicenseVerifier.TryVerify(licenseText, out _))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            if (!TryDeserializeLicense(licenseText, out var license))
            {
                errorMessage = "Invalid license file";
                return false;
            }

            if (!license.ValidForApplication("ServiceControl"))
            {
                errorMessage = "License is not for ServiceControl";
                return false;
            }

            if(license.HasExpired())
            {
                errorMessage = "Failed to import because the license has expired";
                return false;
            }

            try
            {
                new RegistryLicenseStore(Registry.LocalMachine).StoreLicense(licenseText);
            }
            catch (Exception)
            {
                errorMessage = "Failed to import license into the registry";
                return false;
            }

            try
            {
                var machineLevelLicenseLocation = LicenseFileLocationResolver.GetPathFor(Environment.SpecialFolder.CommonApplicationData);

                new FilePathLicenseStore().StoreLicense(machineLevelLicenseLocation, licenseText);
            }
            catch (Exception)
            {
                errorMessage = "Failed to import license into the filesystem";
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