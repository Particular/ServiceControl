namespace Particular.Licensing
{
    using System;
    using System.IO;

    public class FilePathLicenseStore
    {
        public static readonly string ApplicationLevelLicenseLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.xml");
        public static readonly string MachineLevelLicenseLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ParticularSoftware", "license.xml");
        public static readonly string UserLevelLicenseLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParticularSoftware", "license.xml");

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