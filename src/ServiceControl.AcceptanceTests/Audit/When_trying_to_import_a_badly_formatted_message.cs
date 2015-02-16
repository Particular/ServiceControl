namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;
    using System.Xml.XPath;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Transports;
    using NUnit.Framework;

    [TestFixture]
    public class When_trying_to_import_a_badly_formatted_message : AcceptanceTest
    {
        const string spyQueueName = "audit.spy.default";

        [Test]
        public void Should_be_moved_to_the_service_control_error_queue()
        {
            var context = new MyContext()
            {
                Id = Guid.NewGuid()
            };
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<ServerEndpoint>()
                .WithEndpoint<Spy>()
                .Done(c => c.MessageDetected)
                .Run(TimeSpan.FromMinutes(2));

            Assert.IsTrue(context.MessageDetected);
        }

        protected override void CustomizeAppConfig(XDocument doc)
        {
            var appSettingsElement = doc.XPathSelectElement(@"/configuration/appSettings");
            var dbPathElement = appSettingsElement.XPathSelectElement(@"add[@key=""ServiceBus/AuditImportFailureQueue""]");
            if (dbPathElement != null)
            {
                dbPathElement.SetAttributeValue("value", spyQueueName);
            }
            else
            {
                appSettingsElement.Add(new XElement("add",
                    new XAttribute("key", "ServiceBus/AuditImportFailureQueue"), new XAttribute("value", spyQueueName)));
            }
        }

        public class Spy : EndpointConfigurationBuilder
        {
            public Spy()
            {
                EndpointSetup<DefaultServer>();
            }

            class BodySpy : IMutateIncomingTransportMessages, INeedInitialization
            {
                public MyContext Context { get; set; }

                public void MutateIncoming(TransportMessage transportMessage)
                {
                    string contextId;
                    if (transportMessage.Headers.TryGetValue("ContextId", out contextId)
                        && Guid.Parse(contextId) == Context.Id)
                    {
                        Context.MessageDetected = true;
                    }
                }

                public void Init()
                {
                    Configure.Component<BodySpy>(DependencyLifecycle.InstancePerCall);
                }
            }
        }

        public class ServerEndpoint : EndpointConfigurationBuilder
        {
            public ServerEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class Foo : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }
                public MyContext Context { get; set; }

                public void Start()
                {
                    //hack until we can fix the types filtering in default server
                    if (Configure.EndpointName != "Particular.ServiceControl")
                    {
                        return;
                    }

                    //Missing all required headers
                    var transportMessage = new TransportMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>()
                    {
                        {"ContextId", Context.Id.ToString()}
                    })
                    {
                        MessageIntent = MessageIntentEnum.Send,
                        Body = new byte[100],
                    };
                    SendMessages.Send(transportMessage, Address.Parse("audit"));
                }

                public void Stop()
                {
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public Guid Id { get; set; }
            public bool MessageDetected { get; set; }
        }
    }
}
