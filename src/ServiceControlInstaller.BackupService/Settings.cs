// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.BackupStubService
{
    using System;
    using System.IO;
    using System.Linq;

    public class Settings
    {
        public int Port =>  SettingsReader<int>.Read("Port", 33333);
        public string HostName => SettingsReader<string>.Read("HostName", "localhost");
        public string DBPath => SettingsReader<string>.Read("DBPath", DefaultDBPath());
        public string VirtualDirectory => SettingsReader<string>.Read("VirtualDirectory", string.Empty);

        string DefaultDBPath()
        {
            var host = (HostName == "*") ? "%" : HostName;
            var dbFolder = $"{host}-{Port}";
            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                dbFolder += $"-{SanitizeFolderName(VirtualDirectory)}";
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", dbFolder);
        }

        static string SanitizeFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }
    }
}