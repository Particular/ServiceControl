namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTests;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Monitoring;


    class When_endpoint_detected_via_audits : AcceptanceTest
    {
        [Test]
        public async Task Should_be_configurable()
        {
            CustomAuditEndpointConfiguration = ConfigureWaitingForMasterToSubscribe;

            List<EndpointsView> response = null;

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(c => c.HasNativePubSubSupport || c.ServiceControlSubscribed,
                    (bus, c) => bus.SendLocal(new MyMessage())))
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

        void ConfigureWaitingForMasterToSubscribe(EndpointConfiguration config)
        {
            config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberEndpoint.IndexOf(ServiceControlInstanceName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.ServiceControlSubscribed = true;
                }
            });
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
            public bool ServiceControlSubscribed { get; set; }
        }
    }
}