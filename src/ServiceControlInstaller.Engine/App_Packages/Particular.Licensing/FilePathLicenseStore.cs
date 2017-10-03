namespace Particular.Licensing
{
    using System;
    using System.IO;

    using static System.Environment;

    class FilePathLicenseStore
    {
        public static readonly string ApplicationLevelLicenseLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.xml");
        public static readonly string MachineLevelLicenseLocation = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData, SpecialFolderOption.DoNotVerify), "ParticularSoftware", "license.xml");
        public static readonly string UserLevelLicenseLocation = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData, SpecialFolderOption.DoNotVerify), "ParticularSoftware", "license.xml");

        public void StoreLicense(string filePath, string license)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, license);
        }
    }
}