namespace ServiceControl.Persistence.Tests.RavenDB.Throughput;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.Persistence.RavenDB.Throughput;
using ServiceControl.Persistence.RavenDB.Throughput.Indexes;

// The licensing queries must use a static index. A dynamic query makes RavenDB create an auto index in the
// throughput database, for example Auto/EndpointDocuments/ByEndpointId.ThroughputSource. An auto index has no
// per-index search engine setting, so it keeps the search engine custom check failing after all static indexes use Lucene.
[TestFixture]
class LicensingDataStoreIndexTests : RavenPersistenceTestBase
{
    [Test]
    public async Task Licensing_queries_create_no_auto_index()
    {
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit) { SanitizedName = "Endpoint" };
        await LicensingDataStore.SaveEndpoint(endpoint);
        await LicensingDataStore.RecordEndpointThroughput(endpoint.Id.Name, ThroughputSource.Audit, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), 50);

        await LicensingDataStore.GetAllEndpoints(includePlatformEndpoints: true);
        await LicensingDataStore.GetAllEndpoints(includePlatformEndpoints: false);
        await LicensingDataStore.GetEndpoints([endpoint.Id]);
        await LicensingDataStore.GetEndpointThroughputByQueueName(["Endpoint"]);
        await LicensingDataStore.UpdateUserIndicatorOnEndpoints([new UpdateUserIndicator { Name = "Endpoint", UserIndicator = "someIndicator" }]);
        await LicensingDataStore.IsThereThroughputForLastXDays(5);
        await LicensingDataStore.IsThereThroughputForLastXDaysForSource(5, ThroughputSource.Audit, includeToday: true);

        var throughputDatabase = ServiceProvider.GetRequiredService<ThroughputDatabaseConfiguration>().Name;
        var indexNames = await DocumentStore.Maintenance.ForDatabase(throughputDatabase).SendAsync(new GetIndexNamesOperation(0, int.MaxValue));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(indexNames.Where(name => name.StartsWith("Auto/")), Is.Empty);
            Assert.That(indexNames, Does.Contain(new EndpointIndex().IndexName));
        }
    }
}
