namespace ServiceControl.AcceptanceTests.Upgrade
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceControl.Upgrade;

    class When_upgrade_status_requested : AcceptanceTest
    {
        [Test]
        public async Task Should_return_false()
        {
            StaleIndexInfo indexInfo = null;

            await Define<MyContext>()
                .Done(async c =>
                {
                    var result = await this.TryGet<StaleIndexInfo>("/api/upgrade");
                    indexInfo = result;
                    return indexInfo != null;
                })
                .Run();

            Assert.AreEqual(false, indexInfo.InProgress);
            Assert.AreEqual(null, indexInfo.StartedAt);
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}