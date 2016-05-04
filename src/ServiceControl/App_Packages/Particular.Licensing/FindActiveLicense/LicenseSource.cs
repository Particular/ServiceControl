namespace Particular.Licensing
{
    using System;
    
    abstract class LicenseSource 
    {
        protected string Location;
        
        protected LicenseSource(string location)
        {
            Location = location;
        }

        public abstract LicenseSourceResult Find(string applicationName);

        protected LicenseSourceResult ValidateLicense(string licenseText, string applicationName)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentException("No application name specified");
            }

            var result = new LicenseSourceResult{Location = Location};

            Exception validationFailure;
            if (!LicenseVerifier.TryVerify(licenseText, out validationFailure))
            {
                result.Result = $"License found at '{Location}' is not valid - {validationFailure.Message}";
                return result;
            }

            License license;
            try
            {
                license = LicenseDeserializer.Deserialize(licenseText);
            }
            catch
            {
                result.Result = $"License found at '{Location}' could not be deserialized";
                return result;
            }

            if (license.ValidForApplication(applicationName))
            {
                result.License = license;
                result.Result = $"License found at '{Location}'";
            }
            else
            {
                result.Result = $"License found at '{Location}' was not valid for '{applicationName}'. Valid apps: '{string.Join(",", license.ValidApplications)}'";
            }
            return result;
        }
    }
}
