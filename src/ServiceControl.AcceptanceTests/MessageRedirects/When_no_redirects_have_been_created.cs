namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_no_redirects_have_been_created : AcceptanceTest
    {
        [Test]
        public async Task Listing_redirects_should_not_error()
        {
            Define<Context>();

            var result = await this.TryGetMany<MessageRedirectFromJson>("/api/redirects");
            List<MessageRedirectFromJson> response = result;

            Assert.AreEqual(0, response.Count, "Expected 0 redirects to be created");
        }

        public class Context : ScenarioContext
        { }
    }
}