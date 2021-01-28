namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

    class When_an_endpoint_is_removed : AcceptanceTest
    {
        [Test]
        public async Task Should_signal_support_for_delete()
        {
            HttpResponseMessage response = null;

            await Define<Context>()
                .Done(async c =>
                {
                    response = await this.Options("/api/endpoints");
                    return true;
                })
                .Run();

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsTrue(response.Content.Headers.Allow.Contains("DELETE"));
        }

        [Test]
        public async Task Should_be_successfully_deleted()
        {
            var endpointsAfterDelete = new List<EndpointsView>();

            await Define<Context>()
                .WithEndpoint<StartingEndpoint>()
                .Done(async c =>
                {
                    var endpointAfterStartup = await this.TryGetSingle<EndpointsView>("/api/endpoints");
                    if (!endpointAfterStartup.HasResult)
                    {
                        return false;
                    }

                    await this.Delete($"/api/endpoints/{endpointAfterStartup.Item.Id}/");

                    endpointsAfterDelete = await this.TryGetMany<EndpointsView>("/api/endpoints");
                    return true;
                })
                .Run();

            Assert.AreEqual(0, endpointsAfterDelete.Count);
        }

        class Context : ScenarioContext
        {
        }

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.SendHeartbeatTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromHours(1)));
            }
        }
    }
}