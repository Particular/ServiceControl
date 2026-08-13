namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Notifications;

class NotificationsDataStoreTests : PersistenceTestBase
{
    [Test, CancelAfter(30_000)]
    public async Task LoadSettings_returns_defaults_when_no_settings_exist()
    {
        var settings = await NotificationsStore.LoadSettings(TestContext.CurrentContext.CancellationToken);

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
    public void LoadSettings_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(
            async () => await NotificationsStore.LoadSettings(cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test, CancelAfter(30_000)]
    public void SaveSettings_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(
            async () => await NotificationsStore.SaveSettings(CreateSettings("cancelled.smtp"), cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test, CancelAfter(30_000)]
    public async Task SaveSettings_without_a_prior_load_round_trips_all_values()
    {
        var settings = CreateSettings("smtp.example.com");

        await NotificationsStore.SaveSettings(settings, TestContext.CurrentContext.CancellationToken);
        await CompleteDatabaseOperation();

        var loaded = await NotificationsStore.LoadSettings(TestContext.CurrentContext.CancellationToken);
        AssertSettings(loaded, "smtp.example.com");
    }

    [Test, CancelAfter(30_000)]
    public async Task SaveSettings_replaces_the_persisted_snapshot()
    {
        await NotificationsStore.SaveSettings(CreateSettings("original.smtp"), TestContext.CurrentContext.CancellationToken);

        var replacement = CreateSettings("replacement.smtp");
        replacement.Email.Enabled = false;
        replacement.Email.SmtpPort = 2525;
        replacement.Email.To = "replacement@example.com";
        await NotificationsStore.SaveSettings(replacement, TestContext.CurrentContext.CancellationToken);

        replacement.Email.SmtpServer = "mutated.after.save";
        replacement.Email.To = "mutated@example.com";

        await CompleteDatabaseOperation();
        var loaded = await NotificationsStore.LoadSettings(TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded, Is.Not.SameAs(replacement));
            Assert.That(loaded.Email, Is.Not.SameAs(replacement.Email));
            Assert.That(loaded.Email.Enabled, Is.False);
            Assert.That(loaded.Email.SmtpServer, Is.EqualTo("replacement.smtp"));
            Assert.That(loaded.Email.SmtpPort, Is.EqualTo(2525));
            Assert.That(loaded.Email.To, Is.EqualTo("replacement@example.com"));
            Assert.That(loaded.Email.From, Is.EqualTo("sc@example.com"));
        }
    }

    [Test, CancelAfter(30_000)]
    public async Task Repeated_saves_behave_consistently()
    {
        var settings = CreateSettings("repeat.smtp");

        await NotificationsStore.SaveSettings(settings, TestContext.CurrentContext.CancellationToken);
        await NotificationsStore.SaveSettings(settings, TestContext.CurrentContext.CancellationToken);
        await NotificationsStore.SaveSettings(settings, TestContext.CurrentContext.CancellationToken);

        await CompleteDatabaseOperation();
        var loaded = await NotificationsStore.LoadSettings(TestContext.CurrentContext.CancellationToken);
        AssertSettings(loaded, "repeat.smtp");
    }

    static NotificationsSettings CreateSettings(string smtpServer) => new()
    {
        Email = new EmailNotifications
        {
            Enabled = true,
            SmtpServer = smtpServer,
            SmtpPort = 587,
            EnableTLS = true,
            From = "sc@example.com",
            To = "ops@example.com",
            AuthenticationAccount = "user",
            AuthenticationPassword = "p@ssw0rd"
        }
    };

    static void AssertSettings(NotificationsSettings settings, string smtpServer)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Email.Enabled, Is.True);
            Assert.That(settings.Email.SmtpServer, Is.EqualTo(smtpServer));
            Assert.That(settings.Email.SmtpPort, Is.EqualTo(587));
            Assert.That(settings.Email.EnableTLS, Is.True);
            Assert.That(settings.Email.From, Is.EqualTo("sc@example.com"));
            Assert.That(settings.Email.To, Is.EqualTo("ops@example.com"));
            Assert.That(settings.Email.AuthenticationAccount, Is.EqualTo("user"));
            Assert.That(settings.Email.AuthenticationPassword, Is.EqualTo("p@ssw0rd"));
        }
    }
}
