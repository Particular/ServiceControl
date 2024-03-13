namespace Particular.ThroughputCollector.UnitTests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.UnitTests.Infrastructure;

    [TestFixture]
    class ThroughputCollector_BrokerSettings_Tests : ThroughputCollectorTestFixture
    {
        readonly Broker broker = Broker.AzureServiceBus;
        public override Task Setup()
        {
            SetThroughputSettings = s =>
            {
                s.Broker = broker;
            };

            return base.Setup();
        }

        [Test]
        public async Task Should_find_broker_settings_for_current_broker()
        {
            var brokerSettings = await ThroughputCollector.GetBrokerSettingsInformation();

            Assert.That(brokerSettings, Is.Not.Null);
            Assert.That(brokerSettings.Broker, Is.EqualTo(broker));
            Assert.That(brokerSettings.Settings, Is.Not.Null);
            Assert.That(brokerSettings.Settings.Count, Is.AtLeast(1));
        }
    }
}