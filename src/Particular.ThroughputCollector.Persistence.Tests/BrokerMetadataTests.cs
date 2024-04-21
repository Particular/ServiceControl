namespace Particular.ThroughputCollector.Persistence.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;

[TestFixture]
class BrokerMetadataTests : PersistenceTestFixture
{
    [Test]
    public async Task Should_retrieve_saved_broker_metadata()
    {
        //Arrange
        var expectedBrokerMetadata = new BrokerMetadata("Some scope", new Dictionary<string, string> { ["Some key"] = "Some value" });
        await DataStore.SaveBrokerMetadata(expectedBrokerMetadata);

        //Act
        var retrievedBrokerMetadata = await DataStore.GetBrokerMetadata();

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
        await DataStore.SaveBrokerMetadata(oldBrokerMetadata);

        // Act
        var expectedBrokerMetadata = new BrokerMetadata("New scope", new Dictionary<string, string> { ["New key"] = "New value" });
        await DataStore.SaveBrokerMetadata(expectedBrokerMetadata);
        var retrievedBrokerMetadata = await DataStore.GetBrokerMetadata();

        // Assert
        Assert.That(retrievedBrokerMetadata, Is.Not.Null);
        Assert.That(retrievedBrokerMetadata.ScopeType, Is.EqualTo(expectedBrokerMetadata.ScopeType));
        Assert.That(retrievedBrokerMetadata.Data, Is.EquivalentTo(expectedBrokerMetadata.Data));
    }
}