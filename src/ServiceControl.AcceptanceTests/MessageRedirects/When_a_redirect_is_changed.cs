namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    public class When_a_redirect_is_changed : AcceptanceTest
    {
        [Test]
        public async Task Should_be_successfully_updated()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var messageRedirectId = DeterministicGuid.MakeId(redirect.fromphysicaladdress);

            const string newTo = "endpointC@machine3";

            var context = new Context();

            Define(context);

            await Post("/api/redirects", redirect);

            var result = await TryGetMany<MessageRedirectFromJson>("/api/redirects");
            List<MessageRedirectFromJson> response = result;

            context.CreatedAt = response[0].last_modified;

            await Put($"/api/redirects/{messageRedirectId}/", new
            {
                tophysicaladdress = newTo
            }, status => status != HttpStatusCode.NoContent);

            result = await TryGetMany<MessageRedirectFromJson>("/api/redirects");
            response = result;

            Assert.AreEqual(1, response.Count, "Expected only 1 redirect");
            Assert.AreEqual(messageRedirectId, response[0].message_redirect_id, "Message Redirect Id mismatch");
            Assert.AreEqual(redirect.fromphysicaladdress, response[0].from_physical_address, "From physical address mismatch");
            Assert.AreEqual(newTo, response[0].to_physical_address, "To physical address mismatch");
            Assert.Greater(response[0].last_modified, context.CreatedAt, "Last modified was not updated");
        }

        [Test]
        public async Task Should_fail_validation_with_blank_tophysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var messageRedirectId = DeterministicGuid.MakeId(redirect.fromphysicaladdress.ToLowerInvariant());

            Define<Context>();

            await Post("/api/redirects", redirect);

            await Put($"/api/redirects/{messageRedirectId}/", new
            {
                tophysicaladdress = string.Empty
            }, status => status != HttpStatusCode.BadRequest);
        }

        [Test]
        public async Task Should_return_not_found_if_it_does_not_exist()
        {
            const string newTo = "endpointC@machine3";

            Define<Context>();

            await Put($"/api/redirects/{Guid.Empty}/", new
            {
                tophysicaladdress = newTo
            }, status => status != HttpStatusCode.NotFound);
        }

        [Test]
        public async Task Should_return_conflict_when_it_will_create_a_dependency()
        {
            var updateRedirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var conflictRedirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointC@machine3",
                tophysicaladdress = "endpointD@machine4"
            };

            var messageRedirectId = DeterministicGuid.MakeId(updateRedirect.fromphysicaladdress);

            Define<Context>();

            await Post("/api/redirects", updateRedirect);

            await Post("/api/redirects", conflictRedirect);

            await Put($"/api/redirects/{messageRedirectId}/", new
            {
                tophysicaladdress = conflictRedirect.fromphysicaladdress
            }, status => status != HttpStatusCode.Conflict);
        }

        class Context : ScenarioContext
        {
            public DateTime? CreatedAt { get; set; }
        }
    }
}