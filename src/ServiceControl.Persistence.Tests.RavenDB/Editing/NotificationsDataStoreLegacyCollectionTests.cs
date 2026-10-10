namespace ServiceControl.Persistence.Tests.RavenDB.Editing;

using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client;
using ServiceControl.Notifications;
using ServiceControl.Persistence.RavenDB.Editing;

class NotificationsDataStoreLegacyCollectionTests : PersistenceTestBase
{
    [Test, CancelAfter(30_000)]
    public async Task SaveSettings_updates_a_document_held_in_the_legacy_collection()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        using (var session = PersistenceTestsContext.DocumentStore.OpenAsyncSession())
        {
            var legacy = new NotificationsSettingsDocument { Email = new EmailNotifications { SmtpServer = "legacy.smtp" } };
            await session.StoreAsync(legacy, "NotificationsSettings/All", cancellationToken);
            // Databases written by ServiceControl 6.19 and earlier hold the document in this collection, under the old class name.
            var metadata = session.Advanced.GetMetadataFor(legacy);
            metadata[Constants.Documents.Metadata.Collection] = "NotificationsSettings";
            metadata[Constants.Documents.Metadata.RavenClrType] = "ServiceControl.Notifications.NotificationsSettings, ServiceControl.Persistence";
            await session.SaveChangesAsync(cancellationToken);
        }

        using (var session = PersistenceTestsContext.DocumentStore.OpenAsyncSession())
        {
            var seeded = await session.LoadAsync<NotificationsSettingsDocument>("NotificationsSettings/All", cancellationToken);
            Assert.That(session.Advanced.GetMetadataFor(seeded)[Constants.Documents.Metadata.Collection], Is.EqualTo("NotificationsSettings"));
        }

        var settings = await NotificationsStore.LoadSettings(cancellationToken);
        Assert.That(settings.Email.SmtpServer, Is.EqualTo("legacy.smtp"));
        settings.Email.SmtpServer = "updated.smtp";
        await NotificationsStore.SaveSettings(settings, cancellationToken);

        await CompleteDatabaseOperation();
        var loaded = await NotificationsStore.LoadSettings(cancellationToken);
        Assert.That(loaded.Email.SmtpServer, Is.EqualTo("updated.smtp"));
    }
}
