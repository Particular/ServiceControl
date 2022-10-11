namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    class PlatformZipInfoFinder
    {
        readonly string mainEntryPoint;
        readonly string name;
        readonly Regex fileRegex;
        readonly string searchPattern;

        public PlatformZipInfoFinder(string packageFileNamePrefix, string mainEntryPoint, string name)
        {
            this.mainEntryPoint = mainEntryPoint;
            this.name = name;
            fileRegex = new Regex(packageFileNamePrefix + @"-(?<version>\d+\.\d+\.\d+)\.zip", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            searchPattern = packageFileNamePrefix + "-*.zip";
        }

        public PlatformZipInfo Find(string deploymentCachePath) => Find(new DirectoryInfo(deploymentCachePath));

        PlatformZipInfo Find(DirectoryInfo deploymentCache)
        {
            for (var folder = deploymentCache; folder != null; folder = folder.Parent)
            {
                var fromHere = Get(folder);
                if (fromHere != null)
                {
                    return fromHere;
                }

                var zipFolder = folder.EnumerateDirectories("zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (zipFolder != null)
                {
                    var fromZipFolder = Get(zipFolder);
                    if (fromZipFolder != null)
                    {
                        return fromZipFolder;
                    }
                }
            }

            return PlatformZipInfo.Empty;
        }

        PlatformZipInfo Get(DirectoryInfo deploymentCache)
        {
            var list = new Dictionary<string, Version>();

            foreach (var file in deploymentCache.GetFiles(searchPattern, SearchOption.TopDirectoryOnly))
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
                return null;
            }

            var latest = list.OrderByDescending(p => p.Value).First();
            return new PlatformZipInfo(mainEntryPoint, name, filePath: latest.Key, version: latest.Value, present: true);
        }
    }
}