namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using NUnit.Framework;

class NotificationsDataStoreTests : PersistenceTestBase
{
    [Test, CancelAfter(30_000)]
    public async Task LoadSettings_returns_defaults_when_no_settings_exist()
    {
        using var manager = await NotificationsStore.CreateNotificationsManager();

        var settings = await manager.LoadSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Email, Is.Not.Null);
            Assert.That(settings.Email.Enabled, Is.False);
            Assert.That(settings.Email.SmtpServer, Is.Null);
            Assert.That(settings.Email.SmtpPort, Is.Null);
            Assert.That(settings.Email.EnableTLS, Is.False);
            Assert.That(settings.Email.From, Is.Null);
            Assert.That(settings.Email.To, Is.Null);
            Assert.That(settings.Email.AuthenticationAccount, Is.Null);
            Assert.That(settings.Email.AuthenticationPassword, Is.Null);
        }
    }

    [Test, CancelAfter(30_000)]
    public async Task SaveChanges_persists_email_settings_round_trip()
    {
        using (var manager = await NotificationsStore.CreateNotificationsManager())
        {
            var settings = await manager.LoadSettings();

            settings.Email.Enabled = true;
            settings.Email.SmtpServer = "smtp.example.com";
            settings.Email.SmtpPort = 587;
            settings.Email.EnableTLS = true;
            settings.Email.From = "sc@example.com";
            settings.Email.To = "ops@example.com";
            settings.Email.AuthenticationAccount = "user";
            settings.Email.AuthenticationPassword = "p@ssw0rd";

            await manager.SaveChanges();
        }

        await CompleteDatabaseOperation();

        using var verifyManager = await NotificationsStore.CreateNotificationsManager();
        var loaded = await verifyManager.LoadSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Email.Enabled, Is.True);
            Assert.That(loaded.Email.SmtpServer, Is.EqualTo("smtp.example.com"));
            Assert.That(loaded.Email.SmtpPort, Is.EqualTo(587));
            Assert.That(loaded.Email.EnableTLS, Is.True);
            Assert.That(loaded.Email.From, Is.EqualTo("sc@example.com"));
            Assert.That(loaded.Email.To, Is.EqualTo("ops@example.com"));
            Assert.That(loaded.Email.AuthenticationAccount, Is.EqualTo("user"));
            Assert.That(loaded.Email.AuthenticationPassword, Is.EqualTo("p@ssw0rd"));
        }
    }

    [Test, CancelAfter(30_000)]
    public async Task Toggling_enabled_is_persisted()
    {
        using (var manager = await NotificationsStore.CreateNotificationsManager())
        {
            var settings = await manager.LoadSettings();
            settings.Email.Enabled = true;
            await manager.SaveChanges();
        }

        await CompleteDatabaseOperation();

        using (var manager = await NotificationsStore.CreateNotificationsManager())
        {
            var settings = await manager.LoadSettings();
            Assert.That(settings.Email.Enabled, Is.True);

            settings.Email.Enabled = false;
            await manager.SaveChanges();
        }

        await CompleteDatabaseOperation();

        using var verifyManager = await NotificationsStore.CreateNotificationsManager();
        var final = await verifyManager.LoadSettings();
        Assert.That(final.Email.Enabled, Is.False);
    }

    [Test, CancelAfter(30_000)]
    public async Task LoadSettings_returns_previously_saved_settings()
    {
        using (var manager = await NotificationsStore.CreateNotificationsManager())
        {
            var settings = await manager.LoadSettings();
            settings.Email.SmtpServer = "configured.server";
            settings.Email.SmtpPort = 2525;
            await manager.SaveChanges();
        }

        await CompleteDatabaseOperation();

        using var manager2 = await NotificationsStore.CreateNotificationsManager();
        var loaded = await manager2.LoadSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Email.SmtpServer, Is.EqualTo("configured.server"));
            Assert.That(loaded.Email.SmtpPort, Is.EqualTo(2525));
            // Untouched fields keep their defaults
            Assert.That(loaded.Email.Enabled, Is.False);
            Assert.That(loaded.Email.EnableTLS, Is.False);
        }
    }

    [Test, CancelAfter(30_000)]
    public async Task Updating_individual_fields_preserves_others()
    {
        using (var manager = await NotificationsStore.CreateNotificationsManager())
        {
            var settings = await manager.LoadSettings();
            settings.Email.Enabled = true;
            settings.Email.SmtpServer = "original.smtp";
            settings.Email.SmtpPort = 25;
            settings.Email.EnableTLS = false;
            settings.Email.From = "from@orig";
            settings.Email.To = "to@orig";
            settings.Email.AuthenticationAccount = "acct";
            settings.Email.AuthenticationPassword = "secret";
            await manager.SaveChanges();
        }

        await CompleteDatabaseOperation();

        using (var manager = await NotificationsStore.CreateNotificationsManager())
        {
            var settings = await manager.LoadSettings();
            settings.Email.SmtpServer = "updated.smtp";
            settings.Email.EnableTLS = true;
            await manager.SaveChanges();
        }

        await CompleteDatabaseOperation();

        using var verifyManager = await NotificationsStore.CreateNotificationsManager();
        var loaded = await verifyManager.LoadSettings();

        using (Assert.EnterMultipleScope())
        {
            // Updated fields
            Assert.That(loaded.Email.SmtpServer, Is.EqualTo("updated.smtp"));
            Assert.That(loaded.Email.EnableTLS, Is.True);
            // Preserved fields
            Assert.That(loaded.Email.Enabled, Is.True);
            Assert.That(loaded.Email.SmtpPort, Is.EqualTo(25));
            Assert.That(loaded.Email.From, Is.EqualTo("from@orig"));
            Assert.That(loaded.Email.To, Is.EqualTo("to@orig"));
            Assert.That(loaded.Email.AuthenticationAccount, Is.EqualTo("acct"));
            Assert.That(loaded.Email.AuthenticationPassword, Is.EqualTo("secret"));
        }
    }
}