namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;

    [TestFixture]
    class EnvironmentDataTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_add_new_environment_data_when_none_exists()
        {
            //Arrange
            var existingBrokerData = await DataStore.GetEnvironmentData(default);
            Assert.That(existingBrokerData, Is.Null);

            //Act
            await DataStore.SaveEnvironmentData(null, [], default);
            existingBrokerData = await DataStore.GetEnvironmentData(default);

            //Assert
            Assert.That(existingBrokerData, Is.Not.Null);
        }

        [Test]
        public async Task Should_update_existing_environment_data_if_already_exists()
        {
            //Arrange
            var existingEnvironmentData = await DataStore.GetEnvironmentData(default);
            Assert.That(existingEnvironmentData, Is.Null);

            await DataStore.SaveEnvironmentData(null, new Dictionary<string, string> { { "Version", "1.2" } }, default);
            existingEnvironmentData = await DataStore.GetEnvironmentData(default);

            Assert.That(existingEnvironmentData, Is.Not.Null);
            Assert.That(existingEnvironmentData.Data["Version"], Is.EqualTo("1.2"));
            Assert.That(existingEnvironmentData.ScopeType, Is.Null);

            //Act
            await DataStore.SaveEnvironmentData("scope", new Dictionary<string, string> { { "Version", "2.2" } }, default);
            existingEnvironmentData = await DataStore.GetEnvironmentData(default);

            //Assert
            Assert.That(existingEnvironmentData, Is.Not.Null);
            Assert.That(existingEnvironmentData.Data["Version"], Is.EqualTo("2.2"));
            Assert.That(existingEnvironmentData.ScopeType, Is.EqualTo("scope"));
        }

        [Test]
        public async Task Should_save_audit_instance_Data()
        {
            //Arrange
            var auditInstance1 = new AuditInstance { Url = "http://localhost:44", MessageTransport = "AzureServiceBus", Version = "4.3.6" };
            var auditInstance2 = new AuditInstance { Url = "http://localhost:43", MessageTransport = "AzureServiceBus", Version = "4.3.6" };

            //Act
            await DataStore.SaveAuditInstancesInEnvironmentData([auditInstance1, auditInstance2], default);
            var existingBrokerData = await DataStore.GetEnvironmentData(default);

            //Assert
            Assert.That(existingBrokerData, Is.Not.Null);
        }
    }
}