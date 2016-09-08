namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System.Collections.Generic;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    class When_a_redirect_is_removed : AcceptanceTest
    {
        [Test]
        public void Should_be_successfully_deleted()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var messageRedirectId = DeterministicGuid.MakeId(redirect.fromphysicaladdress);

            List<MessageRedirectFromJson> response;

            Define<Context>();

            Post("/api/redirects", redirect);

            Delete($"/api/redirects/{messageRedirectId}/");

            TryGetMany("/api/redirects", out response);

            Assert.AreEqual(0, response.Count, "Expected no redirects after delete");
        }

        class Context : ScenarioContext
        {
        }
    }
}