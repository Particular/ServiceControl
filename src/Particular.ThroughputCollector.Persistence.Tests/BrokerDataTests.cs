namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts;
    using NUnit.Framework;

    [TestFixture]
    class BrokerDataTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_add_new_broker_data_when_none_exists_for_broker()
        {
            var broker = Broker.RabbitMQ;

            var existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Null);

            await DataStore.SaveBrokerData(broker, null, []);

            existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Not.Null);
        }

        [Test]
        public async Task Should_update_existing_broker_data_for_the_same_broker()
        {
            var broker = Broker.AmazonSQS;

            var existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Null);

            await DataStore.SaveBrokerData(broker, null, new Dictionary<string, string> { { "Version", "1.2" } });
            existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Not.Null);
            Assert.That(existingBrokerData.Data["Version"], Is.EqualTo("1.2"));
            Assert.That(existingBrokerData.ScopeType, Is.Null);

            await DataStore.SaveBrokerData(broker, "scope", new Dictionary<string, string> { { "Version", "2.2" } });
            existingBrokerData = await DataStore.GetBrokerData(broker);

            Assert.That(existingBrokerData, Is.Not.Null);
            Assert.That(existingBrokerData.Data["Version"], Is.EqualTo("2.2"));
            Assert.That(existingBrokerData.ScopeType, Is.EqualTo("scope"));
        }
    }
}