namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.Infrastructure;
using EFCore.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;

class StorageIdentityProviderTests : PersistenceTestBase
{
    [Test]
    public async Task Identifies_the_cluster_database_and_schema()
    {
        var provider = ServiceProvider.GetRequiredService<IStorageIdentityProvider>();

        var identity = await provider.GetIdentity();

        Assert.That(identity, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(identity.Engine, Is.EqualTo("PostgreSQL"));
            Assert.That(identity.Server, Is.Not.Null.And.Not.Empty);
            Assert.That(identity.Database, Is.Not.Null.And.Not.Empty);
            Assert.That(identity.Schema, Is.EqualTo(((PostgreSqlPersisterSettings)PersistenceSettings).Schema));
        }
    }

    [Test]
    public async Task Measures_the_search_path_schema_when_none_is_configured()
    {
        var configured = (PostgreSqlPersisterSettings)PersistenceSettings;
        var services = new ServiceCollection();
        services.AddLogging();
        new PostgreSqlPersistenceConfiguration().Create(new PostgreSqlPersisterSettings
        {
            ConnectionString = new NpgsqlConnectionStringBuilder(configured.ConnectionString) { SearchPath = configured.Schema }.ConnectionString,
            BodyStorage = configured.BodyStorage,
            ErrorRetentionPeriod = configured.ErrorRetentionPeriod,
            EventsRetentionPeriod = configured.EventsRetentionPeriod
        }).AddPersistence(services);
        await using var unconfigured = services.BuildServiceProvider();

        var identity = await unconfigured.GetRequiredService<IStorageIdentityProvider>().GetIdentity();
        var footprint = await unconfigured.GetRequiredService<IStorageFootprintProbe>().Probe();
        var expected = await ServiceProvider.GetRequiredService<IStorageFootprintProbe>().Probe();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(identity?.Schema, Is.EqualTo(configured.Schema));
            Assert.That(footprint?.SizeGB, Is.Not.Null.And.EqualTo(expected?.SizeGB));
        }
    }
}
