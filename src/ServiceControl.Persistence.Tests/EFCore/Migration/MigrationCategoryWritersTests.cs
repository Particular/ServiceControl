namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;

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

    [Test]
    public void Every_writer_derives_from_the_typed_base()
    {
        var writers = typeof(IMigrationCategoryWriter).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IMigrationCategoryWriter).IsAssignableFrom(type))
            .ToArray();

        Assert.That(writers, Is.Not.Empty, "the test proves nothing if it finds no writer");
        Assert.That(writers.Where(type => !DerivesFrom(type, typeof(MigrationCategoryWriter<>))).Select(type => type.Name), Is.Empty,
            "a writer that implements the interface directly skips the named cast and can declare a DocumentType it does not take");
    }

    static bool DerivesFrom(Type type, Type genericBase) =>
        type.BaseType is { } baseType && ((baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericBase) || DerivesFrom(baseType, genericBase));

    [Test]
    public async Task Every_writer_names_its_category_and_the_row_when_a_document_is_not_its_type()
    {
        var target = ServiceProvider.GetRequiredService<IMigrationTarget>();
        await target.Open();

        using (Assert.EnterMultipleScope())
        {
            foreach (var (categoryId, document) in target.SupportedCategoryIds.SelectMany(id => new (string, object)[] { (id, new object()), (id, null) }))
            {
                var category = MigrationCategoryRegistry.Find(categoryId)!;
                var batch = new MigrationBatch([new MigrationRow("Wrong/1", document, new Dictionary<string, object>())], "Wrong/1");
                var checkpoint = new MigrationCheckpoint(categoryId, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null);

                var exception = Assert.ThrowsAsync<InvalidCastException>(() => target.Write(category, batch, checkpoint));

                Assert.That(exception!.Message, Does.Contain(categoryId).And.Contain("Wrong/1").And.Contain(target.DocumentTypes[categoryId].FullName), "an engine halt quotes this message, so it has to say which pair disagrees");
            }
        }
    }
}
