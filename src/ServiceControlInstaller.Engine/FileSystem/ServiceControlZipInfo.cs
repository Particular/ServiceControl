namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Collections.Generic;
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
            var tempDomain = AppDomain.CreateDomain("TemporaryAppDomain");
            var tempFile = Path.Combine(Path.GetTempPath(), @"ServiceControl\ServiceControl.exe");
            try
            {
                using (var zip = ZipFile.Read(FilePath))
                {
                    var entry = zip.Entries.FirstOrDefault(p => p.FileName == "ServiceControl/ServiceControl.exe");
                    if (entry == null)
                    {
                        return false;
                    }
                    entry.Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                    var loaderType = typeof(AssemblyReleaseDateReader);
                    var loader = (AssemblyReleaseDateReader) tempDomain.CreateInstanceFrom(Assembly.GetExecutingAssembly().Location, loaderType.FullName).Unwrap();
                    releaseDate = loader.GetReleaseDate(tempFile);
                    return true;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                AppDomain.Unload(tempDomain);
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        class AssemblyReleaseDateReader : MarshalByRefObject
        {
            internal DateTime GetReleaseDate(string assemblyPath)
            {
                try
                {
                    var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                    var releaseDateAttribute = assembly.GetCustomAttributesData().FirstOrDefault(p => p.Constructor?.ReflectedType?.Name == "ReleaseDateAttribute");
                    var x = (string) releaseDateAttribute?.ConstructorArguments[0].Value;
                    return DateTime.ParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return DateTime.MinValue;
                }
            }
        }
    }
}