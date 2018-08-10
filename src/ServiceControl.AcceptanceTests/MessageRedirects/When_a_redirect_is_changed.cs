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

            var context = await Define<Context>().Done(async c =>
            {
                await this.Post("/redirects", redirect);

                var result = await this.TryGetMany<MessageRedirectFromJson>("/redirects");

                c.CreatedAt = result.Items[0].last_modified;

                await this.Put($"/redirects/{messageRedirectId}/", new
                {
                    tophysicaladdress = newTo
                }, status => status != HttpStatusCode.NoContent);

                result = await this.TryGetMany<MessageRedirectFromJson>("/redirects");
                c.Response = result;
                return true;
            }).Run();

            var response = context.Response;
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

            await Define<Context>()
                .Done(async c =>
                {
                    await this.Post("/redirects", redirect);

                    await this.Put($"/redirects/{messageRedirectId}/", new
                    {
                        tophysicaladdress = string.Empty
                    }, status => status != HttpStatusCode.BadRequest);

                    return true;
                })
                .Run();
        }

        [Test]
        public async Task Should_return_not_found_if_it_does_not_exist()
        {
            const string newTo = "endpointC@machine3";

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Put($"/redirects/{Guid.Empty}/", new
                    {
                        tophysicaladdress = newTo
                    }, status => status != HttpStatusCode.NotFound);

                    return true;
                }).Run();
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

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/redirects", updateRedirect);

                    await this.Post("/redirects", conflictRedirect);

                    await this.Put($"/redirects/{messageRedirectId}/", new
                    {
                        tophysicaladdress = conflictRedirect.fromphysicaladdress
                    }, status => status != HttpStatusCode.Conflict);

                    return true;
                }).Run();
        }

        class Context : ScenarioContext
        {
            public List<MessageRedirectFromJson> Response { get; set; }
            public DateTime? CreatedAt { get; set; }
        }
    }
}