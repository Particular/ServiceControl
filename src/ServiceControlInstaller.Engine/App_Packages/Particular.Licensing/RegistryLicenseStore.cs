namespace Particular.Licensing
{
    using System;
    using System.Globalization;
    using System.Security;
    using Microsoft.Win32;

    class RegistryLicenseStore
    {
        public RegistryLicenseStore()
        {
            keyPath = DefaultKeyPath;
            keyName = DefaultKeyName;
            regKey = Registry.CurrentUser;
        }

        public RegistryLicenseStore(RegistryKey regKey, string keyPath = DefaultKeyPath, string keyName = DefaultKeyName)
        {
            this.keyPath = keyPath;
            this.keyName = keyName;
            this.regKey = regKey;
        }

        public bool TryReadLicense(out string license)
        {
            try
            {
                using (var registryKey = regKey.OpenSubKey(keyPath))
                {
                    if (registryKey == null)
                    {
                        license = null;
                        return false;
                    }

                    var licenseValue = registryKey.GetValue("License", null);

                    var value = licenseValue as string[];
                    if (value != null)
                    {
                        license = string.Join(" ", value);
                    }
                    else
                    {
                        license = (string)licenseValue;    
                    }
                    
                    return !string.IsNullOrEmpty(license);
                }
            }
            catch (SecurityException exception)
            {
                throw new Exception($"Failed to access '{FullPath}'. Do you have permission to read this key?", exception);
            }
        }

        public bool TryReadTrialLicense(out License license)
        {
            license = null;
            try
            {

                using (var registryKey = regKey.OpenSubKey(keyPath))
                {
                    var licenseValue = registryKey?.GetValue("TrialStart", null);

                    var value = licenseValue as string;
                    if (value != null)
                    {
                        DateTime trialStartDate;
                        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out trialStartDate))
                        {
                            license = License.TrialLicense(trialStartDate);
                            return true;
                        }
                    }
                    return false;
                }
            }
            catch (SecurityException exception)
            {
                throw new Exception($"Failed to access '{FullPath}'. Do you have permission to read this key?", exception);
            }
        }


        public void StoreLicense(string license)
        {
            try
            {
                using (var registryKey = regKey.CreateSubKey(keyPath))
                {
                    if (registryKey == null)
                    {
                        throw new Exception($"CreateSubKey for '{keyPath}' returned null. Do you have permission to write to this key");
                    }

                    registryKey.SetValue(keyName, license, RegistryValueKind.String);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new Exception($"Failed to access '{FullPath}'. Do you have permission to write to this key?", exception);
            }
        }

        string FullPath => $"{regKey.Name} : {keyPath} : {keyName}";

        string keyPath;
        string keyName;
        RegistryKey regKey;

        const string DefaultKeyPath = @"SOFTWARE\ParticularSoftware";
        const string DefaultKeyName = "License";
    }
}