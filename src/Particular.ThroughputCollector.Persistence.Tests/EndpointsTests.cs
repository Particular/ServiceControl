namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class EndpointsTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_add_new_endpoint_when_no_endpoints()
        {
            var endpoint = new Endpoint
            {
                Name = "Endpoint",
                Queue = "Queue",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date, TotalThroughput = 50 }]
            };

            await DataStore.RecordEndpointThroughput(endpoint);

            var endpoints = await DataStore.GetAllEndpoints();

            Assert.That(endpoints.Count, Is.EqualTo(1));
            var foundEndpoint = endpoints[0];
            Assert.That(foundEndpoint.Name, Is.EqualTo(endpoint.Name));
            Assert.That(foundEndpoint.Queue, Is.EqualTo(endpoint.Queue));
            Assert.That(foundEndpoint.ThroughputSource, Is.EqualTo(endpoint.ThroughputSource));
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(endpoint.DailyThroughput.Count));
        }

        [Test]
        public async Task Should_add_new_endpoint_when_name_is_the_same_but_source_different()
        {
            var endpoint1 = new Endpoint
            {
                Name = "Endpoint1",
                Queue = "Queue1",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date, TotalThroughput = 50 }]
            };
            await DataStore.RecordEndpointThroughput(endpoint1);

            var endpoint2 = new Endpoint
            {
                Name = "Endpoint1",
                Queue = "Queue1",
                ThroughputSource = ThroughputSource.Broker,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 600 }]
            };
            await DataStore.RecordEndpointThroughput(endpoint2);

            var endpoints = await DataStore.GetAllEndpoints();

            Assert.That(endpoints.Count, Is.EqualTo(2));
        }


        [Test]
        public async Task Should_update_endpoint_that_already_has_throughput_with_new_throughput()
        {
            var endpoint1 = new Endpoint
            {
                Name = "Endpoint1",
                Queue = "Queue1",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date, TotalThroughput = 50 }]
            };
            await DataStore.RecordEndpointThroughput(endpoint1);

            var endpoint2 = new Endpoint
            {
                Name = "Endpoint1",
                Queue = "Queue1",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 100 }]
            };
            await DataStore.RecordEndpointThroughput(endpoint2);

            var endpoints = await DataStore.GetAllEndpoints();

            Assert.That(endpoints.Count, Is.EqualTo(1));
            var foundEndpoint = endpoints[0];
            Assert.That(foundEndpoint.Name, Is.EqualTo(endpoint1.Name));
            Assert.That(foundEndpoint.Queue, Is.EqualTo(endpoint1.Queue));
            Assert.That(foundEndpoint.ThroughputSource, Is.EqualTo(endpoint1.ThroughputSource));
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Should_not_update_endpoint_with_throughput_with_no_throughput()
        {
            var endpoint1 = new Endpoint
            {
                Name = "Endpoint1",
                Queue = "Queue1",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 100 }]
            };
            await DataStore.RecordEndpointThroughput(endpoint1);

            var endpoint2 = new Endpoint
            {
                Name = "Endpoint1",
                Queue = "Queue1",
                ThroughputSource = ThroughputSource.Audit,
            };
            await DataStore.RecordEndpointThroughput(endpoint2);

            var endpoints = await DataStore.GetAllEndpoints();

            Assert.That(endpoints.Count, Is.EqualTo(1));
            var foundEndpoint = endpoints[0];
            Assert.That(foundEndpoint.Name, Is.EqualTo(endpoint1.Name));
            Assert.That(foundEndpoint.Queue, Is.EqualTo(endpoint1.Queue));
            Assert.That(foundEndpoint.ThroughputSource, Is.EqualTo(endpoint1.ThroughputSource));
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Should_retrieve_matching_endpoint_when_same_source()
        {
            var endpoint = new Endpoint
            {
                Name = "Endpoint",
                Queue = "Queue",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date, TotalThroughput = 50 }]
            };

            await DataStore.RecordEndpointThroughput(endpoint);

            var foundEndpoint = await DataStore.GetEndpointByNameOrQueue("Endpoint", ThroughputSource.Audit);

            Assert.That(foundEndpoint, Is.Not.Null);
        }

        [Test]
        public async Task Should_not_retrieve_matching_endpoint_when_different_source()
        {
            var endpoint = new Endpoint
            {
                Name = "Endpoint",
                Queue = "Queue",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date, TotalThroughput = 50 }]
            };

            await DataStore.RecordEndpointThroughput(endpoint);

            var foundEndpoint = await DataStore.GetEndpointByNameOrQueue("Endpoint", ThroughputSource.Broker);

            Assert.That(foundEndpoint, Is.Null);
        }

        [Test]
        public async Task Should_update_user_indicators_and_nothing_else()
        {
            var endpoint = new Endpoint
            {
                Name = "Endpoint",
                Queue = "Queue",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput() { DateUTC = DateTime.UtcNow.Date, TotalThroughput = 50 }]
            };

            await DataStore.RecordEndpointThroughput(endpoint);

            var foundEndpoint = await DataStore.GetEndpointByNameOrQueue("Endpoint", ThroughputSource.Audit);
            Assert.That(foundEndpoint, Is.Not.Null);
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpoint.UserIndicatedSendOnly, Is.Null);
            Assert.That(foundEndpoint.UserIndicatedToIgnore, Is.Null);
            ;

            var endpointWithUserIndicators = new Endpoint
            {
                Name = "Endpoint",
                Queue = "Queue",
                ThroughputSource = ThroughputSource.Audit,
                UserIndicatedSendOnly = true,
                UserIndicatedToIgnore = true,
            };

            await DataStore.UpdateUserIndicationOnEndpoints([endpointWithUserIndicators]);

            foundEndpoint = await DataStore.GetEndpointByNameOrQueue("Endpoint", ThroughputSource.Audit);

            Assert.That(foundEndpoint, Is.Not.Null);
            Assert.That(foundEndpoint.DailyThroughput.Count, Is.EqualTo(1));
            Assert.That(foundEndpoint.UserIndicatedSendOnly, Is.EqualTo(true));
            Assert.That(foundEndpoint.UserIndicatedToIgnore, Is.EqualTo(true));
        }

        [Test]
        public async Task Should_not_add_endpoint_when_updating_user_indication()
        {
            var endpointWithUserIndicators = new Endpoint
            {
                Name = "Endpoint",
                Queue = "Queue",
                ThroughputSource = ThroughputSource.Audit,
                UserIndicatedSendOnly = true,
                UserIndicatedToIgnore = true,
            };

            await DataStore.UpdateUserIndicationOnEndpoints([endpointWithUserIndicators]);

            var foundEndpoint = await DataStore.GetEndpointByNameOrQueue("Endpoint", ThroughputSource.Audit);
            var allEndpoints = await DataStore.GetAllEndpoints();

            Assert.That(foundEndpoint, Is.Null);
            Assert.That(allEndpoints.Count, Is.EqualTo(0));
        }
    }
}