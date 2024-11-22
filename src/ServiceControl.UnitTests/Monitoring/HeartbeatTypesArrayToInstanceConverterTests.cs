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
            var heartbeat = JsonSerializer.Deserialize<EndpointHeartbeat>("""
                [
                    {
                        "$type": "ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat, ServiceControl",
                        "ExecutedAt": "2024-06-02T12:03:41.780",
                        "EndpointName": "Test",
                        "HostId": "1865830e-71b0-dc6c-e146-62cdd0034e6e",
                        "Host": "Machine"
                    }
                ]
                """, options);

            Assert.That(heartbeat, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(heartbeat.EndpointName, Is.EqualTo("Test"));
                Assert.That(heartbeat.Host, Is.EqualTo("Machine"));
                Assert.That(heartbeat.ExecutedAt, Is.EqualTo(new DateTime(2024, 6, 2, 12, 3, 41, 780, DateTimeKind.Utc)));
                Assert.That(heartbeat.HostId, Is.EqualTo(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e")));
            });
        }

        [Test]
        public void Should_deserialize_single_heartbeat()
        {
            var heartbeat = JsonSerializer.Deserialize<EndpointHeartbeat>("""
                {
                    "$type": "ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat, ServiceControl",
                    "ExecutedAt": "2024-06-02T12:03:41.780",
                    "EndpointName": "Test",
                    "HostId": "1865830e-71b0-dc6c-e146-62cdd0034e6e",
                    "Host": "Machine"
                }
                """, options);

            Assert.That(heartbeat, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(heartbeat.EndpointName, Is.EqualTo("Test"));
                Assert.That(heartbeat.Host, Is.EqualTo("Machine"));
                Assert.That(heartbeat.ExecutedAt, Is.EqualTo(new DateTime(2024, 6, 2, 12, 3, 41, 780, DateTimeKind.Utc)));
                Assert.That(heartbeat.HostId, Is.EqualTo(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e")));
            });
        }

        [Test]
        public void Should_deserialize_register_endpoint_startup_arrays()
        {
            // sample json for RegisterEndpointStartup
            var endpointStartup = JsonSerializer.Deserialize<RegisterEndpointStartup>("""
                [
                    {
                        "HostId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                        "Endpoint": "SampleEndpoint",
                        "StartedAt": "2022-12-01T12:00:00Z",
                        "HostProperties": {
                            "Property1": "Value1",
                            "Property2": "Value2"
                        },
                        "HostDisplayName": "SampleHostDisplayName",
                        "Host": "SampleHost"
                    }
                ]
                """, options);

            Assert.That(endpointStartup, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(endpointStartup.HostId, Is.EqualTo(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6")));
                Assert.That(endpointStartup.Endpoint, Is.EqualTo("SampleEndpoint"));
                Assert.That(endpointStartup.StartedAt, Is.EqualTo(new DateTime(2022, 12, 1, 12, 0, 0, 0, DateTimeKind.Utc)));
                Assert.That(endpointStartup.HostDisplayName, Is.EqualTo("SampleHostDisplayName"));
                Assert.That(endpointStartup.Host, Is.EqualTo("SampleHost"));
            });
            Assert.That(endpointStartup.HostProperties, Is.EqualTo(new Dictionary<string, string>
            {
                { "Property1", "Value1" },
                { "Property2", "Value2" }
            }).AsCollection);
        }

        [Test]
        public void Should_deserialize_single_register_endpoint_startup()
        {
            // sample json for RegisterEndpointStartup
            var endpointStartup = JsonSerializer.Deserialize<RegisterEndpointStartup>("""
                {
                    "HostId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                    "Endpoint": "SampleEndpoint",
                    "StartedAt": "2022-12-01T12:00:00Z",
                    "HostProperties": {
                        "Property1": "Value1",
                        "Property2": "Value2"
                    },
                    "HostDisplayName": "SampleHostDisplayName",
                    "Host": "SampleHost"
                }
                """, options);

            Assert.That(endpointStartup, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(endpointStartup.HostId, Is.EqualTo(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6")));
                Assert.That(endpointStartup.Endpoint, Is.EqualTo("SampleEndpoint"));
                Assert.That(endpointStartup.StartedAt, Is.EqualTo(new DateTime(2022, 12, 1, 12, 0, 0, 0, DateTimeKind.Utc)));
                Assert.That(endpointStartup.HostDisplayName, Is.EqualTo("SampleHostDisplayName"));
                Assert.That(endpointStartup.Host, Is.EqualTo("SampleHost"));
            });
            Assert.That(endpointStartup.HostProperties, Is.EqualTo(new Dictionary<string, string>
            {
                { "Property1", "Value1" },
                { "Property2", "Value2" }
            }).AsCollection);
        }

        [Test]
        public void Should_deserialize_register_potentially_missing_heartbeat_arrays()
        {
            var potentiallyMissingHeartbeats = JsonSerializer.Deserialize<RegisterPotentiallyMissingHeartbeats>("""
                [
                    {
                        "DetectedAt": "2024-06-02T12:03:41.780",
                        "LastHeartbeatAt": "2024-06-02T12:03:38.780",
                        "EndpointInstanceId": "1865830e-71b0-dc6c-e146-62cdd0034e6e"
                    }
                ]
                """, options);

            Assert.That(potentiallyMissingHeartbeats, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(potentiallyMissingHeartbeats.DetectedAt, Is.EqualTo(new DateTime(2024, 6, 2, 12, 3, 41, 780, DateTimeKind.Utc)));
                Assert.That(potentiallyMissingHeartbeats.LastHeartbeatAt, Is.EqualTo(new DateTime(2024, 6, 2, 12, 3, 38, 780, DateTimeKind.Utc)));
                Assert.That(potentiallyMissingHeartbeats.EndpointInstanceId, Is.EqualTo(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e")));
            });
        }

        [Test]
        public void Should_deserialize_single_register_potentially_missing_heartbeat()
        {
            var potentiallyMissingHeartbeats = JsonSerializer.Deserialize<RegisterPotentiallyMissingHeartbeats>("""
                {
                    "DetectedAt": "2024-06-02T12:03:41.780",
                    "LastHeartbeatAt": "2024-06-02T12:03:38.780",
                    "EndpointInstanceId": "1865830e-71b0-dc6c-e146-62cdd0034e6e"
                }
                """, options);

            Assert.That(potentiallyMissingHeartbeats, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(potentiallyMissingHeartbeats.DetectedAt, Is.EqualTo(new DateTime(2024, 6, 2, 12, 3, 41, 780, DateTimeKind.Utc)));
                Assert.That(potentiallyMissingHeartbeats.LastHeartbeatAt, Is.EqualTo(new DateTime(2024, 6, 2, 12, 3, 38, 780, DateTimeKind.Utc)));
                Assert.That(potentiallyMissingHeartbeats.EndpointInstanceId, Is.EqualTo(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e")));
            });
        }
    }
}