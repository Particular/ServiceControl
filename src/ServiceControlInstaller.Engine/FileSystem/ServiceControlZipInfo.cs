namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Instances;

    public static class ServiceControlZipInfo
    {
        public static PlatformZipInfo Find(string deploymentCachePath)
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
                return PlatformZipInfo.Empty;
            }

            var latest = list.OrderByDescending(p => p.Value).First();
            return new PlatformZipInfo(mainEntrypoint: Constants.ServiceControlExe, name: "ServiceControl", filePath: latest.Key, version: latest.Value, present: true);
        }
    }
}