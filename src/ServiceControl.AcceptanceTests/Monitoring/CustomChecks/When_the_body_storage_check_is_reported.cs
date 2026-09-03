namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using CustomCheckView = global::ServiceControl.Contracts.CustomChecks.CustomCheckView;

    [TestFixture]
    class When_the_body_storage_check_is_reported : AcceptanceTest
    {
        [SetUp]
        public void EnableInternalChecks() =>
            SetSettings = static s => s.DisableHealthChecks = false;

        [Test]
        public async Task Should_be_classified_internal()
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
            }
        }
    }
}