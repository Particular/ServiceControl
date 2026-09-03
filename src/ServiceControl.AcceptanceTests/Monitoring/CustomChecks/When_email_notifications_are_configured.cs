namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts.CustomChecks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Notifications;
    using CheckStatus = global::ServiceControl.Persistence.Status;
    using CustomCheck = NServiceBus.CustomChecks.CustomCheck;

    class When_email_notifications_are_configured : AcceptanceTest
    {
        [Test]
        public async Task Should_gate_notifications_on_the_settings_the_page_saved()
        {
            var emailDropPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emailDropPath);

            SetSettings = settings =>
            {
                settings.NotificationsFilter = $"{SilencedCheck}#{WatchedCheck}";
                settings.EmailDropFolder = emailDropPath;
            };

            EmailNotifications initial = null;
            EmailNotifications saved = null;
            EmailNotifications enabled = null;
            HttpResponseMessage testEmail = null;
            string[] delivered = null;

            try
            {
                await Define<Context>()
                    .WithEndpoint<EndpointWithCustomChecks>()
                    .Do("Read the settings the notifications page opens with", async _ =>
                    {
                        initial = await this.TryGet<EmailNotifications>("/api/notifications/email");

                        return initial != null;
                    })
                    .Do("Save an SMTP server to send through", async _ =>
                    {
                        await this.Post("/api/notifications/email", new
                        {
                            smtp_server = SmtpServer,
                            smtp_port = SmtpPort,
                            from = From,
                            to = To,
                            enable_tls = false
                        });

                        saved = await this.TryGet<EmailNotifications>("/api/notifications/email");
                    })
                    .Do("Check the server before relying on it", async _ =>
                    {
                        // Nothing is listening on that port, and the route sends through real SMTP even
                        // when EmailDropFolder is set, so this is the answer a wrong server produces.
                        testEmail = await HttpClient.PostAsync("/api/notifications/email/test", null);
                    })
                    .Do("Let a check fail while notifications are still off", async ctx =>
                    {
                        var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks",
                            check => check.CustomCheckId == SilencedCheck && check.Status == CheckStatus.Fail);

                        return checks.HasResult;
                    })
                    .Do("Switch notifications on", async _ =>
                    {
                        await this.Post("/api/notifications/email/toggle", new { enabled = true });

                        enabled = await this.TryGet<EmailNotifications>("/api/notifications/email");
                    })
                    .Do("Let the other check fail now they are on", async ctx =>
                    {
                        ctx.WatchedCheckFails = true;

                        var emails = await EmailsOnceDelivered(emailDropPath);

                        delivered = emails;

                        return delivered != null;
                    })
                    .Done(_ => true)
                    .Run();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(initial.Enabled, Is.False,
                        "Notifications start off, so saving a server cannot be what turns them on");

                    Assert.That(saved.SmtpServer, Is.EqualTo(SmtpServer));
                    Assert.That(saved.SmtpPort, Is.EqualTo(SmtpPort));
                    Assert.That(saved.From, Is.EqualTo(From));
                    Assert.That(saved.To, Is.EqualTo(To),
                        "The page reads its own form back from this route, so what was saved has to come back");

                    Assert.That(saved.Enabled, Is.False,
                        "Saving a server must not switch notifications on behind the operator");

                    Assert.That(testEmail.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError),
                        "A server that cannot be reached has to be reported, not silently accepted");

                    Assert.That(testEmail.Headers.TryGetValues("X-Particular-Reason", out var reason) ? reason.Single() : null,
                        Is.EqualTo("Error sending test email notification"),
                        "ServicePulse shows this header rather than the status code alone");

                    Assert.That(enabled.Enabled, Is.True);

                    Assert.That(delivered, Has.All.Contains(WatchedCheck),
                        "Only the check that failed after the switch may produce an email");

                    Assert.That(delivered, Has.None.Contains(SilencedCheck),
                        "The check that failed while notifications were off must never be delivered, or the switch is decorative");

                    Assert.That(delivered.Single(), Does.Contain($"From: {From}").And.Contain($"To: {To}"),
                        "The email goes to the addresses saved through the API, not to whatever was in the store");
                }
            }
            finally
            {
                Directory.Delete(emailDropPath, recursive: true);
            }
        }

        // SmtpClient creates the file in the pickup folder before writing to it, so an email is only
        // readable once the blank line that terminates its headers has arrived.
        static async Task<string[]> EmailsOnceDelivered(string emailDropPath)
        {
            var files = Directory.EnumerateFiles(emailDropPath).ToArray();

            if (files.Length == 0)
            {
                return null;
            }

            var contents = new string[files.Length];

            for (var i = 0; i < files.Length; i++)
            {
                string[] lines;

                try
                {
                    lines = await File.ReadAllLinesAsync(files[i]);
                }
                catch (IOException)
                {
                    return null;
                }

                var endOfHeaders = Array.IndexOf(lines, string.Empty);

                if (endOfHeaders < 0)
                {
                    return null;
                }

                contents[i] = string.Join(Environment.NewLine, lines[..endOfHeaders]) + Environment.NewLine + DecodedBody(lines[(endOfHeaders + 1)..]);
            }

            return contents;
        }

        // The notification body is sent base64 encoded, so the check that caused it is only legible
        // once it is decoded.
        static string DecodedBody(string[] lines)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(lines)));
            }
            catch (FormatException)
            {
                return string.Join(Environment.NewLine, lines);
            }
        }

        const string SilencedCheck = "SilencedCheck";
        const string WatchedCheck = "WatchedCheck";
        const string SmtpServer = "localhost";
        const int SmtpPort = 25252;
        const string From = "servicecontrol@particular.net";
        const string To = "oncall@particular.net";

        public class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }

            public bool WatchedCheckFails { get; set; }
        }

        public class EndpointWithCustomChecks : EndpointConfigurationBuilder
        {
            public EndpointWithCustomChecks() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)));

            class AlwaysFailingCheck() : CustomCheck(SilencedCheck, "Testing", TimeSpan.FromSeconds(1))
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default) =>
                    Task.FromResult(CheckResult.Failed("Failing before notifications were switched on"));
            }

            class EventuallyFailingCheck(Context scenarioContext) : CustomCheck(WatchedCheck, "Testing", TimeSpan.FromSeconds(1))
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default) =>
                    Task.FromResult(scenarioContext.WatchedCheckFails
                        ? CheckResult.Failed("Failing after notifications were switched on")
                        : CheckResult.Pass);
            }
        }
    }
}
