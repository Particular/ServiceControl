﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_a_redirect_is_created : AcceptanceTest
    {
        [Test]
        public async Task Should_be_added_and_accessible_via_the_api()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var response = new List<MessageRedirectFromJson>();

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/api/redirects", redirect);

                    var result = await this.TryGetMany<MessageRedirectFromJson>("/api/redirects");
                    response = result;

                    return result;
                }).Run();

            Assert.That(response.Count, Is.EqualTo(1), "Expected 1 redirect to be created");
            Assert.Multiple(() =>
            {
                Assert.That(response[0].message_redirect_id, Is.EqualTo(DeterministicGuid.MakeId(redirect.fromphysicaladdress)), "Message Redirect Id mismatch");
                Assert.That(response[0].from_physical_address, Is.EqualTo(redirect.fromphysicaladdress), "From physical address mismatch");
                Assert.That(response[0].to_physical_address, Is.EqualTo(redirect.tophysicaladdress), "To physical address mismatch");
                Assert.That(response[0].last_modified, Is.GreaterThan(DateTime.MinValue), "Last modified was not set");
            });
        }

        [Test]
        public async Task Should_fail_validation_with_blank_fromphysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = string.Empty,
                tophysicaladdress = "endpointB@machine2"
            };

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.BadRequest);
                    return true;
                }).Run();
        }

        [Test]
        public async Task Should_fail_validation_with_blank_tophysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = string.Empty
            };

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.BadRequest);
                    return true;
                }).Run();
        }

        [Test]
        public async Task Should_fail_validation_with_different_tophysicaladdress()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);

                    redirect.tophysicaladdress = "endpointC@machine3";

                    await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.Conflict);
                    return true;
                }).Run();
        }

        [Test]
        public async Task Should_ignore_exact_copies()
        {
            var redirect = new RedirectRequest
            {
                fromphysicaladdress = "endpointA@machine1",
                tophysicaladdress = "endpointB@machine2"
            };

            var response = new List<MessageRedirectFromJson>();

            await Define<Context>()
                .Done(async ctx =>
                {
                    await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);

                    await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);

                    var result = await this.TryGetMany<MessageRedirectFromJson>("/api/redirects");
                    response = result;
                    return result;
                }).Run();

            Assert.That(response.Count, Is.EqualTo(1), "Expected only 1 redirect to be created");
        }


        [Test]
        public async Task Should_fail_validation_with_dependent_redirects()
        {
            var toAddress = "endpointTo@machineTo";
            var dependentCount = 3;

            await Define<Context>()
                .Done(async ctx =>
                {
                    for (var i = 0; i < dependentCount; i++)
                    {
                        var redirect = new RedirectRequest
                        {
                            fromphysicaladdress = $"endpoint{i}@machine{i}",
                            tophysicaladdress = toAddress
                        };
                        await this.Post("/api/redirects", redirect, status => status != HttpStatusCode.Created);
                    }

                    await this.Post("/api/redirects", new RedirectRequest
                    {
                        fromphysicaladdress = toAddress,
                        tophysicaladdress = "endpointX@machineX"
                    }, status => status != HttpStatusCode.Conflict);

                    return true;
                }).Run();
        }

        class Context : ScenarioContext
        {
        }
    }
}