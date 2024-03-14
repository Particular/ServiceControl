namespace Particular.ThroughputCollector.UnitTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.UnitTests.Infrastructure;

    [TestFixture]
    class ThroughputCollector_Indicator_Tests : ThroughputCollectorTestFixture
    {
        readonly Contracts.Broker broker = Contracts.Broker.AzureServiceBus;
        public override Task Setup()
        {
            SetThroughputSettings = s =>
            {
                s.Broker = broker;
            };

            return base.Setup();
        }

        [Test]
        public async Task Should_indicate_known_endpoint_if_at_least_one_instance_of_it_exists_in_the_sources()
        {
            EndpointsWithMultipleSourcesAndEndpointIndicator.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(1));

            Assert.That(summary[0].IsKnownEndpoint, Is.EqualTo(true), $"Incorrect IsKnownEndpoint recorded for {summary[0].Name}");
        }

        [Test]
        public async Task Should_return_correct_user_indicators_when_multiple_throughput_sources()
        {
            EndpointsWithNoUserIndicatorsFromMultipleSources.ForEach(e => DataStore.RecordEndpointThroughput(e));

            var summary = await ThroughputCollector.GetThroughputSummary();

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.EqualTo(1));

            Assert.That(summary[0].UserIndicator, Is.EqualTo(string.Empty));

            var userIndicator = "SomeIndicator";
            List<EndpointThroughputSummary> endpointsWithUpdates = [new EndpointThroughputSummary { Name = "Endpoint1", UserIndicator = userIndicator }];
            await ThroughputCollector.UpdateUserIndicatorsOnEndpoints(endpointsWithUpdates);

            var updatedEndpoints = await DataStore.GetAllEndpoints();
            Assert.That(updatedEndpoints, Is.Not.Null);
            Assert.That(updatedEndpoints.Count, Is.EqualTo(2));

            Assert.That(updatedEndpoints.All(a => a.UserIndicator == userIndicator), Is.True, $"Incorrect UserIndicator recorded for Endpoint1");
        }

        List<Endpoint> EndpointsWithNoUserIndicatorsFromMultipleSources =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-2), TotalThroughput = 75 }] },
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Monitoring, DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-2), TotalThroughput = 65 }] },
        ];

        List<Endpoint> EndpointsWithMultipleSourcesAndEndpointIndicator =
        [
            new Endpoint { Name = "Endpoint1", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Broker, DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-2), TotalThroughput = 75 }] },
            new Endpoint { Name = "Endpoint1_", SanitizedName = "Endpoint1", ThroughputSource = ThroughputSource.Audit, EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()], DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-2), TotalThroughput = 65 }] },
        ];
    }
}