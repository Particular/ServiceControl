namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Monitoring;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_endpoint_known_to_audit_instance : AcceptanceTest
    {
        [Test]
        public async Task Should_appear_in_list_of_known_endpoints()
        {
            var knownEndpoints = new List<KnownEndpointsView>();

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(session => session.SendLocal(new SomeMessage())))
                .Done(async ctx =>
                {
                    if (!ctx.MessageHandled)
                    {
                        return false;
                    }

                    var result = await this.TryGetMany<KnownEndpointsView>("/api/endpoints/known");
                    knownEndpoints = result.Items;
                    return result.HasResult;
                })
                .Run();
            Assert.AreEqual(1, knownEndpoints.Count);
            var knownEndpoint = knownEndpoints.FirstOrDefault(x => x.EndpointDetails.Name == Conventions.EndpointNamingConvention(typeof(Sender)));
            Assert.IsNotNull(knownEndpoint);
        }

        class SomeMessage : IMessage
        {
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            class SomeMessageHandler : IHandleMessages<SomeMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(SomeMessage message, IMessageHandlerContext context)
                {
                    Context.MessageHandled = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public bool MessageHandled { get; set; }
        }
    }
}