namespace Particular.ThroughputCollector.Persistence.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;

[TestFixture]
class AuditServiceMetadataTests : PersistenceTestFixture
{
    [Test]
    public async Task Should_retrieve_saved_audit_service_metadata()
    {
        //Arrange
        var expectedAuditServiceMetadata = new AuditServiceMetadata(
            new Dictionary<string, int> { ["Some version"] = 2 },
            new Dictionary<string, int> { ["Some transport"] = 3 });
        await DataStore.SaveAuditServiceMetadata(expectedAuditServiceMetadata, default);

        //Act
        var retrievedAuditServiceMetadata = await DataStore.GetAuditServiceMetadata();

        //Assert
        Assert.That(retrievedAuditServiceMetadata, Is.Not.Null);
        Assert.That(retrievedAuditServiceMetadata.Transports, Is.EquivalentTo(expectedAuditServiceMetadata.Transports));
        Assert.That(retrievedAuditServiceMetadata.Versions, Is.EquivalentTo(expectedAuditServiceMetadata.Versions));
    }

    [Test]
    public async Task Should_update_existing_audit_service_metadata_if_already_exists()
    {
        // Arrange
        var oldAuditServiceMetadata = new AuditServiceMetadata(
            new Dictionary<string, int> { ["Some version"] = 2 },
            new Dictionary<string, int> { ["Some transport"] = 3 });
        await DataStore.SaveAuditServiceMetadata(oldAuditServiceMetadata, default);

        // Act
        var expectedAuditServiceMetadata = new AuditServiceMetadata(
            new Dictionary<string, int> { ["Some version"] = 2, ["New version"] = 1 },
            new Dictionary<string, int> { ["Some transport"] = 4 });
        await DataStore.SaveAuditServiceMetadata(expectedAuditServiceMetadata, default);
        var retrievedAuditServiceMetadata = await DataStore.GetAuditServiceMetadata();

        // Assert
        Assert.That(retrievedAuditServiceMetadata, Is.Not.Null);
        Assert.That(retrievedAuditServiceMetadata.Transports, Is.EquivalentTo(expectedAuditServiceMetadata.Transports));
        Assert.That(retrievedAuditServiceMetadata.Versions, Is.EquivalentTo(expectedAuditServiceMetadata.Versions));
    }
}