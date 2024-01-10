namespace Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using NUnit.Framework;

    [TestFixtureSource(typeof(DeploymentPackage), nameof(DeploymentPackage.All))]
    public class DeploymentPackageTests
    {
        public DeploymentPackageTests(DeploymentPackage deploymentPackage)
        {
            this.deploymentPackage = deploymentPackage;
        }

        [Test]
        public void CheckPackageStructure()
        {
            var rootDirectory = deploymentPackage.Directory;

            DirectoryAssert.Exists($"{rootDirectory.FullName}/{deploymentPackage.ServiceName}", $"Expected a {deploymentPackage.ServiceName} folder");
            DirectoryAssert.DoesNotExist($"{rootDirectory.FullName}/Transports", $"Transports folder should be a separate asset");
            DirectoryAssert.Exists($"{rootDirectory.FullName}/../Transports", $"Transports folder should be a separate asset");

            foreach (var deploymentUnit in deploymentPackage.DeploymentUnits)
            {
                Assert.False(string.IsNullOrEmpty(deploymentUnit.Category), "All deployment units should have a category");
                CollectionAssert.IsNotEmpty(deploymentUnit.Files, "All deployment units should have assemblies");
            }
        }

        [Test]
        public void DuplicateAssemblyShouldHaveMatchingVersions()
        {
            var rootDirectory = deploymentPackage.Directory;

            var serviceDirectory = rootDirectory.GetDirectories(deploymentPackage.ServiceName).Single();
            var serviceAssemblies = serviceDirectory.EnumerateFiles();

            var componentCategoryDirectories = rootDirectory.GetDirectories()
                .Where(d => d.Name != serviceDirectory.Name);

            var detectedMismatches = new List<string>();

            foreach (var leftDeploymentUnit in deploymentPackage.DeploymentUnits)
            {
                //only check for compatibility with units in other categories
                foreach (var rightDeploymentUnit in deploymentPackage.DeploymentUnits.Where(u => u.Category != leftDeploymentUnit.Category))
                {
                    detectedMismatches.AddRange(GetAssemblyMismatches(leftDeploymentUnit, rightDeploymentUnit));
                }
            }

            CollectionAssert.IsEmpty(detectedMismatches, $"Component assembly version mismatch detected");
        }

        IEnumerable<string> GetAssemblyMismatches(DeploymentPackage.DeploymentUnit leftDeploymentUnit, DeploymentPackage.DeploymentUnit rightDeploymentUnit)
        {
            var detectedMismatches = new List<string>();

            foreach (var leftAssembly in leftDeploymentUnit.Files)
            {
                var rightAssembly = rightDeploymentUnit.Files.SingleOrDefault(sa => sa.Name == leftAssembly.Name);

                if (rightAssembly == null)
                {
                    continue;
                }

                var leftVersion = FileVersionInfo.GetVersionInfo(leftAssembly.FullName).FileVersion;
                var rightVersion = FileVersionInfo.GetVersionInfo(rightAssembly.FullName).FileVersion;

                if (leftVersion == rightVersion)
                {
                    continue;
                }

                var mismatch = $"{leftAssembly.Name} has a different version in {leftDeploymentUnit.FullName} compared to {rightDeploymentUnit.FullName}. Add the package to Directory.Packages.props to ensure the same version is used everywhere: {leftVersion} | {rightVersion}";

                var leftAssemblyFullname = $"{leftDeploymentUnit.FullName}/{leftAssembly.Name}";
                var rightAssemblyFullname = $"{rightDeploymentUnit.FullName}/{rightAssembly.Name}";

                detectedMismatches.Add(mismatch);
            }

            return detectedMismatches;
        }

        [Test]
        public void Should_package_transports_individually()
        {
            var allTransports = new string[] {
                "SQLServer",
                "AzureStorageQueue",
                "AzureServiceBus",
                "NetStandardAzureServiceBus",
                "RabbitMQ",
                //"MSMQ", TODO Uncomment when we add MSMQ back in
                "AmazonSQS",
                "LearningTransport"};


            var transportsIfBundledInAppZips = deploymentPackage.DeploymentUnits
                .Where(u => u.Category == "Transports")
                .Select(u => u.Name);

            CollectionAssert.IsEmpty(transportsIfBundledInAppZips, "Transports should not be bundled inside app zips to conserve space");

            var trueTransportDirectories = deploymentPackage.Directory
                .Parent
                .GetDirectories("Transports")
                .Single()
                .GetDirectories()
                .Select(di => di.Name);

            CollectionAssert.AreEquivalent(allTransports, trueTransportDirectories, $"Expected transports folder to contain {string.Join(",", allTransports)}");
        }

        readonly DeploymentPackage deploymentPackage;
    }
}