namespace Particular.ThroughputCollector.UnitTests
{
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;

    [TestFixture]
    public class BrokerManifestLibraryTests
    {
        const Broker broker = Broker.AzureServiceBus;

        [Test]
        public void Should_find_broker_by_name()
        {
            var _broker = BrokerManifestLibrary.Find(broker);

            Assert.That(_broker, Is.Not.Null);
            Assert.That(_broker.Broker, Is.EqualTo(_broker.Broker));
        }

        [Test]
        public void Should_return_null_for_not_found_broker()
        {
            var _noneBroker = Broker.None;
            var _broker = BrokerManifestLibrary.Find(_noneBroker);

            Assert.That(_broker, Is.Null);
        }
    }
}