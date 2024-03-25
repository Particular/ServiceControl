namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;

    [TestFixture]
    class EndpointsTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_add_new_endpoint_when_no_endpoints()
        {
            var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };

            await DataStore.RecordEndpointThroughput(endpoint.Id, endpoint.DailyThroughput);

            var endpoints = await DataStore.GetAllEndpoints();

            var foundEndpoint = endpoints.Single();
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint.Id.Name));
            Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint.Id.ThroughputSource));
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(endpoint.DailyThroughput.Count));
        }

        [Test]
        public async Task Should_add_new_endpoint_when_name_is_the_same_but_source_different()
        {
            var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };
            await DataStore.RecordEndpointThroughput(endpoint1.Id, endpoint1.DailyThroughput);

            var endpoint2 = new Endpoint("Endpoint1", ThroughputSource.Broker)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                        TotalThroughput = 600
                    }
                ]
            };
            await DataStore.RecordEndpointThroughput(endpoint2.Id, endpoint2.DailyThroughput);

            var endpoints = await DataStore.GetAllEndpoints();

            Assert.That(endpoints.Count, Is.EqualTo(2));
        }


        [Test]
        public async Task Should_update_endpoint_that_already_has_throughput_with_new_throughput()
        {
            var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };
            await DataStore.RecordEndpointThroughput(endpoint1.Id, endpoint1.DailyThroughput);

            var endpoint2 = new Endpoint("Endpoint1", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                        TotalThroughput = 100
                    }
                ]
            };
            await DataStore.RecordEndpointThroughput(endpoint2.Id, endpoint2.DailyThroughput);

            var endpoints = await DataStore.GetAllEndpoints();

            var foundEndpoint = endpoints.Single();
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint1.Id.Name));
            Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint1.Id.ThroughputSource));
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Should_not_update_endpoint_with_throughput_with_no_throughput()
        {
            var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                        TotalThroughput = 100
                    }
                ]
            };
            await DataStore.RecordEndpointThroughput(endpoint1.Id, endpoint1.DailyThroughput);

            var endpoint2 = new Endpoint("Endpoint1", ThroughputSource.Audit);
            await DataStore.RecordEndpointThroughput(endpoint2.Id, endpoint2.DailyThroughput);

            var endpoints = await DataStore.GetAllEndpoints();

            var foundEndpoint = endpoints.Single();
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint1.Id.Name));
            Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint1.Id.ThroughputSource));
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Should_retrieve_matching_endpoint_when_same_source()
        {
            var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };

            await DataStore.RecordEndpointThroughput(endpoint.Id, endpoint.DailyThroughput);

            var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

            Assert.That(foundEndpoint, Is.Not.Null);
        }

        [Test]
        public async Task Should_not_retrieve_matching_endpoint_when_different_source()
        {
            var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };

            await DataStore.RecordEndpointThroughput(endpoint.Id, endpoint.DailyThroughput);

            var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Broker);

            Assert.That(foundEndpoint, Is.Null);
        }

        [Test]
        public async Task Should_update_user_indicators_and_nothing_else()
        {
            var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                SanitizedName = "Endpoint",
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };

            await DataStore.SaveEndpoint(endpoint);
            await DataStore.RecordEndpointThroughput(endpoint.Id, endpoint.DailyThroughput);

            var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);
            Assert.That(foundEndpoint, Is.Not.Null);
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpoint.UserIndicator, Is.Null);

            var userIndicator = "someIndicator";
            var endpointWithUserIndicators = new Endpoint("Endpoint")
            {
                SanitizedName = "Endpoint",
                UserIndicator = userIndicator
            };

            await DataStore.UpdateUserIndicatorOnEndpoints([endpointWithUserIndicators]);

            foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

            Assert.That(foundEndpoint, Is.Not.Null);
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpoint.UserIndicator, Is.EqualTo(userIndicator));
        }

        [Test]
        public async Task Should_not_add_endpoint_when_updating_user_indication()
        {
            var endpointWithUserIndicators = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                SanitizedName = "Endpoint",
                UserIndicator = "someIndicator",
            };

            await DataStore.UpdateUserIndicatorOnEndpoints([endpointWithUserIndicators]);

            var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);
            var allEndpoints = await DataStore.GetAllEndpoints();

            Assert.That(foundEndpoint, Is.Null);
            Assert.That(allEndpoints.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task Should_update_indicators_on_all_endpoint_sources()
        {
            var endpointAudit = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                SanitizedName = "Endpoint",
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 50
                    }
                ]
            };

            await DataStore.SaveEndpoint(endpointAudit);
            await DataStore.RecordEndpointThroughput(endpointAudit.Id, endpointAudit.DailyThroughput);

            var endpointMonitoring = new Endpoint("Endpoint", ThroughputSource.Monitoring)
            {
                SanitizedName = "Endpoint",
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow),
                        TotalThroughput = 70
                    }
                ]
            };

            await DataStore.SaveEndpoint(endpointMonitoring);
            await DataStore.RecordEndpointThroughput(endpointMonitoring.Id, endpointMonitoring.DailyThroughput);

            var foundEndpointAudit = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);
            Assert.That(foundEndpointAudit, Is.Not.Null);
            Assert.That(foundEndpointAudit.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpointAudit.UserIndicator, Is.Null);

            var foundEndpointMonitoring = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Monitoring);
            Assert.That(foundEndpointMonitoring, Is.Not.Null);
            Assert.That(foundEndpointMonitoring.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpointMonitoring.UserIndicator, Is.Null);

            var userIndicator = "someIndicator";

            var endpointWithUserIndicators = new Endpoint("Endpoint")
            {
                SanitizedName = "Endpoint",
                UserIndicator = userIndicator
            };

            await DataStore.UpdateUserIndicatorOnEndpoints([endpointWithUserIndicators]);

            foundEndpointAudit = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);
            Assert.That(foundEndpointAudit, Is.Not.Null);
            Assert.That(foundEndpointAudit.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpointAudit.UserIndicator, Is.EqualTo(userIndicator));

            foundEndpointMonitoring = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Monitoring);
            Assert.That(foundEndpointMonitoring, Is.Not.Null);
            Assert.That(foundEndpointMonitoring.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpointMonitoring.UserIndicator, Is.EqualTo(userIndicator));
        }

        [Test]
        public async Task Should_correctly_report_throughput_existence_for_X_days()
        {
            var endpointAudit = new Endpoint("Endpoint", ThroughputSource.Audit)
            {
                DailyThroughput =
                [
                    new EndpointDailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10),
                        TotalThroughput = 50
                    }
                ]
            };
            await DataStore.RecordEndpointThroughput(endpointAudit.Id, endpointAudit.DailyThroughput);

            Assert.That(await DataStore.IsThereThroughputForLastXDays(5), Is.False);
            Assert.That(await DataStore.IsThereThroughputForLastXDays(20), Is.True);
        }
    }
}