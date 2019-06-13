namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Instances;

    public static class MonitoringZipInfo
    {
        /// <summary>
        /// Find the latest servicecontrol zip based on the version number in the file name - file name must be in form
        /// particular.servicecontrol-&lt;major&gt;.&lt;minor&gt;.&lt;patch&gt;.zip
        /// </summary>
        public static PlatformZipInfo Find(string deploymentCachePath)
        {
            var list = new Dictionary<string, Version>();
            var fileRegex = new Regex(@"particular.servicecontrol.monitoring-(?<version>\d+\.\d+\.\d+)\.zip", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var deploymentCache = new DirectoryInfo(deploymentCachePath);

            foreach (var file in deploymentCache.GetFiles("particular.servicecontrol.monitoring-*.zip", SearchOption.TopDirectoryOnly))
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
            return new PlatformZipInfo(Constants.MonitoringExe, "ServiceControl Monitoring", filePath: latest.Key, version: latest.Value, present: true);
        }
    }
}