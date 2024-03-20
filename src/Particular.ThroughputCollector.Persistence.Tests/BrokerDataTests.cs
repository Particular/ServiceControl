namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;

    [TestFixture]
    class BrokerDataTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_add_new_broker_data_when_none_exists_for_broker()
        {
            var broker = Broker.RabbitMQ;

            var existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Null);

            await DataStore.SaveBrokerData(broker, null, "1.2");

            existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Not.Null);
        }

        [Test]
        public async Task Should_update_existing_broker_data_for_the_same_broker()
        {
            var broker = Broker.AmazonSQS;

            var existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Null);

            await DataStore.SaveBrokerData(broker, null, "1.2");
            existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Not.Null);
            Assert.That(existingBrokerData.Version, Is.EqualTo("1.2"));
            Assert.That(existingBrokerData.ScopeType, Is.Null);

            await DataStore.SaveBrokerData(broker, "scope", null);
            existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Not.Null);
            Assert.That(existingBrokerData.Version, Is.EqualTo("1.2"));
            Assert.That(existingBrokerData.ScopeType, Is.EqualTo("scope"));
        }
    }
}