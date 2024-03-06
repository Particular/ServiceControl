namespace Particular.ThroughputCollector.UnitTests
{
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
            var brokerSettings = await ThroughputCollector.GetBrokerSettingsInformation();
            Assert.That(brokerSettings, Is.Not.Null);
            Assert.That(brokerSettings.Broker, Is.EqualTo(broker));
            Assert.That(brokerSettings.Settings, Is.Not.Null);
            Assert.That(brokerSettings.Settings.Count, Is.AtLeast(1));
        }

        [Test]
        public async Task Should_return_correct_number_of_endpoints_in_summary()
        {
            var summary = await ThroughputCollector.GetThroughputSummary(30);

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.AtLeast(1));

            //remove error and audit queues, as well as endpoints without throughput recorded for month
            var endpointsWithThroughput = EndpointsWithThroughput.Where(w => w.Name != configuration.ThroughputSettings.ErrorQueue && w.Name != configuration.ThroughputSettings.AuditQueue && w.Name != configuration.ThroughputSettings.ServiceControlQueue);
            Assert.That(summary.Count, Is.EqualTo(endpointsWithThroughput.GroupBy(g => g.Name).Count()), "Incorrect number of endpoints with throughput in summary");
        }

        [Test]
        public async Task Should_return_correct_max_throughput_in_summary()
        {
            var summary = await ThroughputCollector.GetThroughputSummary(30);

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.AtLeast(1));

            var endpointsWithMultipleThroughputs = EndpointsWithThroughput.GroupBy(g => g.Name).Where(w => w.Count() > 1).FirstOrDefault();
            var maxThroughPutExpected = endpointsWithMultipleThroughputs.SelectMany(s => s.DailyThroughput).MaxBy(m => m.TotalThroughput).TotalThroughput;
            Assert.That(summary.FirstOrDefault(w => w.Name == endpointsWithMultipleThroughputs.Key).MaxDailyThroughput, Is.EqualTo(maxThroughPutExpected), $"Incorrect MaxDailyThroughput recorded for {endpointsWithMultipleThroughputs.Key}");
        }

        [Test]
        public async Task Should_return_correct_no_throughput_indicator()
        {
            var summary = await ThroughputCollector.GetThroughputSummary(30);

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.AtLeast(1));

            var endpointWithNoThroughputs = summary.Where(w => w.Name == ThroughputCollectorTestData.EndpointNameWithNoThroughput).FirstOrDefault();
            Assert.That(endpointWithNoThroughputs, Is.Not.Null);

            Assert.That(endpointWithNoThroughputs.MaxDailyThroughput, Is.EqualTo(0), $"Incorrect MaxDailyThroughput recorded for {ThroughputCollectorTestData.EndpointNameWithNoThroughput}");
            Assert.That(endpointWithNoThroughputs.ThroughputExistsForThisPeriod, Is.EqualTo(false), $"Incorrect ThroughputExistsForThisPeriod recorded for {ThroughputCollectorTestData.EndpointNameWithNoThroughput}");
        }

        [Test]
        public async Task Should_return_correct_user_indicators_when_multiple_throughput_sources()
        {
            var summary = await ThroughputCollector.GetThroughputSummary(30);

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Count, Is.AtLeast(1));

            var endpointWithMultiIndicators = summary.Where(w => w.Name == ThroughputCollectorTestData.EndpointNameWithMultiIndicators).FirstOrDefault();
            Assert.That(endpointWithMultiIndicators, Is.Not.Null);

            Assert.That(endpointWithMultiIndicators.IsKnownEndpoint, Is.EqualTo(true), $"Incorrect IsKnownEndpoint recorded for {ThroughputCollectorTestData.EndpointNameWithNoThroughput}");
            Assert.That(endpointWithMultiIndicators.UserIndicatedSendOnly, Is.EqualTo(true), $"Incorrect UserIndicatedSendOnly recorded for {ThroughputCollectorTestData.EndpointNameWithNoThroughput}");
            Assert.That(endpointWithMultiIndicators.UserIndicatedToIgnore, Is.EqualTo(true), $"Incorrect UserIndicatedToIgnore recorded for {ThroughputCollectorTestData.EndpointNameWithNoThroughput}");
        }
    }
}