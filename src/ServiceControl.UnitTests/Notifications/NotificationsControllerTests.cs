#nullable enable
namespace ServiceControl.UnitTests.Notifications;

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Notifications;
using ServiceControl.Notifications.Api;
using ServiceControl.Persistence;

/// <summary>
/// The SMTP password is write-only: the settings route never returns it, and a save without a
/// password keeps the stored one. ServicePulse reads the settings into its form and sends the form
/// back, so a save without a password must not erase the password that was stored before.
/// </summary>
[TestFixture]
class NotificationsControllerTests
{
    [Test]
    public async Task Should_not_return_the_password()
    {
        var store = new InMemoryNotificationsDataStore(Account, Password);

        var returned = await Controller(store).GetEmailNotificationsSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(returned.AuthenticationAccount, Is.EqualTo(Account), "The account is not a secret and stays visible");
            Assert.That(returned.AuthenticationPassword, Is.Null, "The password must never leave the instance through the API");
            Assert.That(store.Stored.AuthenticationPassword, Is.EqualTo(Password), "Reading the settings must not change the stored password");
        }
    }

    [Test]
    public async Task Should_keep_the_stored_password_when_saved_without_one()
    {
        var store = new InMemoryNotificationsDataStore(Account, Password);

        await Controller(store).UpdateSettings(Request(Account, password: null));

        Assert.That(store.Stored.AuthenticationPassword, Is.EqualTo(Password));
    }

    [Test]
    public async Task Should_replace_the_stored_password_when_saved_with_a_new_one()
    {
        var store = new InMemoryNotificationsDataStore(Account, Password);

        await Controller(store).UpdateSettings(Request(Account, NewPassword));

        Assert.That(store.Stored.AuthenticationPassword, Is.EqualTo(NewPassword));
    }

    [Test]
    public async Task Should_remove_the_stored_password_when_saved_without_an_account()
    {
        var store = new InMemoryNotificationsDataStore(Account, Password);

        await Controller(store).UpdateSettings(Request(account: null, password: null));

        Assert.That(store.Stored.AuthenticationPassword, Is.Null, "Without an account the password is never used, so it must not stay stored");
    }

    static NotificationsController Controller(INotificationsDataStore store) => new(store, null!, null!);

    static UpdateEmailNotificationsSettingsRequest Request(string? account, string? password) => new()
    {
        SmtpServer = "localhost",
        SmtpPort = 25,
        AuthorizationAccount = account,
        AuthorizationPassword = password,
        EnableTLS = false,
        From = "servicecontrol@particular.net",
        To = "oncall@particular.net"
    };

    const string Account = "svc-notifications";
    const string Password = "p@ssw0rd";
    const string NewPassword = "n3w-p@ssw0rd";

    // Loads and saves copies, like the real stores, so the controller cannot change the stored value in place.
    class InMemoryNotificationsDataStore(string account, string password) : INotificationsDataStore
    {
        public EmailNotifications Stored { get; private set; } = new() { AuthenticationAccount = account, AuthenticationPassword = password };

        public Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default) =>
            Task.FromResult(new NotificationsSettings { Email = Copy(Stored) });

        public Task SaveSettings(NotificationsSettings settings, CancellationToken cancellationToken = default)
        {
            Stored = Copy(settings.Email);
            return Task.CompletedTask;
        }

        static EmailNotifications Copy(EmailNotifications source) => new()
        {
            Enabled = source.Enabled,
            SmtpServer = source.SmtpServer,
            SmtpPort = source.SmtpPort,
            AuthenticationAccount = source.AuthenticationAccount,
            AuthenticationPassword = source.AuthenticationPassword,
            EnableTLS = source.EnableTLS,
            To = source.To,
            From = source.From
        };
    }
}
