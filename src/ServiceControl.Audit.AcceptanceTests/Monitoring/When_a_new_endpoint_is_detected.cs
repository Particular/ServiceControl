namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Audit.Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_notify_service_control()
        {
            var context = await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c => c.SentRegisterEndpointCommands.Any())
                .Run();

            var command = context.SentRegisterEndpointCommands.Single();
            Assert.AreEqual(Conventions.EndpointNamingConvention(typeof(Receiver)), command.Endpoint.Name);
        }

        [Test]
        public async Task It_gets_persisted()
        {
            KnownEndpoint persistedEndpoint = null;
            IDictionary<string, object> persistedMetadata = null;

            await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (!c.SentRegisterEndpointCommands.Any())
                    {
                        return false;
                    }

                    var store = Database.PrepareDatabase("audit").GetAwaiter().GetResult();

                    using (var session = store.OpenSession())
                    {
                        var endpoint = session.Query<KnownEndpoint>().SingleOrDefault();
                        if (endpoint == null)
                        {
                            return false;
                        }
                        persistedEndpoint = endpoint;
                        persistedMetadata = session.Advanced.GetMetadataFor(endpoint)?.ToDictionary(x  => x.Key, x => x.Value);
                    }

                    return true;
                })
                .Run();

            Assert.IsNotNull(persistedEndpoint, "Persisted Endpoint was not loaded");
            Assert.IsNotNull(persistedMetadata, "Persisted Metadata was not loaded");
            Assert.IsTrue(persistedMetadata.ContainsKey(Raven.Client.Constants.Documents.Metadata.Expires), "Expiry should be set");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }
    }
}