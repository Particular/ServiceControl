﻿namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.RavenDB;
    using Microsoft.Extensions.DependencyInjection;
    using Notifications;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

    [TestFixture]
    class When_email_notifications_are_enabled : AcceptanceTest
    {
        [Test]
        public async Task Should_send_custom_check_status_change_emails()
        {
            var emailDropPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emailDropPath);
            string[] emails = new string[0];

            SetSettings = settings =>
            {
                settings.NotificationsFilter = "MyCustomCheckId#Other custom check";
                settings.EmailDropFolder = emailDropPath;
            };

            CustomizeHostBuilder = hostBuilder =>
                hostBuilder.ConfigureServices((hostBuilderContext, services) =>
                    services.AddSingleton<IDataMigration, CreateNotificationsDataMigration>());

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

            CollectionAssert.IsNotEmpty(emails);

            var emailText = File.ReadAllLines(emails[0]);

            Assert.AreEqual("X-Sender: YouServiceControl@particular.net", emailText[0]);
            Assert.AreEqual("X-Receiver: WhoeverMightBeConcerned@particular.net", emailText[1]);
            Assert.AreEqual("From: YouServiceControl@particular.net", emailText[3]);
            Assert.AreEqual("To: WhoeverMightBeConcerned@particular.net", emailText[4]);
            Assert.AreEqual("Subject: [Particular.ServiceControl] health check failed", emailText[6]);
        }

        class CreateNotificationsDataMigration : IDataMigration
        {
            public Task Migrate(IDocumentStore store)
            {
                using (var session = store.OpenSession())
                {
                    var notificationsSettings = new NotificationsSettings
                    {
                        Id = NotificationsSettings.SingleDocumentId,
                        Email = new EmailNotifications
                        {
                            Enabled = true,
                            From = "YouServiceControl@particular.net",
                            To = "WhoeverMightBeConcerned@particular.net",
                        }
                    };

                    session.Store(notificationsSettings);

                    session.SaveChanges();
                }

                return Task.CompletedTask;

            }
        }

        public class MyContext : ScenarioContext
        {
            public string EmailDropPath { get; set; }
        }

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithFailingCustomCheck()
            {
                EndpointSetup<DefaultServer>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
            }

            class FailingCustomCheck : CustomCheck
            {
                public FailingCustomCheck()
                    : base("MyCustomCheckId", "MyCategory")
                {
                }

                public override Task<CheckResult> PerformCheck()
                {
                    return Task.FromResult(CheckResult.Failed("Some reason"));
                }
            }
        }
    }
}