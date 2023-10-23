namespace ServiceControl.Transport.Tests
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using ServiceControl.Transports;

    [TestFixture]
    public class TransportManifestLibraryTests
    {
        const string transportName = "AzureServiceBus.EndpointOriented";
        const string transportType = "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB";
        const string transportAlias = "ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus";

        [Test]
        public void Should_find_transport_type_by_name()
        {
            var _transportType = TransportManifestLibrary.Find(transportName);

            Assert.AreEqual(transportType, _transportType);
        }

        [Test]
        public void Should_find_transport_type_by_type()
        {
            var _transportType = TransportManifestLibrary.Find(transportType);

            Assert.AreEqual(transportType, _transportType);
        }

        [Test]
        public void Should_find_transport_type_by_alias()
        {
            var _transportType = TransportManifestLibrary.Find(transportAlias);

            Assert.AreEqual(transportType, _transportType);
        }

        [Test]
        public void Should_return_transport_type_passed_in_if_not_found()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var _transportType = TransportManifestLibrary.Find(fakeTransportType);

            Assert.AreEqual(fakeTransportType, _transportType);
        }

        [Test]
        public void Should_find_transport_type_folder_by_name()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportName);

            Assert.IsNotNull(_transportTypeFolder);
        }

        [Test]
        public void Should_find_transport_type_folder_by_type()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportType);

            Assert.IsNotNull(_transportTypeFolder);
        }

        [Test]
        public void Should_find_transport_type_folder_by_alias()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportAlias);

            Assert.IsNotNull(_transportTypeFolder);
        }

        [Test]
        public void Should_return_null_for_not_found_transport_type()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(fakeTransportType);

            Assert.IsNull(_transportTypeFolder);
        }

        [Test]
        public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
        {
            var count = 0;

            foreach (var definition in TransportManifestLibrary.TransportManifests.SelectMany(t => t.Definitions))
            {
                count++;
                var transportFolder = TransportManifestLibrary.GetTransportFolder(definition.Name);
                var assemblyName = definition.TypeName.Split(',')[1].Trim();
                var assemblyFile = Path.Combine(transportFolder, assemblyName + ".dll");
                var assembly = Assembly.LoadFrom(assemblyFile);

                Assert.IsNotNull(assembly, $"Could not load assembly {assemblyName}");
                Assert.IsTrue(assembly.GetTypes().Any(a => a.FullName == definition.TypeName.Split(',').FirstOrDefault() && a.Namespace == assemblyName), $"Transport type {definition.TypeName} not found in assembly {assemblyName}");
            }

            Assert.NotZero(count, "No transport manifests found.");
        }
    }
}