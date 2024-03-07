namespace Tests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
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

            DirectoryAssert.Exists($"{rootDirectory.FullName}", $"Expected a {rootDirectory.Name} folder");
            DirectoryAssert.Exists($"{rootDirectory.FullName}/Transports", $"Expected a Transports subfolder in the {rootDirectory.Name} folder");

            foreach (var deploymentUnit in deploymentPackage.DeploymentUnits)
            {
                Assert.False(string.IsNullOrEmpty(deploymentUnit.Category), "All deployment units should have a category");
                CollectionAssert.IsNotEmpty(deploymentUnit.Files, "All deployment units should have assemblies");
            }
        }

        [Test]
        public void DuplicateAssemblyShouldHaveMatchingVersions()
        {
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
                var leftAssemblyRelativePath = Path.GetRelativePath(leftDeploymentUnit.Directory.FullName, leftAssembly.FullName);
                var rightAssembly = rightDeploymentUnit.Files.SingleOrDefault(sa => Path.GetRelativePath(rightDeploymentUnit.Directory.FullName, sa.FullName) == leftAssemblyRelativePath);

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

                detectedMismatches.Add(mismatch);
            }

            return detectedMismatches;
        }

        [Test]
        public void Should_package_all_transports()
        {
            var allTransports = new string[] {
                "SQLServer",
                "AzureStorageQueue",
                "AzureServiceBus",
                "NetStandardAzureServiceBus",
                "RabbitMQ",
                "MSMQ",
                "AmazonSQS",
                "LearningTransport"};

            var bundledTransports = deploymentPackage.DeploymentUnits
                .Where(u => u.Category == "Transports")
                .Select(u => u.Name);

            CollectionAssert.AreEquivalent(allTransports, bundledTransports, $"Expected transports folder to contain {string.Join(",", allTransports)}");
        }

        readonly DeploymentPackage deploymentPackage;
    }
}