namespace ServiceControl.UnitTests.Heartbeats
{
    using System;
    using Contracts.Operations;
    using HeartbeatMonitoring;
    using NUnit.Framework;

    [TestFixture]
    public class CurrentHeartbeatStatusTest
    {
        [Test]
        public void When_A_New_Endpoint_Is_Detected_Should_List_As_Inactive()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var stats = currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails(){Host="Machine", HostId = Guid.NewGuid(), Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 0, 1);
        }

        [Test]
        public void When_Heartbeating_Endpoint_Is_Detected_That_Is_Already_Known()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            var stats = currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 1, 0);
        }

        [Test]
        public void When_Heartbeating_Endpoint_Is_Detected_For_A_Known_Endpoint_That_Has_No_Prior_HostId()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails() { Host = "Machine", Name = "NewEndpoint" });
            var stats = currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 1, 0);
        }

        [Test]
        public void When_Heartbeating_Endpoint_No_Longer_Sends_Heartbats()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            var stats = currentHeartbeatStatus.RegisterEndpointThatFailedToHeartbeat(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 0, 1);
        }

        [Test]
        public void When_Endpoint_Heartbeats_Is_Restored()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterEndpointThatFailedToHeartbeat(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            var stats = currentHeartbeatStatus.RegisterEndpointWhoseHeartbeatIsRestored(new EndpointDetails() { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 1, 0);
        }

        [Test]
        public void When_A_Heartbeating_Endpoint_Is_Unmonitored()
        {
            //TODO
        }

        [Test]
        public void When_A_Heartbeating_Endpoint_Is_Monitored()
        {
            //TODO
        }

        [Test]
        public void When_A_Dead_Endpoint_Is_Unmonitored()
        {
            //TODO
        }

        [Test]
        public void When_A_Dead_Endpoint_Is_Monitored()
        {
            //TODO
        }

        static void VerifyHeartbeatStats(HeartbeatsComputation.HeartbeatsStats stats, int expectedActive, int expectedDead)
        {
            Assert.AreEqual(expectedDead, stats.Dead);
            Assert.AreEqual(expectedActive, stats.Active);
        }
    }
}
