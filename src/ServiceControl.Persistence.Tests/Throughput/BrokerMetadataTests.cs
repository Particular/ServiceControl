namespace ServiceControl.Persistence.Tests.Throughput;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

[TestFixture, Ignore("TF3601 licensing not active")]
class BrokerMetadataTests : PersistenceTestBase
{
    [Test]
    public async Task Should_retrieve_saved_broker_metadata()
    {
        //Arrange
        var expectedBrokerMetadata = new BrokerMetadata("Some scope", new Dictionary<string, string> { ["Some key"] = "Some value" });
        await LicensingDataStore.SaveBrokerMetadata(expectedBrokerMetadata, default);

        //Act
        var retrievedBrokerMetadata = await LicensingDataStore.GetBrokerMetadata(default);

        //Assert
        Assert.That(retrievedBrokerMetadata, Is.Not.Null);
        Assert.That(retrievedBrokerMetadata.ScopeType, Is.EqualTo(expectedBrokerMetadata.ScopeType));
        Assert.That(retrievedBrokerMetadata.Data, Is.EquivalentTo(expectedBrokerMetadata.Data));
    }

    [Test]
    public async Task Should_update_existing_broker_metadata_if_already_exists()
    {
        // Arrange
        var oldBrokerMetadata = new BrokerMetadata("Some scope", new Dictionary<string, string> { ["Some key"] = "Some value" });
        await LicensingDataStore.SaveBrokerMetadata(oldBrokerMetadata, default);

        // Act
        var expectedBrokerMetadata = new BrokerMetadata("New scope", new Dictionary<string, string> { ["New key"] = "New value" });
        await LicensingDataStore.SaveBrokerMetadata(expectedBrokerMetadata, default);
        var retrievedBrokerMetadata = await LicensingDataStore.GetBrokerMetadata(default);

        // Assert
        Assert.That(retrievedBrokerMetadata, Is.Not.Null);
        Assert.That(retrievedBrokerMetadata.ScopeType, Is.EqualTo(expectedBrokerMetadata.ScopeType));
        Assert.That(retrievedBrokerMetadata.Data, Is.EquivalentTo(expectedBrokerMetadata.Data));
    }
}