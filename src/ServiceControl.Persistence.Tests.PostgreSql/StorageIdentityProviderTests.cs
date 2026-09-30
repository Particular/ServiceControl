namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
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
}
