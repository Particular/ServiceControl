namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class PendingIntegrationEventRowMapperTests : PersistenceTestBase
{
    [Test]
    public void Sets_every_mapped_property_of_a_dispatch_request()
    {
        var entity = PendingIntegrationEventRowMapper.Map(Request("one"));

        using var scope = ServiceProvider.CreateScope();
        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model, entity);
    }

    [Test]
    public async Task Writes_exactly_the_type_name_and_json_the_product_writes_for_the_same_context()
    {
        await ExternalIntegrationStore.StoreDispatchRequest([Request("one")]);

        using var scope = ServiceProvider.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().ExternalIntegrationDispatchRequests.AsNoTracking().SingleAsync();
        var mapped = PendingIntegrationEventRowMapper.Map(Request("one"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mapped.DispatchContextTypeName, Is.EqualTo(stored.DispatchContextTypeName));
            Assert.That(mapped.DispatchContextJson, Is.EqualTo(stored.DispatchContextJson));
        }
    }

    static ExternalIntegrationDispatchRequest Request(string value) => new() { DispatchContext = new TestDispatchContext { Value = value } };

    public class TestDispatchContext
    {
        public string Value { get; set; }
    }
}
