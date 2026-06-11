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
            Assert.That(knownEndpoints, Has.Count.EqualTo(1));
            var knownEndpoint = knownEndpoints.FirstOrDefault(x => x.EndpointDetails.Name == Conventions.EndpointNamingConvention(typeof(Sender)));
            Assert.That(knownEndpoint, Is.Not.Null);
        }

        internal class SomeMessage : IMessage;

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServerWithAudit>();

            [Handler]
            public class SomeMessageHandler(MyContext testContext) : IHandleMessages<SomeMessage>
            {
                public Task Handle(SomeMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageHandled = true;
                    return Task.CompletedTask;
                }
            }
        }

        internal class MyContext : ScenarioContext
        {
            public bool MessageHandled { get; set; }
        }
    }
}