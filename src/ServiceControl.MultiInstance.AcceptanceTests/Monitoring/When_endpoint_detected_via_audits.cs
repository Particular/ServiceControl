namespace ServiceControl.MultiInstance.AcceptanceTests.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Monitoring;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class When_endpoint_detected_via_audits : AcceptanceTest
    {
        [Test]
        public async Task Should_be_configurable()
        {
            List<EndpointsView> response = null;

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(session => session.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<EndpointsView>("/api/endpoints/", instanceName: ServiceControlInstanceName);
                    response = result;
                    if (result && response.Count > 0)
                    {
                        c.EndpointKnownOnMaster = true;
                    }

                    if (c.EndpointKnownOnMaster)
                    {
                        var endpointId = response.First().Id;

                        await this.Patch($"/api/endpoints/{endpointId}", new EndpointUpdateModel
                        {
                            MonitorHeartbeat = true
                        }, ServiceControlInstanceName);

                        var resultAfterPath = await this.TryGetMany<EndpointsView>("/api/endpoints/", instanceName: ServiceControlInstanceName);
                        response = resultAfterPath;
                        return resultAfterPath;
                    }

                    return false;
                })
                .Run();

            Assert.IsNotNull(response.First());
            Assert.IsTrue(response.First().MonitorHeartbeat);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool EndpointKnownOnMaster { get; set; }
        }
    }
}