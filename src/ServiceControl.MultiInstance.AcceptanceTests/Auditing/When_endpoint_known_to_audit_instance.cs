namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Persistence;
    using TestSupport;
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
            Assert.That(knownEndpoints.Count, Is.EqualTo(1));
            var knownEndpoint = knownEndpoints.FirstOrDefault(x => x.EndpointDetails.Name == Conventions.EndpointNamingConvention(typeof(Sender)));
            Assert.IsNotNull(knownEndpoint);
        }

        class SomeMessage : IMessage
        {
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServerWithAudit>();

            class SomeMessageHandler : IHandleMessages<SomeMessage>
            {
                readonly MyContext testContext;

                public SomeMessageHandler(MyContext testContext) => this.testContext = testContext;

                public Task Handle(SomeMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageHandled = true;
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