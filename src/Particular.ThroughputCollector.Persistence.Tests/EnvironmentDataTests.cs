namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class EnvironmentDataTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_add_new_environment_data_when_none_exists()
        {
            var existingBrokerData = await DataStore.GetEnvironmentData();

            Assert.That(existingBrokerData, Is.Null);

            await DataStore.SaveEnvironmentData(null, []);

            existingBrokerData = await DataStore.GetEnvironmentData();

            Assert.That(existingBrokerData, Is.Not.Null);
        }

        [Test]
        public async Task Should_update_existing_environment_data_if_already_exists()
        {
            var existingEnvironmentData = await DataStore.GetEnvironmentData();

            Assert.That(existingEnvironmentData, Is.Null);

            await DataStore.SaveEnvironmentData(null, new Dictionary<string, string> { { "Version", "1.2" } });
            existingEnvironmentData = await DataStore.GetEnvironmentData();

            Assert.That(existingEnvironmentData, Is.Not.Null);
            Assert.That(existingEnvironmentData.Data["Version"], Is.EqualTo("1.2"));
            Assert.That(existingEnvironmentData.ScopeType, Is.Null);

            await DataStore.SaveEnvironmentData("scope", new Dictionary<string, string> { { "Version", "2.2" } });
            existingEnvironmentData = await DataStore.GetEnvironmentData();

            Assert.That(existingEnvironmentData, Is.Not.Null);
            Assert.That(existingEnvironmentData.Data["Version"], Is.EqualTo("2.2"));
            Assert.That(existingEnvironmentData.ScopeType, Is.EqualTo("scope"));
        }
    }
}