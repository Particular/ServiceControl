namespace Particular.Licensing
{
    using System.IO;

    class LicenseSourceFilePath : LicenseSource
    {
        public LicenseSourceFilePath(string path) : base(path)
        {
            
        }

        public override LicenseSourceResult Find(string applicationName)
        {
            if (File.Exists(Location))
            {
                return ValidateLicense(NonBlockingReader.ReadAllTextWithoutLocking(Location), applicationName);
            }

            return new LicenseSourceResult
            {
                Location = Location,
                Result = $"License not found in {Location}"
            };
        }
    }
}

