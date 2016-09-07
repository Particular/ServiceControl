namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    public class When_a_redirect_is_created : AcceptanceTest
    {
        [Test]
        public void Should_be_added_and_accessible_via_the_api()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            List<MessageRedirectFromJson> response;

            Define<Context>();

            Post("/api/redirects", redirect);

            TryGetMany("/api/redirects", out response);


            Assert.AreEqual(1, response.Count, "Expected 1 redirect to be created");
            Assert.AreEqual(DeterministicGuid.MakeId(redirect.fromphysicaladdress), response[0].message_redirect_id, "Message Redirect Id mismatch");
            Assert.AreEqual(redirect.fromphysicaladdress, response[0].from_physical_address, "From physical address mismatch");
            Assert.AreEqual(redirect.tophysicaladdress, response[0].to_physical_address, "To physical address mismatch");
            Assert.Greater(response[0].last_modified, DateTime.MinValue, "Last modified was not set");
        }

        [Test]
        public void Should_fail_validation_with_blank_fromphysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = string.Empty,
                tophysicaladdress = "endpointB@machine2"
            };

            Define<Context>();

            Post("/api/redirects", redirect, status => status != HttpStatusCode.BadRequest);
        }

        [Test]
        public void Should_fail_validation_with_blank_tophysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = string.Empty
            };

            Define<Context>();

            Post("/api/redirects", redirect, status => status != HttpStatusCode.BadRequest);
        }

        [Test]
        public void Should_fail_validation_with_different_tophysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            Define<Context>();

            Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);

            redirect.tophysicaladdress = "endpointC@machine3";

            Post("/api/redirects", redirect, status => status != HttpStatusCode.Conflict);
        }

        [Test]
        public void Should_ignore_exact_copies()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            List<MessageRedirectFromJson> response;

            Define<Context>();

            Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);

            Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);

            TryGetMany("/api/redirects", out response);

            Assert.AreEqual(1, response.Count, "Expected only 1 redirect to be created");
        }


        [Test]
        public void Should_fail_validation_with_dependent_redirects()
        {
            var toAddress = "endpointTo@machineTo";
            var dependentCount = 3;

            var context = new Context();

            Define(context);

            for (var i = 0; i < dependentCount; i++)
            {
                var redirect = new RedirectRequest
                {
                    fromphysicaladdress = $"endpoint{i}@machine{i}",
                    tophysicaladdress = toAddress
                };
                Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);
            }

            Post("/api/redirects", new RedirectRequest
            {
                fromphysicaladdress = toAddress,
                tophysicaladdress = "endpointX@machineX"
            }, status => status != HttpStatusCode.Conflict);
        }

        private class Context : ScenarioContext
        {
        }
    }
}
