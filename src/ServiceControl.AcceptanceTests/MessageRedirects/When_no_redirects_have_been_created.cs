namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System.Collections.Generic;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_no_redirects_have_been_created : AcceptanceTest
    {
        [Test]
        public void Listing_redirects_should_not_error()
        {
            Define<Context>();

            List<MessageRedirectFromJson> response;

            TryGetMany("/api/redirects", out response);

            Assert.AreEqual(0, response.Count, "Expected 0 redirects to be created");
        }

        public class Context : ScenarioContext
        { }
    }
}