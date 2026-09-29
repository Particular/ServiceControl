namespace ServiceControl.Persistence.Tests.RavenDB.Throughput;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB.Throughput;

[TestFixture]
class AuditServiceMetadataCompatibilityTests : RavenPersistenceTestBase
{
    [Test]
    public async Task Audit_service_metadata_saved_before_the_instance_counts_existed_reads_back_without_them()
    {
        var throughputDatabase = ServiceProvider.GetRequiredService<ThroughputDatabaseConfiguration>().Name;

        using (var session = DocumentStore.OpenAsyncSession(throughputDatabase))
        {
            var metadataWithoutCounts = new AuditServiceMetadataBeforeInstanceCounts(
                new Dictionary<string, int> { ["6.2.0"] = 2 },
                new Dictionary<string, int> { ["RabbitMQ.QuorumConventionalRouting"] = 2 });

            await session.StoreAsync(metadataWithoutCounts, "AuditServiceMetadata");
            await session.SaveChangesAsync();
        }

        var auditServiceMetadata = await LicensingDataStore.GetAuditServiceMetadata();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(auditServiceMetadata.Versions, Is.EquivalentTo(new Dictionary<string, int> { ["6.2.0"] = 2 }));
            Assert.That(auditServiceMetadata.ConfiguredInstances, Is.Null);
            Assert.That(auditServiceMetadata.LiveInstances, Is.Null);
        }
    }

    record AuditServiceMetadataBeforeInstanceCounts(Dictionary<string, int> Versions, Dictionary<string, int> Transports);
}
