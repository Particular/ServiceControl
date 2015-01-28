namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;

    public class When_a_message_has_been_successfully_processed : AcceptanceTest
    {
        [Test]
        public void Should_list_the_endpoint_in_the_list_of_known_endpoints()
        {
            var context = new MyContext();

            List<EndpointsView> knownEndpoints = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetMany("/api/endpoints", out knownEndpoints, m => m.Name == context.EndpointNameOfReceivingEndpoint))
                .Run(TimeSpan.FromSeconds(20));

            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, knownEndpoints.Single(e => e.Name == context.EndpointNameOfReceivingEndpoint).Name);
            Assert.AreEqual(Environment.MachineName, knownEndpoints.Single(e => e.Name == context.EndpointNameOfReceivingEndpoint).HostDisplayName);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class Foo : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public MyContext MyContext { get; set; }

                public void Start()
                {
                    //hack until we can fix the types filtering in default server
                    if (MyContext == null || string.IsNullOrEmpty(MyContext.MessageId))
                    {
                        return;
                    }

                    if (Configure.EndpointName != "Particular.ServiceControl")
                    {
                        return;
                    }

                    var transportMessage = new TransportMessage();
                    transportMessage.Headers[Headers.MessageId] = MyContext.MessageId;
                    transportMessage.Headers[Headers.ProcessingEndpoint] = Configure.EndpointName;
                    SendMessages.Send(transportMessage, Address.Parse("audit"));
                }

                public void Stop()
                {
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public string PropertyToSearchFor { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string EndpointNameOfSendingEndpoint { get; set; }

            public string PropertyToSearchFor { get; set; }
        }
    }
}