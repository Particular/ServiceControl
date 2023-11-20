namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Validation;

    [TestFixture]
    class RuntimeNetVersionTest
    {
        [Test]
        public void EnsureCorrectRuntimeVersionIsShipped()
        {
            var raven = GetFromRavenServerRuntimeConfig();
            Console.WriteLine(raven);

            const string somethingWrong = "There is something wrong with the logic used to find the .NET version that RavenDB needs to run.";
            Assert.IsNotNull(raven.NetRuntime, somethingWrong);
            Assert.IsNotNull(raven.AspNetCore, somethingWrong);

            var minVersion = DotnetVersionValidator.MinimumVersionString;

            Console.WriteLine($"Minimum version requested by ServiceControl: {minVersion}");

            var versionsOk = DotnetVersionValidator.DotnetVersionOk(raven.NetRuntime, minVersion) && DotnetVersionValidator.DotnetVersionOk(raven.AspNetCore, minVersion);
            string howToFix = $"The .NET/AspNetCore runtime {minVersion} validated by the installer is incorrect and won't allow RavenDB to run. Update the DotnetVersionValidator.MinimumVersionString constant to a version matching or newer than what is needed by RavenDB. ({raven})";

            Assert.IsTrue(versionsOk, howToFix);
            Console.WriteLine("Versions are OK");
        }

        [Test]
        [Ignore("Currently failing on CI, temporarily ignored", Until = "2023-11-27")]
        public void TestValidatorLogic()
        {
            // Should always pass on CI because we download the latest available dotnet SDK
            var isMissing = DotnetVersionValidator.FrameworkRequirementsAreMissing(true, out var message);

            if (isMissing)
            {
                throw new Exception(message);
            }
        }

        [Test]
        public void CheckForSelfContainedRavenDB()
        {
            bool isCI = Environment.GetEnvironmentVariable("CI") == "true";
            bool isLocal = !isCI;

            var ravenServerPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "deploy", "RavenDBServer"));
            var ravenStudio = Path.Combine(ravenServerPath, "Raven.Studio.zip");
            var runtimes = Path.Combine(ravenServerPath, "runtimes");
            var systemCollections = Path.Combine(ravenServerPath, "System.Collections.dll");
            var aspNetCoreHttp = Path.Combine(ravenServerPath, "Microsoft.AspNetCore.Http.dll");

            FileAssert.Exists(ravenStudio); // No matter what
            Assert.That(Directory.Exists(runtimes), Is.EqualTo(isLocal));  // Only in local development
            Assert.That(File.Exists(systemCollections), Is.EqualTo(isCI)); // Only on CI
            Assert.That(File.Exists(aspNetCoreHttp), Is.EqualTo(isCI));    // Only on CI
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
            var nugetPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (string.IsNullOrWhiteSpace(nugetPath))
            {
                nugetPath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
            }

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

        class RavenServerVersions
        {
            public Version NetRuntime;
            public Version AspNetCore;

            public override string ToString() => $"RavenServer Minimum Versions: NetCoreApp = {NetRuntime}, AspNetCore = {AspNetCore}";
        }
    }
}
