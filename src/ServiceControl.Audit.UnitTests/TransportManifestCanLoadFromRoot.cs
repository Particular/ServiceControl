﻿namespace ServiceControl.Audit.UnitTests
{
    using NUnit.Framework;
    using ServiceControl.Transports;

    [TestFixture]
    public class TransportManifestCanLoadFromRoot
    {
        const string transportName = "learningTransport";
        const string transportType = "ServiceControl.Transports.Learning.LearningTransportCustomization, ServiceControl.Transports.Learning";

        [Test]
        public void Should_find_transport_type_by_name()
        {
            var _transportType = TransportManifestLibrary.Find(transportName);

            Assert.AreEqual(transportType, _transportType);
        }
    }
}
