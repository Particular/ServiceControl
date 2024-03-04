namespace Particular.ThroughputCollector.UnitTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.UnitTests.Infrastructure;

    [TestFixture]
    class ThroughputCollectorTests : ThroughputCollectorTestFixture
    {
        readonly Broker broker = Broker.AzureServiceBus;
        public override Task Setup()
        {
            SetThroughputSettings = s =>
            {
                s.Broker = broker;
            };

            EndpointsWithThroughput = ThroughputCollectorTestData.GetEndpointsThroughput(broker);
            return base.Setup();
        }

        [Test]
        public async Task Should_find_broker_settings_for_asb()
        {
            var brokerSettings = await ThroughputCollector.GetBrokerSettings();
            Assert.That(brokerSettings, Is.Not.Null);
            Assert.That(brokerSettings.Broker, Is.EqualTo(broker));
            Assert.That(brokerSettings.Settings, Is.Not.Null);
            Assert.That(brokerSettings.Settings.Count, Is.AtLeast(1));
        }

        [Test]
        public async Task Should()
        {
            var month = DateTime.UtcNow.AddMonths(-1).Month;
            var summary = await ThroughputCollector.GetThroughputSummary(month);

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.AtLeast(1));

            //remove error and audit queues, as well as endpoints without throughput recorded for month
            var endpointsWithThroughput = EndpointsWithThroughput.Where(w => w.DailyThroughput.Any(a => a.DateUTC.Month == month) && w.Name != "error" && w.Name != "audit");
            var distinctEndpoints = endpointsWithThroughput.GroupBy(g => g.Name);

            Assert.That(summary.Count, Is.EqualTo(distinctEndpoints.Count()), "Incorrect number of endpoints with throughput");

            //TODO other tests to verify
        }
    }
}