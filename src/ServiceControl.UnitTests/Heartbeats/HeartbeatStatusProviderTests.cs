namespace ServiceControl.UnitTests.Heartbeats
{
    using System;
    using System.Linq;
    using Contracts.Operations;
    using HeartbeatMonitoring;
    using NUnit.Framework;

    [TestFixture]
    public class HeartbeatStatusProviderTests
    {
        [Test]
        public void When_A_New_Endpoint_Is_Detected_Should_List_As_Inactive()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();

            VerifyHeartbeatStats(currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails{ Host = "Machine", HostId = hostId, Name = "NewEndpoint1" }), 0, 1);

            VerifyHeartbeatStats(currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails{ Host = "Machine", HostId = hostId, Name = "NewEndpoint2" }), 0, 2);

        }


        [Test]
        public void When_A_New_Endpoint_Is_Detected_Should_not_override_if_endpoint_already_exists()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();

            VerifyHeartbeatStats(currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint1" },DateTime.UtcNow), 1, 0);

            VerifyHeartbeatStats(currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint1" }), 1, 0);

        }


        [Test]
        public void When_heartbeats_are_enabled_should_update_existing_endpoint_host_id_if_needed()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();

            VerifyHeartbeatStats(currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails { Host = "Machine", Name = "NewEndpoint1" }), 0, 1); 
            VerifyHeartbeatStats(currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint1" }, DateTime.UtcNow), 1, 0);

            var endpoint = currentHeartbeatStatus.GetPotentiallyFailedEndpoints(DateTime.UtcNow.AddDays(1)).SingleOrDefault();

            
            Assert.AreEqual(hostId,endpoint.Details.HostId);
        }


        [Test]
        public void When_endpoint_is_disabled_should_not_count()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();

            var endpoint = new EndpointDetails
            {
                Host = "Machine",
                HostId = Guid.NewGuid(),
                Name = "NewEndpoint"
            };
            currentHeartbeatStatus.RegisterNewEndpoint(endpoint);
            var stats = currentHeartbeatStatus.DisableMonitoring(endpoint);
            
            VerifyHeartbeatStats(stats, 0, 0);

            //enable and make sure it counts again
            stats = currentHeartbeatStatus.EnableMonitoring(endpoint);

            VerifyHeartbeatStats(stats, 0, 1);

        }

        [Test]
        public void When_A_New_Endpoint_without_a_hostid_Is_Detected_Should_List_As_Inactive()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var stats = currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails { Host = "Machine", Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 0, 1);

            //make sure to handles duplicates as well
            stats = currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails { Host = "Machine", Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 0, 1);
        }

        [Test]
        public void When_Heartbeating_Endpoint_Is_Detected_That_Is_Already_Known()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            var stats = currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint" }, DateTime.UtcNow);
            VerifyHeartbeatStats(stats, 1, 0);
        }

        [Test]
        public void When_Heartbeating_Endpoint_Is_Detected_For_A_Known_Endpoint_That_Has_No_Prior_HostId()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            var endpointWithNoHostId = new EndpointDetails
            {
                Host = "Machine", 
                Name = "NewEndpoint"
            };
            currentHeartbeatStatus.RegisterNewEndpoint(endpointWithNoHostId);
            var endpointWithHostId = new EndpointDetails
            {
                Host = "Machine", 
                HostId = hostId, 
                Name = "NewEndpoint"
            };
            var stats = currentHeartbeatStatus.RegisterHeartbeatingEndpoint(endpointWithHostId, DateTime.UtcNow);
            VerifyHeartbeatStats(stats, 1, 0);
        }

        [Test]
        public void When_Heartbeating_Endpoint_No_Longer_Sends_Heartbeats()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint" },DateTime.UtcNow);
            var stats = currentHeartbeatStatus.RegisterEndpointThatFailedToHeartbeat(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            VerifyHeartbeatStats(stats, 0, 1);
        }

        [Test]
        public void When_Endpoint_Heartbeats_Is_Restored()
        {
            var currentHeartbeatStatus = new HeartbeatStatusProvider();
            var hostId = Guid.NewGuid();
            currentHeartbeatStatus.RegisterEndpointThatFailedToHeartbeat(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint" });
            var stats = currentHeartbeatStatus.RegisterEndpointWhoseHeartbeatIsRestored(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint" },DateTime.UtcNow);
            VerifyHeartbeatStats(stats, 1, 0);
        }

        [Test]
        public void Should_detect_active_endpoints_that_has_not_sent_heartbeats_within_the_grace_period()
        {
            var now = DateTime.UtcNow;
            var gracePeriod = TimeSpan.FromSeconds(40);

            var currentHeartbeatStatus = new HeartbeatStatusProvider
            {
                GracePeriod = gracePeriod
            };
            var hostId = Guid.NewGuid();

            //unknown
            currentHeartbeatStatus.RegisterNewEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "InactiveEndpoint" });
            
            //endpoint that has already failed
            currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "AlreadyFailedEndpoint" }, now);
            currentHeartbeatStatus.RegisterEndpointThatFailedToHeartbeat(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "AlreadyFailedEndpoint" });
            
            //our failing one
            currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "FailingEndpoint" }, now);
            
            //one that is outside the grace
            currentHeartbeatStatus.RegisterHeartbeatingEndpoint(new EndpointDetails { Host = "Machine", HostId = hostId, Name = "NewEndpoint2" }, now + gracePeriod);

            var endpoints = currentHeartbeatStatus.GetPotentiallyFailedEndpoints(DateTime.UtcNow.Add(gracePeriod));

            var endpoint = endpoints.SingleOrDefault();

            Assert.NotNull(endpoint);

            Assert.AreEqual("FailingEndpoint", endpoint.Details.Name);
            Assert.AreEqual(now, endpoint.LastHeartbeatAt);

        }

        static void VerifyHeartbeatStats(HeartbeatsStats stats, int expectedActive, int expectedDead)
        {
            Assert.AreEqual(expectedDead, stats.Dead);
            Assert.AreEqual(expectedActive, stats.Active);
        }
    }
}
