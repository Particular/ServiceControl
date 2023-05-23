namespace ServiceControl.Transport.Tests
{
    using NUnit.Framework;
    using ServiceControl.Transports;

    class TransportManifestLibraryTests
    {
        const string transportName = "MSMQ";
        const string transportType = "ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq";
        const string transportAlias = "NServiceBus.MsmqTransport, NServiceBus.Transport.Msmq";

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
    }
}