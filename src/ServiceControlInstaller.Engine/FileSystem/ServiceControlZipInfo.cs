namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Ionic.Zip;
    using ServiceControlInstaller.Engine.Instances;

    public class ServiceControlZipInfo
    {
        public string FilePath { get; private set; }
        public Version Version { get; private set; }
        public bool Present { get; private set; }

        public static ServiceControlZipInfo Find(string deploymentCachePath)
        {
            var list = new Dictionary<string, Version>();
            var fileRegex = new Regex(@"particular.servicecontrol-(?<version>\d+\.\d+\.\d+)\.zip", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var deploymentCache = new DirectoryInfo(deploymentCachePath);

            foreach (var file in deploymentCache.GetFiles("particular.servicecontrol-*.zip", SearchOption.TopDirectoryOnly))
            {
                var matchResult = fileRegex.Match(file.Name);
                if (!matchResult.Success)
                {
                    continue;
                }
                var v = new Version(matchResult.Groups["version"].Value);
                list.Add(file.FullName, v);
            }

            if (list.Count == 0)
            {
                return new ServiceControlZipInfo();
            }

            var latest = list.OrderByDescending(p => p.Value).First();
            return new ServiceControlZipInfo
            {
                FilePath = latest.Key,
                Version = latest.Value,
                Present = true
            };
        }

        public void ValidateZip()
        {
            if (!Present)
                throw new FileNotFoundException("No ServiceControl zip file found", FilePath);

            if (!ZipFile.CheckZip(FilePath))
                throw new Exception($"Corrupt Zip File - {FilePath}");
        }

        public bool TryReadServiceControlReleaseDate(out DateTime releaseDate)
        {
            releaseDate = DateTime.MinValue;
            var tempFile = Path.Combine(Path.GetTempPath(), $@"ServiceControl\{Constants.ServiceControlExe}");
            try
            {
                using (var zip = ZipFile.Read(FilePath))
                {
                    var entry = zip.Entries.FirstOrDefault(p => p.FileName == $"ServiceControl/{Constants.ServiceControlExe}");
                    if (entry == null)
                    {
                        return false;
                    }
                    entry.Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                    return ReleaseDateReader.TryReadReleaseDateAttribute(tempFile, out releaseDate);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}