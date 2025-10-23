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
            string[] emails = [];

            SetSettings = settings =>
            {
                settings.ServiceControl.NotificationsFilter = "MyCustomCheckId#Other custom check";
                settings.ServiceControl.EmailDropFolder = emailDropPath;
            };

            CustomizeHostBuilder = hostBuilder => hostBuilder.Services.AddHostedService<SetupNotificationSettings>();

            await Define<MyContext>(c =>
                {
                    c.EmailDropPath = emailDropPath;
                })
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .Done(c =>
                {
                    emails = Directory.EnumerateFiles(emailDropPath).ToArray();
                    return emails.Length > 0;
                })
                .Run();

            Assert.That(emails, Is.Not.Empty);

            var emailText = await File.ReadAllLinesAsync(emails[0]);

            Assert.Multiple(() =>
            {
                Assert.That(emailText[0], Is.EqualTo("X-Sender: YouServiceControl@particular.net"));
                Assert.That(emailText[1], Is.EqualTo("X-Receiver: WhoeverMightBeConcerned@particular.net"));
                Assert.That(emailText[3], Is.EqualTo("From: YouServiceControl@particular.net"));
                Assert.That(emailText[4], Is.EqualTo("To: WhoeverMightBeConcerned@particular.net"));
                Assert.That(emailText[6], Is.EqualTo("Subject: [Particular.ServiceControl] health check failed"));
            });
        }

        class SetupNotificationSettings(IErrorMessageDataStore errorMessageDataStore) : IHostedService
        {
            public async Task StartAsync(CancellationToken cancellationToken)
            {
                using var notificationsManager = await errorMessageDataStore.CreateNotificationsManager();

                var settings = await notificationsManager.LoadSettings();
                settings.Email = new EmailNotifications
                {
                    Enabled = true,
                    From = "YouServiceControl@particular.net",
                    To = "WhoeverMightBeConcerned@particular.net",
                };

                await notificationsManager.SaveChanges();
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public class MyContext : ScenarioContext
        {
            public string EmailDropPath { get; set; }
        }

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithFailingCustomCheck() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.ReportCustomChecksTo(PrimaryOptions.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)); });

            class FailingCustomCheck() : CustomCheck("MyCustomCheckId", "MyCategory")
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
                    => Task.FromResult(CheckResult.Failed("Some reason"));
            }
        }
    }
}