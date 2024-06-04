namespace ServiceControl.UnitTests.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using HeartbeatMonitoring.InternalMessages;
    using NUnit.Framework;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Monitoring.HeartbeatMonitoring;

    [TestFixture]
    public class HeartbeatTypesArrayToInstanceConverterTests
    {
        JsonSerializerOptions options;

        [SetUp]
        public void Setup() =>
            options = new JsonSerializerOptions
            {
                Converters =
                {
                    new HeartbeatTypesArrayToInstanceConverter()
                },
                TypeInfoResolverChain =
                {
                    HeartbeatSerializationContext.Default
                }
            };

        [Test]
        public void Should_deserialize_heartbeat_arrays()
        {
            var heartbeat = JsonSerializer.Deserialize<EndpointHeartbeat>(@"[
  {
    ""$type"": ""ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat, ServiceControl"",
    ""ExecutedAt"": ""2024-06-02T12:03:41.780"",
    ""EndpointName"": ""Test"",
    ""HostId"": ""1865830e-71b0-dc6c-e146-62cdd0034e6e"",
    ""Host"": ""Machine""
  }
]", options);

            Assert.IsNotNull(heartbeat);
            Assert.AreEqual("Test", heartbeat.EndpointName);
            Assert.AreEqual("Machine", heartbeat.Host);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 41, 780, System.DateTimeKind.Utc), heartbeat.ExecutedAt);
            Assert.AreEqual(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e"), heartbeat.HostId);
        }

        [Test]
        public void Should_deserialize_single_heartbeat()
        {
            var heartbeat = JsonSerializer.Deserialize<EndpointHeartbeat>(@"{
    ""$type"": ""ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat, ServiceControl"",
    ""ExecutedAt"": ""2024-06-02T12:03:41.780"",
    ""EndpointName"": ""Test"",
    ""HostId"": ""1865830e-71b0-dc6c-e146-62cdd0034e6e"",
    ""Host"": ""Machine""
  }", options);

            Assert.IsNotNull(heartbeat);
            Assert.AreEqual("Test", heartbeat.EndpointName);
            Assert.AreEqual("Machine", heartbeat.Host);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 41, 780, System.DateTimeKind.Utc), heartbeat.ExecutedAt);
            Assert.AreEqual(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e"), heartbeat.HostId);
        }

        [Test]
        public void Should_deserialize_register_endpoint_startup_arrays()
        {
            // sample json for RegisterEndpointStartup
            var endpointStartup = JsonSerializer.Deserialize<RegisterEndpointStartup>(@"[
    {
        ""HostId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
        ""Endpoint"": ""SampleEndpoint"",
        ""StartedAt"": ""2022-12-01T12:00:00Z"",
        ""HostProperties"": {
            ""Property1"": ""Value1"",
            ""Property2"": ""Value2""
        },
        ""HostDisplayName"": ""SampleHostDisplayName"",
        ""Host"": ""SampleHost""
    }
]", options);

            Assert.IsNotNull(endpointStartup);
            Assert.AreEqual(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), endpointStartup.HostId);
            Assert.AreEqual("SampleEndpoint", endpointStartup.Endpoint);
            Assert.AreEqual(new DateTime(2022, 12, 1, 12, 0, 0, 0, System.DateTimeKind.Utc), endpointStartup.StartedAt);
            Assert.AreEqual("SampleHostDisplayName", endpointStartup.HostDisplayName);
            Assert.AreEqual("SampleHost", endpointStartup.Host);
            CollectionAssert.AreEqual(new Dictionary<string, string>
            {
                { "Property1", "Value1" },
                { "Property2", "Value2" }
            }, endpointStartup.HostProperties);
        }

        [Test]
        public void Should_deserialize_single_register_endpoint_startup()
        {
            // sample json for RegisterEndpointStartup
            var endpointStartup = JsonSerializer.Deserialize<RegisterEndpointStartup>(@"{
        ""HostId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
        ""Endpoint"": ""SampleEndpoint"",
        ""StartedAt"": ""2022-12-01T12:00:00Z"",
        ""HostProperties"": {
            ""Property1"": ""Value1"",
            ""Property2"": ""Value2""
        },
        ""HostDisplayName"": ""SampleHostDisplayName"",
        ""Host"": ""SampleHost""
    }", options);

            Assert.IsNotNull(endpointStartup);
            Assert.AreEqual(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), endpointStartup.HostId);
            Assert.AreEqual("SampleEndpoint", endpointStartup.Endpoint);
            Assert.AreEqual(new DateTime(2022, 12, 1, 12, 0, 0, 0, System.DateTimeKind.Utc), endpointStartup.StartedAt);
            Assert.AreEqual("SampleHostDisplayName", endpointStartup.HostDisplayName);
            Assert.AreEqual("SampleHost", endpointStartup.Host);
            CollectionAssert.AreEqual(new Dictionary<string, string>
            {
                { "Property1", "Value1" },
                { "Property2", "Value2" }
            }, endpointStartup.HostProperties);
        }

        [Test]
        public void Should_deserialize_register_potentially_missing_heartbeat_arrays()
        {
            var potentiallyMissingHeartbeats = JsonSerializer.Deserialize<RegisterPotentiallyMissingHeartbeats>(@"[
  {
    ""DetectedAt"": ""2024-06-02T12:03:41.780"",
    ""LastHeartbeatAt"": ""2024-06-02T12:03:38.780"",
    ""EndpointInstanceId"": ""1865830e-71b0-dc6c-e146-62cdd0034e6e""
  }
]", options);

            Assert.IsNotNull(potentiallyMissingHeartbeats);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 41, 780, System.DateTimeKind.Utc), potentiallyMissingHeartbeats.DetectedAt);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 38, 780, System.DateTimeKind.Utc), potentiallyMissingHeartbeats.LastHeartbeatAt);
            Assert.AreEqual(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e"), potentiallyMissingHeartbeats.EndpointInstanceId);
        }

        [Test]
        public void Should_deserialize_single_register_potentially_missing_heartbeat()
        {
            var potentiallyMissingHeartbeats = JsonSerializer.Deserialize<RegisterPotentiallyMissingHeartbeats>(@"{
    ""DetectedAt"": ""2024-06-02T12:03:41.780"",
    ""LastHeartbeatAt"": ""2024-06-02T12:03:38.780"",
    ""EndpointInstanceId"": ""1865830e-71b0-dc6c-e146-62cdd0034e6e""
  }", options);

            Assert.IsNotNull(potentiallyMissingHeartbeats);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 41, 780, System.DateTimeKind.Utc), potentiallyMissingHeartbeats.DetectedAt);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 38, 780, System.DateTimeKind.Utc), potentiallyMissingHeartbeats.LastHeartbeatAt);
            Assert.AreEqual(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e"), potentiallyMissingHeartbeats.EndpointInstanceId);
        }
    }
}