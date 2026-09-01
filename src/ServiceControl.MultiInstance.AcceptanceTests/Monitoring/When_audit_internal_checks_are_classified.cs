namespace ServiceControl.MultiInstance.AcceptanceTests.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport;
    using CustomCheckSeverity = global::ServiceControl.Contracts.CustomChecks.CustomCheckSeverity;
    using CustomCheckView = global::ServiceControl.Contracts.CustomChecks.CustomCheckView;

    // Primary + audit instances. The audit forwards its checks to the primary as
    // ReportCustomCheckResult messages, so this is the test that proves the primary's
    // hard-coded audit IDs (string literals — the audit assembly is not referenced) stay correct.
    [TestFixture]
    class When_audit_internal_checks_are_classified : AcceptanceTest
    {
        [Test]
        public async Task Audit_checks_arriving_as_messages_are_flagged_internal_and_degraded()
        {
            var expectedIds = new[]
            {
                "Audit Message Ingestion Process",
                "Audit Message Ingestion",
            };

            var seen = new List<CustomCheckView>();

            await Define<Context>()
                .Done(async c =>
                {
                    var checks = await this.TryGetMany<CustomCheckView>("/api/customchecks", instanceName: ServiceControlInstanceName);
                    foreach (var item in checks.Items)
                    {
                        // The Done predicate polls, so keep one row per check id
                        if (seen.All(s => s.Id != item.Id))
                        {
                            seen.Add(item);
                        }
                    }

                    return expectedIds.All(id => seen.Any(s => s.CustomCheckId == id));
                })
                .Run();

            foreach (var id in expectedIds)
            {
                var check = seen.Single(s => s.CustomCheckId == id);
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(check.Internal, Is.True, id);
                    Assert.That(check.Severity, Is.EqualTo(CustomCheckSeverity.Degraded), id);
                }
            }
        }

        class Context : ScenarioContext;
    }
}