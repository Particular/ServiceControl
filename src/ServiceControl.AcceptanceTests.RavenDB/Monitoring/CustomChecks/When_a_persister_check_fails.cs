namespace ServiceControl.AcceptanceTests.RavenDB.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Operations;
    using ServiceBus.Management.Infrastructure.Settings;
    using CustomCheckSeverity = global::ServiceControl.Contracts.CustomChecks.CustomCheckSeverity;
    using CustomCheckView = global::ServiceControl.Contracts.CustomChecks.CustomCheckView;
    using CheckStatus = global::ServiceControl.Persistence.Status;

    // Sibling of When_critical_storage_threshold_reached (see .plans/internal-customchecks.md §8.2): proves a
    // persister-implemented internal check that is forced to fail comes back classified through the API.
    // "ServiceControl database" cannot be forced to fail in this environment (the shared embedded server means
    // UseEmbeddedServer is false, so CheckFreeDiskSpace always passes) — see plan §8.6.
    [TestFixture]
    class When_a_persister_check_fails : AcceptanceTest
    {
        [SetUp]
        public void SetupIngestion() =>
            SetSettings = static s =>
            {
                s.DisableHealthChecks = false;
            };

        RavenPersisterSettings PersisterSettings => (RavenPersisterSettings)Settings.PersisterSpecificSettings;

        [Test]
        public async Task Forced_failure_is_classified_internal_and_degraded()
        {
            CustomCheckView ingestionCheck = null;

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context => context.Logs.ToArray().Any(i => i.Message.StartsWith(ErrorIngestion.LogMessages.StartedInfrastructure)),
                        (_, _) =>
                        {
                            PersisterSettings.MinimumStorageLeftRequiredForIngestion = 100;
                            PersisterSettings.DatabasePath = TestContext.CurrentContext.TestDirectory;
                            return Task.CompletedTask;
                        }))
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<CustomCheckView>("/api/customchecks", x => x.CustomCheckId == "Message Ingestion Process" && x.Status == CheckStatus.Fail);
                    ingestionCheck = result;
                    return result;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ingestionCheck, Is.Not.Null, "the forced storage-threshold failure never showed up");
                Assert.That(ingestionCheck.Internal, Is.True);
                Assert.That(ingestionCheck.Severity, Is.EqualTo(CustomCheckSeverity.Degraded));
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)));
        }
    }
}