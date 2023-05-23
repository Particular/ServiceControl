namespace ServiceControl.Transport.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Transports;

    class TransportManifestLibraryTests
    {
        const string transportName = "AzureServiceBus.EndpointOriented";
        const string transportType = "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB";
        const string transportAlias = "ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus";
        const string transportFolderName = "AzureServiceBus";

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

            Assert.AreEqual(transportFolderName, _transportTypeFolder);
        }

        [Test]
        public void Should_find_transport_type_folder_by_type()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportType);

            Assert.AreEqual(transportFolderName, _transportTypeFolder);
        }

        [Test]
        public void Should_find_transport_type_folder_by_alias()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportAlias);

            Assert.AreEqual(transportFolderName, _transportTypeFolder);
        }

        [Test]
        public void Should_return_null_for_not_found_transport_type()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(fakeTransportType);

            Assert.IsNull(_transportTypeFolder);
        }
    }
}