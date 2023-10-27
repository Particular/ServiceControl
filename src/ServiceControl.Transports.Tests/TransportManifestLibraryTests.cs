namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using Particular.Approvals;
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
            var foundTransportNames = new List<string>();

            foreach (var definition in TransportManifestLibrary.TransportManifests.SelectMany(t => t.Definitions))
            {
                foundTransportNames.Add(definition.Name);

                var transportFolder = TransportManifestLibrary.GetTransportFolder(definition.Name);
                var assemblyName = definition.TypeName.Split(',')[1].Trim();
                var assemblyFile = Path.Combine(transportFolder, assemblyName + ".dll");
                var assembly = Assembly.LoadFrom(assemblyFile);

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