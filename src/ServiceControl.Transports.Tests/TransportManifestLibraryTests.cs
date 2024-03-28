namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.Transports;

    [TestFixture]
    public class TransportManifestLibraryTests
    {
        const string transportName = "NetStandardAzureServiceBus";
        const string transportType = "ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS";
        const string transportAlias = "ServiceControl.Transports.AzureServiceBus.AzureServiceBusTransport, ServiceControl.Transports.AzureServiceBus";

        [Test]
        public void Should_find_transport_manifest_by_name()
        {
            var transportManifest = TransportManifestLibrary.Find(transportName);

            Assert.IsNotNull(transportManifest);
            Assert.AreEqual(transportName, transportManifest.Name);
        }

        [Test]
        public void Should_find_transport_manifest_by_type()
        {
            var transportManifest = TransportManifestLibrary.Find(transportType);

            Assert.IsNotNull(transportManifest);
            Assert.AreEqual(transportType, transportManifest.TypeName);
        }

        [Test]
        public void Should_find_transport_manifest_by_alias()
        {
            var transportManifest = TransportManifestLibrary.Find(transportAlias);

            Assert.IsNotNull(transportManifest);
            Assert.AreEqual(transportAlias, transportManifest.Aliases[0]);
        }

        [Test]
        public void Should_return_null_for_not_found_transport_type()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var transportManifest = TransportManifestLibrary.Find(fakeTransportType);

            Assert.IsNull(transportManifest);
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
                Assert.IsNotNull(assembly, $"Could not load assembly {assemblyName}");

                var typeFullName = definition.TypeName.Split(',').FirstOrDefault();
                var foundType = assembly.GetType(typeFullName);
                Assert.IsNotNull(foundType, $"Transport type {definition.TypeName} not found in assembly {assemblyName}");
            }

            foundTransportNames.Sort();
            Approver.Verify(foundTransportNames);
        }
    }
}