namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    class When_a_redirect_is_removed : AcceptanceTest
    {
        [Test]
        public async Task Should_be_successfully_deleted()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var response = new List<MessageRedirectFromJson>();
            var messageRedirectId = DeterministicGuid.MakeId(redirect.fromphysicaladdress);

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/api/redirects", redirect);

                    await this.Delete($"/api/redirects/{messageRedirectId}/");

                    var result = await this.TryGetMany<MessageRedirectFromJson>("/api/redirects");
                    response = result;
                    return true;
                })
                .Run();

            Assert.AreEqual(0, response.Count, "Expected no redirects after delete");
        }

        class Context : ScenarioContext
        {
        }
    }
}