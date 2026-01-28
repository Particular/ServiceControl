namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.Infrastructure;
    using ServiceControl.Transports;

    [TestFixture]
    public class TransportManifestLibraryTests
    {
        const string transportName = "NetStandardAzureServiceBus";
        const string transportType = "ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS";
        const string transportAlias = "ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus";

        [SetUp]
        public void SetUp()
        {
            LoggerUtil.ActiveLoggers = Loggers.Test;
        }

        [Test]
        public void Should_find_transport_manifest_by_name()
        {
            var transportManifest = TransportManifestLibrary.Find(transportName);

            Assert.That(transportManifest, Is.Not.Null);
            Assert.That(transportManifest.Name, Is.EqualTo(transportName));
        }

        [Test]
        public void Should_find_transport_manifest_by_type()
        {
            var transportManifest = TransportManifestLibrary.Find(transportType);

            Assert.That(transportManifest, Is.Not.Null);
            Assert.That(transportManifest.TypeName, Is.EqualTo(transportType));
        }

        [Test]
        public void Should_find_transport_manifest_by_alias()
        {
            var transportManifest = TransportManifestLibrary.Find(transportAlias);

            Assert.That(transportManifest, Is.Not.Null);
            Assert.That(transportManifest.Aliases[0], Is.EqualTo(transportAlias));
        }

        [Test]
        public void Should_return_null_for_not_found_transport_type()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var transportManifest = TransportManifestLibrary.Find(fakeTransportType);

            Assert.That(transportManifest, Is.Null);
        }

        [Test]
        public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
        {
            var foundTransportNames = new List<string>();

            foreach (var definition in TransportManifestLibrary.TransportManifests.SelectMany(t => t.Definitions))
            {
                foundTransportNames.Add(definition.Name);

                var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
                var resolver = new PathAssemblyResolver(runtimeAssemblies);
                var metadataLoadContext = new MetadataLoadContext(resolver);

                var assemblyName = definition.TypeName.Split(',')[1].Trim();
                var assemblyFile = Path.Combine(definition.Location, assemblyName + ".dll");

                var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyFile);
                Assert.That(assembly, Is.Not.Null, $"Could not load assembly {assemblyName}");

                var typeFullName = definition.TypeName.Split(',').FirstOrDefault();
                var foundType = assembly.GetType(typeFullName);
                Assert.That(foundType, Is.Not.Null, $"Transport type {definition.TypeName} not found in assembly {assemblyName}");
            }

            foundTransportNames.Sort();

            TestContext.Error.WriteLine("Found Transports: " + string.Join(", ", foundTransportNames));

            var assemblyLocation = typeof(TransportManifestLibrary).Assembly.Location;
            var locationPath = Path.GetDirectoryName(assemblyLocation);
            foreach (var path in Directory.EnumerateFiles(locationPath!, "*.*", SearchOption.AllDirectories))
            {
                TestContext.Error.WriteLine("Found file: " + path);
            }

            Approver.Verify(foundTransportNames);
        }
    }
}