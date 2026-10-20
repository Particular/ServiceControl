namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

class StorageIdentityProviderTests : PersistenceTestBase
{
    [Test]
    public async Task Identifies_the_server_database_and_schema()
    {
        var provider = ServiceProvider.GetRequiredService<IStorageIdentityProvider>();

        var identity = await provider.GetIdentity();

        Assert.That(identity, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(identity.Engine, Is.EqualTo("SQLServer"));
            Assert.That(identity.Server, Is.Not.Null.And.Not.Empty);
            Assert.That(identity.Database, Is.Not.Null.And.Not.Empty);
            Assert.That(identity.Schema, Is.EqualTo(((SqlServerPersisterSettings)PersistenceSettings).Schema));
        }
    }
}
