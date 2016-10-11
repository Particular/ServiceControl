namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Ionic.Zip;

    public class ServiceControlZipInfo
    {
        public string FilePath { get; private set; }
        public Version Version { get; private set; }
        public bool Present { get; private set; }

        /// <summary>
        /// Find the latest servicecontrol zip based on the version number in the file name - file name must be in form particular.servicecontrol-&lt;major&gt;.&lt;minor&gt;.&lt;patch&gt;.zip
        /// </summary>
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
                throw new Exception("No ServiceControl zip file found");

            if (!ZipFile.CheckZip(FilePath))
                throw new Exception($"Corrupt Zip File - {FilePath}");
        }

        public bool TryReadServiceControlReleaseDate(out DateTime releaseDate)
        {
            releaseDate = DateTime.MinValue;
            try
            {
                using (var zip = ZipFile.Read(FilePath))
                using (var stream = new MemoryStream())
                {
                    var entry = zip.Entries.FirstOrDefault(p => p.FileName == "ServiceControl/ServiceControl.exe");
                    if (entry == null)
                    {
                        return false;
                    }
                    entry.Extract(stream);
                    stream.Position = 0;
                    var data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);

                    var assembly = Assembly.ReflectionOnlyLoad(data);
                    var releaseDateAttribute = assembly.GetCustomAttributesData().FirstOrDefault(p => p.Constructor?.ReflectedType?.Name == "ReleaseDateAttribute");
                    var x = (string) releaseDateAttribute?.ConstructorArguments[0].Value;
                    releaseDate = DateTime.ParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}