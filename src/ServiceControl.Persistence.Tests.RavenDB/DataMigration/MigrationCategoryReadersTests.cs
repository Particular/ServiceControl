namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
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

    [Test]
    public void Every_reader_derives_from_the_typed_base()
    {
        var readers = typeof(IMigrationCategoryReader).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IMigrationCategoryReader).IsAssignableFrom(type))
            .ToArray();

        Assert.That(readers, Is.Not.Empty, "the test proves nothing if it finds no reader");
        Assert.That(readers.Where(type => !DerivesFrom(type, typeof(MigrationCategoryReader<>))).Select(type => type.Name), Is.Empty,
            "a reader that implements the interface directly can declare a DocumentType it does not yield, which the pairing test cannot see");
    }

    static bool DerivesFrom(Type type, Type genericBase) =>
        type.BaseType is { } baseType && ((baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericBase) || DerivesFrom(baseType, genericBase));
}
