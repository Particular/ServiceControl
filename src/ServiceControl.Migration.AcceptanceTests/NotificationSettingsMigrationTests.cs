namespace ServiceControl.Migration.AcceptanceTests;

using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Operations;
using ServiceControl.Notifications;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class NotificationSettingsMigrationTests : MigrationAcceptanceTest
{
    static readonly MigrationCategory NotificationSettingsCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.NotificationSettings)!;

    // Every field differs from its default, so a field the copy drops cannot match by accident.
    static EmailNotifications Seeded => new()
    {
        Enabled = true,
        SmtpServer = "smtp.contoso.com",
        SmtpPort = 587,
        AuthenticationAccount = "servicecontrol",
        AuthenticationPassword = "not-a-real-password",
        EnableTLS = true,
        To = "ops@contoso.com",
        From = "servicecontrol@contoso.com"
    };

    // Version 6.19 and earlier stored this document in the NotificationsSettings collection.
    [TestCase("NotificationsSettingsDocuments", "NotificationsSettings")]
    [TestCase("NotificationsSettings", "NotificationsSettingsDocuments")]
    public async Task Copies_notification_settings_as_one_row_in_settings_from_either_collection(string collection, string otherCollection)
    {
        var seeded = Seeded;
        await SeedSourceNotificationSettings(seeded, collection);

        var result = await CopyCategory("NotificationSettings");

        var collections = (await SourceStore.Maintenance.ForDatabase(Source.PrimaryDatabase).SendAsync(new GetCollectionStatisticsOperation())).Collections;
        var settings = await NotificationsStore.LoadSettings();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(collections, Does.ContainKey(collection).And.Not.ContainKey(otherCollection), "the test proves nothing unless the seed landed in the collection this case names");
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(JsonSerializer.Serialize(settings.Email), Is.EqualTo(JsonSerializer.Serialize(seeded)), "LoadSettings reads through GetSetting<EmailNotifications>, so every field must survive that read");
            Assert.That(await Target.Count(NotificationSettingsCategory), Is.EqualTo(1), "--migration-verify reads this count");
        }
    }

    [Test]
    public async Task A_document_with_no_email_settings_copies_nothing()
    {
        await SeedSourceNotificationSettings(null);

        var result = await CopyCategory("NotificationSettings");

        var settings = await NotificationsStore.LoadSettings();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.Zero);
            Assert.That(await Target.Count(NotificationSettingsCategory), Is.Zero,
                "a row may not carry a null document, and the product reads a null value and a missing row as the same default");
            Assert.That(JsonSerializer.Serialize(settings.Email), Is.EqualTo(JsonSerializer.Serialize(new EmailNotifications())));
        }
    }
}
