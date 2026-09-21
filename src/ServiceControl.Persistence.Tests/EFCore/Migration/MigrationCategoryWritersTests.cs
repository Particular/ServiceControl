namespace ServiceControl.Persistence.Tests;

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

class MigrationCategoryWritersTests : PersistenceTestBase
{
    [Test]
    public void Every_writer_claims_a_category_the_registry_knows()
    {
        var unknown = ServiceProvider.GetRequiredService<IMigrationTarget>().SupportedCategoryIds
            .Where(id => MigrationCategoryRegistry.Find(id) is null)
            .ToArray();

        Assert.That(unknown, Is.Empty, "a writer for an id no category has is dead code the engine can never reach");
    }
}
