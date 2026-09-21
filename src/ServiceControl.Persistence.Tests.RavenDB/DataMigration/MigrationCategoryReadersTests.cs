namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.DataMigration;

[TestFixture]
class MigrationCategoryReadersTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Every_reader_claims_a_category_the_registry_knows()
    {
        await using var source = (RavenMigrationSource)await OpenMigrationSource();

        var unknown = source.SupportedCategoryIds.Where(id => MigrationCategoryRegistry.Find(id) is null).ToArray();

        Assert.That(unknown, Is.Empty, "a reader for an id no category has is dead code the engine can never reach");
    }
}
