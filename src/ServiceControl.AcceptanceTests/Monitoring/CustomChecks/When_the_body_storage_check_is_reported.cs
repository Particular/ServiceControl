namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using CustomCheckSeverity = global::ServiceControl.Contracts.CustomChecks.CustomCheckSeverity;
    using CustomCheckView = global::ServiceControl.Contracts.CustomChecks.CustomCheckView;

    // "ServiceControl body storage" is an EF Core persister check (SQL Server / PostgreSQL variants) that was
    // missing from the ServicePulse spike table. This proves goal 5 of .plans/internal-customchecks.md server-side:
    // a shipped check the spike table does not know about is still classified internal + degraded by the API.
    // Excluded from the RavenDB variant (bodies are stored in the database there, so the check does not exist) —
    // see the Compile Remove in ServiceControl.AcceptanceTests.RavenDB.csproj.
    [TestFixture]
    class When_the_body_storage_check_is_reported : AcceptanceTest
    {
        [SetUp]
        public void EnableInternalChecks() =>
            SetSettings = static s => s.DisableHealthChecks = false;

        [Test]
        public async Task Should_be_classified_internal_and_degraded()
        {
            CustomCheckView bodyStorageCheck = null;

            await Define<ScenarioContext>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<CustomCheckView>("/api/customchecks", x => x.CustomCheckId == "ServiceControl body storage");
                    bodyStorageCheck = result;
                    return result;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(bodyStorageCheck, Is.Not.Null, "the EF Core body storage check never reported");
                Assert.That(bodyStorageCheck.Internal, Is.True);
                Assert.That(bodyStorageCheck.Severity, Is.EqualTo(CustomCheckSeverity.Degraded));
            }
        }
    }
}