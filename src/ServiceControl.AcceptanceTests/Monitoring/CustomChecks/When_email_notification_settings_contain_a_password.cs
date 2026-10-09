namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Notifications;

    /// <summary>
    /// The SMTP password is write-only: the settings route never returns it. This checks the HTTP
    /// response end to end. NotificationsControllerTests covers how a save changes the stored password.
    /// </summary>
    class When_email_notification_settings_contain_a_password : AcceptanceTest
    {
        [Test]
        public async Task Should_not_return_the_password()
        {
            EmailNotifications returned = null;

            await Define<Context>()
                .Do("Save settings with a password", async _ =>
                {
                    await this.Post("/api/notifications/email", SettingsRequest(Account, Password));

                    return true;
                })
                .Do("Read the settings back", async _ =>
                {
                    returned = await this.TryGet<EmailNotifications>("/api/notifications/email");

                    return returned != null;
                })
                .Done(_ => true)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(returned.AuthenticationAccount, Is.EqualTo(Account), "The account is not a secret and stays visible");
                Assert.That(returned.AuthenticationPassword, Is.Null, "The password must never leave the instance through the API");
            }
        }

        static object SettingsRequest(string account, string password) => new
        {
            smtp_server = "localhost",
            smtp_port = 25,
            authorization_account = account,
            authorization_password = password,
            enable_tls = false,
            from = "servicecontrol@particular.net",
            to = "oncall@particular.net"
        };

        const string Account = "svc-notifications";
        const string Password = "p@ssw0rd";

        public class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
        }
    }
}
