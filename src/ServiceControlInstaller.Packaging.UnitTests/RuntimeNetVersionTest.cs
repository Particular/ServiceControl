namespace ServiceControlInstaller.Packaging.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [TestFixture]
    class RuntimeNetVersionTest
    {
        [Test]
        public void EnsureCorrectRuntimeVersionIsShipped()
        {
            var aip = GetFromAipFile();
            var raven = GetFromRavenServerRuntimeConfig();

            Console.WriteLine(aip);
            Console.WriteLine(raven);

            const string somethingWrong = "There is something wrong with the logic used to find the .NET version that RavenDB needs the installer to install.";

            Assert.IsNotNull(aip.NetRuntimeMin, somethingWrong);
            Assert.IsNotNull(aip.NetRuntimeMax, somethingWrong);
            Assert.IsNotNull(aip.AspNetCoreMin, somethingWrong);
            Assert.IsNotNull(aip.NetRuntimeToInstall, somethingWrong);
            Assert.IsNotNull(aip.AspNetCoreToInstall, somethingWrong);

            Assert.IsNotNull(raven.NetRuntime, somethingWrong);
            Assert.IsNotNull(raven.AspNetCore, somethingWrong);

            string howToFix = $"The .NET/AspNetCore runtime that the installer wants to install is too old. Update the AIP file in the AdvancedInstaller software as required by the current version of RavenDB. ({raven})";

            // Major versions must be the same
            Assert.That(aip.NetRuntimeMin.Major == raven.NetRuntime.Major && aip.NetRuntimeMax.Major == raven.NetRuntime.Major, howToFix);
            Assert.That(aip.AspNetCoreMin.Major == raven.AspNetCore.Major, howToFix);

            // And within that major, versions installed must be the same or higher than what Raven requires
            Assert.That(aip.NetRuntimeMin >= raven.NetRuntime, howToFix);
            Assert.That(aip.NetRuntimeMax >= raven.NetRuntime, howToFix);
            Assert.That(aip.AspNetCoreMin >= raven.AspNetCore, howToFix);

            // And check the AIP file as well
            Assert.That(aip.NetRuntimeMin.Major == aip.NetRuntimeMax.Major, "Min and max versions should be on the same major version");
            Assert.That(aip.NetRuntimeMax > aip.NetRuntimeMin, "The maximum .NET version needs to be higher than the minimum .NET version. The upper bound is INCLUSIVE so you can't use the next major like with a NuGet package. Use something like `Major.Minor.100` i.e. `7.0.100`.");
            Assert.That(aip.NetRuntimeToInstall == aip.NetRuntimeMin, "The .NET version to install needs to be the same as the check for which version to install.");
            Assert.That(aip.AspNetCoreToInstall == aip.AspNetCoreMin, "The AspNet version to install needs to be the same as the check for which version to install.");

            // Check convention that the maximum version is patch 100
            Assert.That(aip.NetRuntimeMax.Build == 100, "For the maximum .NET runtime version, use 100 as the patch value. (i.e. 7.0.100)");

            // Check that the ASPNET registry key in the AIP file is also correct
            var desiredRegistryKeyEnding = "v" + aip.AspNetCoreMin.ToString(2);
            Assert.That(aip.AspNetCoreRegistryKey, Does.EndWith(desiredRegistryKeyEnding), "The SearchString registry key must match the AspNetCore major+minor version.");
        }


        static AipVersions GetFromAipFile()
        {
            // P:\ServiceControl\src\ServiceControlInstaller.Packaging.UnitTests\bin\Debug\net472
            var aipPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Setup", "ServiceControl.aip"));

            var doc = new XmlDocument();
            doc.Load(aipPath);

            var versions = new AipVersions();

            var runtimeNode = doc.DocumentElement.SelectSingleNode("/DOCUMENT/COMPONENT/ROW[@SearchKey='C6F7BF650B714DC58735D62C12F214E4M_1']");
            Version.TryParse(runtimeNode.Attributes["VerMin"].Value, out versions.NetRuntimeMin);
            Version.TryParse(runtimeNode.Attributes["VerMax"].Value, out versions.NetRuntimeMax);

            var aspNode = doc.DocumentElement.SelectSingleNode("/DOCUMENT/COMPONENT/ROW[@SearchKey='E25D6A62194038942640BDE651049CASP.N']");
            Version.TryParse(aspNode.Attributes["VerMin"].Value, out versions.AspNetCoreMin);
            versions.AspNetCoreRegistryKey = aspNode.Attributes["SearchString"].Value;

            var runtimeToInstallNode = doc.DocumentElement.SelectSingleNode("/DOCUMENT/COMPONENT/ROW[@PrereqKey='C6F7BF650B714DC58735D62C12F214E4']");
            Version.TryParse(runtimeToInstallNode.Attributes["VersionMin"].Value, out versions.NetRuntimeToInstall);

            var aspnetToInstallNode = doc.DocumentElement.SelectSingleNode("/DOCUMENT/COMPONENT/ROW[@PrereqKey='E25D6A62194038942640BDE651049C']");
            Version.TryParse(aspnetToInstallNode.Attributes["VersionMin"].Value, out versions.AspNetCoreToInstall);

            return versions;
        }

        static RavenServerVersions GetFromRavenServerRuntimeConfig()
        {
            // First get RavenDB package version
            var packageConfigPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Directory.Packages.props"));

            var doc = new XmlDocument();
            doc.Load(packageConfigPath);

            var ravenNode = doc.DocumentElement.SelectSingleNode("/Project/ItemGroup/PackageVersion[@Include='RavenDB.Embedded']");
            var ravenPackageVersion = ravenNode.Attributes["Version"].Value;

            // Now find Raven.Server.runtimeconfig.json
            var nugetPath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
            var packagePath = Path.Combine(nugetPath, "ravendb.embedded", ravenPackageVersion);
            var contentFilesPath = Path.Combine(packagePath, "contentFiles");

            var filePaths = Directory.GetFiles(contentFilesPath, "Raven.Server.runtimeconfig.json", SearchOption.AllDirectories);

            var firstFileText = File.ReadAllText(filePaths.FirstOrDefault());

            var json = JsonConvert.DeserializeObject<JObject>(firstFileText);

            var frameworks = json["runtimeOptions"]["frameworks"] as JArray;

            var versions = new RavenServerVersions();

            foreach (var fw in frameworks.OfType<JObject>())
            {
                var name = fw["name"].Value<string>();
                var versionString = fw["version"].Value<string>();

                if (Version.TryParse(versionString, out var version))
                {
                    if (name == "Microsoft.NETCore.App")
                    {
                        versions.NetRuntime = version;
                    }
                    else if (name == "Microsoft.AspNetCore.App")
                    {
                        versions.AspNetCore = version;
                    }
                }
            }

            return versions;
        }

        class AipVersions
        {
            public Version NetRuntimeMin;
            public Version NetRuntimeMax;
            public Version AspNetCoreMin;
            public string AspNetCoreRegistryKey;

            public Version NetRuntimeToInstall;
            public Version AspNetCoreToInstall;

            public override string ToString() => $"AipVersions: NetRuntimeMin = {NetRuntimeMin}, NetRuntimeMax = {NetRuntimeMax}, AspNetCoreMin = {AspNetCoreMin}";
        }

        class RavenServerVersions
        {
            public Version NetRuntime;
            public Version AspNetCore;

            public override string ToString() => $"RavenServerVersions: NetCoreApp = {NetRuntime}, AspNetCore = {AspNetCore}";
        }
    }
}
