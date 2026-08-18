namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Notifications;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;

    [TestFixture]
    class When_email_notifications_are_enabled : AcceptanceTest
    {
        [Test]
        public async Task Should_send_custom_check_status_change_emails()
        {
            var emailDropPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emailDropPath);
            string[] emailHeaders = [];

            SetSettings = settings =>
            {
                settings.NotificationsFilter = "MyCustomCheckId#Other custom check";
                settings.EmailDropFolder = emailDropPath;
            };

            CustomizeHostBuilder = hostBuilder => hostBuilder.Services.AddHostedService<SetupNotificationSettings>();

            await Define<MyContext>(c =>
                {
                })
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .Done(c =>
                {
                    var emails = Directory.EnumerateFiles(emailDropPath).ToArray();

                    return emails.Length > 0 && TryReadHeaders(emails[0], out emailHeaders);
                })
                .Run();

            Assert.That(emailHeaders, Is.Not.Empty);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(emailHeaders[0], Is.EqualTo("X-Sender: YouServiceControl@particular.net"));
                Assert.That(emailHeaders[1], Is.EqualTo("X-Receiver: WhoeverMightBeConcerned@particular.net"));
                Assert.That(emailHeaders[3], Is.EqualTo("From: YouServiceControl@particular.net"));
                Assert.That(emailHeaders[4], Is.EqualTo("To: WhoeverMightBeConcerned@particular.net"));
                Assert.That(emailHeaders[6], Is.EqualTo("Subject: [Particular.ServiceControl] health check failed"));
            }
        }

        // SmtpClient creates the file in the pickup folder before it writes the message into it, so
        // the headers can only be read once the blank line that terminates them has been written.
        static bool TryReadHeaders(string emailFile, out string[] headers)
        {
            headers = [];

            string[] lines;

            try
            {
                lines = File.ReadAllLines(emailFile);
            }
            catch (IOException)
            {
                return false;
            }

            var endOfHeaders = Array.IndexOf(lines, string.Empty);

            if (endOfHeaders < 0)
            {
                return false;
            }

            headers = lines[..endOfHeaders];

            return true;
        }

        class SetupNotificationSettings(INotificationsDataStore notificationsDataStore) : IHostedService
        {
            public async Task StartAsync(CancellationToken cancellationToken = default)
            {
                var settings = await notificationsDataStore.LoadSettings(cancellationToken);
                settings.Email.Enabled = true;
                settings.Email.From = "YouServiceControl@particular.net";
                settings.Email.To = "WhoeverMightBeConcerned@particular.net";
                await notificationsDataStore.SaveSettings(settings, cancellationToken);
            }

            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        public class MyContext : ScenarioContext
        {
        }

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithFailingCustomCheck() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)); });

            class FailingCustomCheck() : CustomCheck("MyCustomCheckId", "MyCategory")
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
                    => Task.FromResult(CheckResult.Failed("Some reason"));
            }
        }
    }
}