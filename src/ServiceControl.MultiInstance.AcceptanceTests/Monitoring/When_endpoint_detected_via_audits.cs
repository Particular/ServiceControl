namespace ServiceControl.MultiInstance.AcceptanceTests.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;
    using TestSupport;


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

            Assert.That(response.First(), Is.Not.Null);
            Assert.That(response.First().MonitorHeartbeat, Is.True);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServerWithAudit>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
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