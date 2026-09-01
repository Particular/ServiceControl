namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using CustomCheckSeverity = global::ServiceControl.Contracts.CustomChecks.CustomCheckSeverity;
    using CustomCheckView = global::ServiceControl.Contracts.CustomChecks.CustomCheckView;
    using CheckStatus = global::ServiceControl.Persistence.Status;

    [TestFixture]
    class When_custom_checks_are_classified : AcceptanceTest
    {
        // Runs at startup with TimeSpan.Zero, so acceptance tests can assert on it without waiting an interval.
        const string InternalId = "ServiceControl Primary Instance";

        [Test]
        public async Task Internal_checks_are_flagged_with_severity_and_endpoint_checks_are_not()
        {
            // The acceptance test runner disables internal custom checks by default; this test needs them.
            SetSettings = settings => { settings.DisableHealthChecks = false; };

            CustomCheckView internalCheck = null;
            CustomCheckView endpointCheck = null;
            string wireBody = null;

            await Define<Context>()
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .Done(async c =>
                {
                    var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks");

                    internalCheck ??= checks.Items.SingleOrDefault(x => x.CustomCheckId == InternalId);
                    endpointCheck ??= checks.Items.SingleOrDefault(x => x.CustomCheckId == "MyCustomCheckId" && x.Status == CheckStatus.Fail);

                    // The view computes Internal/Severity from the check id, so deserializing alone would not
                    // prove the endpoint emits them. Grab the raw payload once and assert on the wire itself.
                    if (internalCheck != null && endpointCheck != null && wireBody == null)
                    {
                        var raw = await this.GetRaw("/api/customchecks");
                        wireBody = await raw.Content.ReadAsStringAsync();
                    }

                    return internalCheck != null && endpointCheck != null && wireBody != null;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(internalCheck, Is.Not.Null, "primary internal checks report at startup; nothing was found");
                Assert.That(internalCheck.Internal, Is.True);
                Assert.That(internalCheck.Severity, Is.EqualTo(CustomCheckSeverity.Unavailable));

                Assert.That(endpointCheck, Is.Not.Null);
                Assert.That(endpointCheck.Internal, Is.False);
                Assert.That(endpointCheck.Severity, Is.Null);

                // What the wire actually carries:
                Assert.That(wireBody, Does.Contain("\"internal\":true"), "internal checks must render internal:true on the wire");
                Assert.That(wireBody, Does.Contain("\"severity\":\"unavailable\""), "the primary instance check must render severity:unavailable on the wire");
                Assert.That(wireBody, Does.Contain("\"internal\":false"), "endpoint checks must render internal:false on the wire");
            }
        }

        [Test]
        public async Task Severity_matches_the_spiked_platform_health_config_for_every_internal_check_present()
        {
            // The acceptance test runner disables internal custom checks by default; this test needs them.
            SetSettings = settings => { settings.DisableHealthChecks = false; };

            var expected = new (string Id, CustomCheckSeverity Severity)[]
            {
                ("ServiceControl Primary Instance", CustomCheckSeverity.Unavailable),
                ("ServiceControl Remotes", CustomCheckSeverity.Unavailable),
                ("Saga Audit Configuration", CustomCheckSeverity.Ignore),
                // RavenDB persister checks also assert here on the RavenDB acceptance variant:
                ("Error Message Ingestion Process", CustomCheckSeverity.Degraded),
                ("Error Message Ingestion", CustomCheckSeverity.Degraded),
            };

            var seen = new System.Collections.Generic.List<CustomCheckView>();

            await Define<Context>()
                .Done(async c =>
                {
                    var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks");
                    foreach (var item in checks.Items)
                    {
                        // The Done predicate polls, so keep one row per check id
                        if (seen.All(s => s.Id != item.Id))
                        {
                            seen.Add(item);
                        }
                    }

                    return expected.All(e => seen.Any(s => s.CustomCheckId == e.Id));
                })
                .Run();

            foreach (var (id, severity) in expected)
            {
                var check = seen.Single(s => s.CustomCheckId == id);
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(check.Internal, Is.True, id);
                    Assert.That(check.Severity, Is.EqualTo(severity), id);
                }
            }
        }

        class Context : ScenarioContext;

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithFailingCustomCheck() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)));

            class FailingCustomCheck() : CustomCheck("MyCustomCheckId", "MyCategory", TimeSpan.FromSeconds(1))
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default) =>
                    Task.FromResult(CheckResult.Failed("Some reason"));
            }
        }
    }
}