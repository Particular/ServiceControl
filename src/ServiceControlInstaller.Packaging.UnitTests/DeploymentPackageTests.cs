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
        public void DuplicateAssemblyShouldHaveMatchingVersions()
        {
            var rootDirectory = deploymentPackage.Directory;

            DirectoryAssert.Exists($"{rootDirectory.FullName}/{deploymentPackage.ServiceName}", $"Expected a {deploymentPackage.ServiceName} folder");
            DirectoryAssert.Exists($"{rootDirectory.FullName}/Transports", $"Expected a Transports folder");

            var serviceDirectory = rootDirectory.GetDirectories(deploymentPackage.ServiceName).Single();
            var serviceAssemblies = serviceDirectory.EnumerateFiles();

            var componentCategoryDirectories = rootDirectory.GetDirectories()
                .Where(d => d.Name != serviceDirectory.Name);

            var detectedMismatches = new List<string>();

            foreach (var componentCategoryDirectory in componentCategoryDirectories)
            {
                foreach (var componentDirectory in componentCategoryDirectory.GetDirectories())
                {
                    var componentAssemblies = componentDirectory.EnumerateFiles();
                    var duplicateAssemblies = serviceAssemblies.Where(sa => componentAssemblies.Any(ca => ca.Name == sa.Name));

                    foreach (var componentAssembly in componentAssemblies)
                    {
                        var serviceAssembly = serviceAssemblies.SingleOrDefault(sa => sa.Name == componentAssembly.Name);

                        if (serviceAssembly == null)
                        {
                            continue;
                        }

                        var serviceVersion = FileVersionInfo.GetVersionInfo(serviceAssembly.FullName).ProductVersion;
                        var componentVersion = FileVersionInfo.GetVersionInfo(componentAssembly.FullName).ProductVersion;

                        if (serviceVersion == componentVersion)
                        {
                            continue;
                        }

                        var componentAssemblyFullname = $"{componentCategoryDirectory}/{componentDirectory}/{componentAssembly}";
                        var mismatch = $"{componentAssemblyFullname} has a version mismatch: {serviceVersion} | {componentVersion}";

                        if (IgnoreList.Contains(componentAssemblyFullname))
                        {
                            TestContext.Out.WriteLine($"IGNORED: {mismatch}");
                            continue;
                        }

                        detectedMismatches.Add($"{componentAssemblyFullname} has a version mismatch is set to be ignored ({serviceVersion} vs {componentVersion})");
                    }
                }
            }

            CollectionAssert.IsEmpty(detectedMismatches, $"Component assembly version missmatch detected");
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
                "MSMQ",
                "AmazonSQS",
                "LearningTransport"};


            var transportDirectories = deploymentPackage.Directory.GetDirectories("Transports/*");

            CollectionAssert.AreEquivalent(allTransports, transportDirectories.Select(d => d.Name), $"Expected transports folder to contain {string.Join(",", allTransports)}");
        }

        static IEnumerable<string> IgnoreList
        {
            get
            {
                // NServiceBus.Transport.Msmq references V5.0.0 of this assembly,
                // but only for the native delayed delivery codepath so is not used by ServiceControl.
                // Updating the reference will in turn break NServiceBus.Transport.RabbitMQ
                // as that package references V4.7.1.
                // It should therefore be safe to explicitly exclude this assembly mismatch from
                // the test.
                //yield return "Transports/MSMQ/System.Runtime.CompilerServices.Unsafe.dll";
                yield return "Transports/MSMQ/System.Threading.Channels.dll";
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}