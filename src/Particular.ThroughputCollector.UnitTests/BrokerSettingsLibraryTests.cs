namespace Particular.ThroughputCollector.UnitTests
{
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Shared;

    [TestFixture]
    public class BrokerSettingsLibraryTests
    {
        const Broker broker = Broker.AzureServiceBus;

        [Test]
        public void Should_find_broker_by_name()
        {
            var _broker = BrokerSettingsLibrary.Find(broker);

            Assert.That(_broker, Is.Not.Null);
            Assert.That(_broker.Broker, Is.EqualTo(_broker.Broker));
        }
    }
}