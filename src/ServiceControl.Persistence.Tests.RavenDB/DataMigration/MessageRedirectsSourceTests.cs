namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.MessageRedirects;
using RavenMessageRedirectsDataStore = Persistence.RavenDB.MessageRedirects.MessageRedirectsDataStore;

class MessageRedirectsSourceTests : RavenMigrationSourceTestBase
{
    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.MessageRedirects);

    [Test]
    public async Task A_document_whose_last_redirect_was_removed_gives_one_empty_batch()
    {
        var redirect = new MessageRedirect { FromPhysicalAddress = "old1", ToPhysicalAddress = "new1", LastModified = DateTime.UtcNow };
        await MessageRedirectsDataStore.AddRedirect(redirect);
        await MessageRedirectsDataStore.RemoveRedirect(redirect);

        await using var source = await OpenMigrationSource();
        var batches = await CollectBatches(source, Category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.SelectMany(batch => batch.Rows), Is.Empty);
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { RavenMessageRedirectsDataStore.CollectionId }));
        }
    }
}
