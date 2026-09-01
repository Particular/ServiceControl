namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
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
    using CheckStatus = global::ServiceControl.Persistence.Status;
    using CustomCheck = NServiceBus.CustomChecks.CustomCheck;

    class When_a_failing_custom_check_is_dismissed : AcceptanceTest
    {
        [Test]
        public async Task Should_come_back_while_the_check_is_still_failing()
        {
            CustomCheckView dismissed = null;
            CustomCheckView returned = null;

            await Define<Context>()
                .WithEndpoint<Checked>()
                .Do("Wait for the check to report a failure", async ctx =>
                {
                    var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks",
                        check => check.CustomCheckId == CheckId && check.Status == CheckStatus.Fail);

                    dismissed = checks.HasResult ? checks.Items.Single() : null;

                    return dismissed != null;
                })
                .Do("Dismiss it from the page", async _ =>
                    await this.Delete($"/api/customchecks/{WithoutPrefix(dismissed.Id)}"))
                .Do("Wait until it has gone", async _ =>
                {
                    var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks");

                    return checks.Items.All(check => check.CustomCheckId != CheckId);
                })
                .Do("Wait for the next report from the endpoint", async _ =>
                {
                    var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks",
                        check => check.CustomCheckId == CheckId && check.Status == CheckStatus.Fail);

                    returned = checks.HasResult ? checks.Items.Single() : null;

                    return returned != null;
                })
                .Done(_ => true)
                .Run();

            Assert.That(returned.FailureReason, Is.EqualTo(dismissed.FailureReason),
                "Dismissing a check that is still failing cannot silence it for good, or a real failure disappears from the page for as long as it lasts");
        }

        static string WithoutPrefix(string id) =>
            id.StartsWith(DocumentPrefix, StringComparison.OrdinalIgnoreCase) ? id[DocumentPrefix.Length..] : id;

        const string DocumentPrefix = "CustomChecks/";
        const string CheckId = "DismissedCheck";

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
        }

        public class Checked : EndpointConfigurationBuilder
        {
            public Checked() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)));

            class FailingCheck() : CustomCheck(CheckId, "Testing", TimeSpan.FromSeconds(1))
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default) =>
                    Task.FromResult(CheckResult.Failed("Still failing"));
            }
        }
    }
}
