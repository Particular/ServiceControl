namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;

class FailedErrorImportSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Every_import_arrives_with_its_body_attached_and_the_time_RavenDB_last_modified_it()
    {
        await FailedImportStore.StoreFailedErrorImport(new FailedErrorImport
        {
            Id = "native-1",
            Message = new FailedTransportMessage { Id = "native-1", Headers = new Dictionary<string, string> { ["NServiceBus.MessageId"] = "native-1" }, Body = [1, 2, 3] },
            ExceptionInfo = "Deserialization failed"
        });
        await FailedImportStore.StoreFailedErrorImport(new FailedErrorImport { Id = "native-2", Message = null, ExceptionInfo = "Deserialization failed" });
        await CompleteDatabaseOperation();

        DateTime lastModified;

        using (var session = await SessionProvider.OpenSession())
        {
            lastModified = session.Advanced.GetLastModifiedFor(await session.LoadAsync<FailedErrorImport>(FailedErrorImportDataStore.MakeDocumentId("native-1"))).Value;
        }

        await using var source = await OpenMigrationSource();

        var rows = (await CollectBatches(source, MigrationCategoryRegistry.Find(MigrationCategoryIds.FailedErrorImports))).SelectMany(batch => batch.Rows).ToDictionary(row => row.SourceId);
        var withMessage = rows[FailedErrorImportDataStore.MakeDocumentId("native-1")];
        var withoutMessage = rows[FailedErrorImportDataStore.MakeDocumentId("native-2")];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withMessage.Body!.Content.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }), "attached in Read, so the engine never asks ReadBody, which refuses this category");
            Assert.That(withMessage.Body.ContentType, Is.EqualTo("application/octet-stream"), "the default the SQL import store gives a message with no content type header");
            Assert.That(withMessage.Metadata[MigrationMetadataKeys.FailedAt], Is.EqualTo(lastModified.ToUniversalTime()), "RavenDB keeps no failure time, only the stamp it wrote when the import was stored");
            Assert.That(((DateTime)withMessage.Metadata[MigrationMetadataKeys.FailedAt]!).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(withoutMessage.Body?.Content.Length, Is.Zero, "an empty body rather than none, so the writer skips the row instead of the engine stopping on ReadBody");
        }
    }
}
