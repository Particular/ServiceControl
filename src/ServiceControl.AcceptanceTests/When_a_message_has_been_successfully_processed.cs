namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews;
    using ServiceControl.Contracts.Operations;

    public class When_a_message_has_been_successfully_processed : AcceptanceTest
    {

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();
            var response = new List<MessagesView>();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetMany("/api/messages", out response))
                .Run(TimeSpan.FromSeconds(40));

            var messageReturned = response.SingleOrDefault();

            Assert.NotNull(messageReturned, "No message was returned by the management api");
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, messageReturned.ReceivingEndpointName,
                "Receiving endpoint name should be parsed correctly");
            Assert.AreEqual(typeof(MyMessage).FullName, messageReturned.MessageType,
                "AuditMessage type should be set to the fullname of the message type");
            //Assert.False(((bool)context.ReturnedMessage.MessageMetadata["IsSystemMessage"].Value), "AuditMessage should not be marked as a system message");
        }


        [Test]
        public void Should_be_found_in_search()
        {
            var context = new MyContext();
            var response = new List<MessagesView>();

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetMany("/api/messages/search/" + searchString, out response))
                .Run(TimeSpan.FromSeconds(40));
        }



        [Test]
        public void Should_list_the_endpoint_in_the_list_of_known_endpoints()
        {
            var context = new MyContext();

            var knownEndpoints = new EndpointDetails[0];

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>
                {

                    knownEndpoints = Get<EndpointDetails[]>("/api/endpoints/");

                    var done = knownEndpoints != null &&
                               knownEndpoints.Any(e => e.Name == c.EndpointNameOfSendingEndpoint);

                    if (!done)
                    {
                        Thread.Sleep(5000);
                    }

                    return done;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.IsTrue(knownEndpoints.Any(e => e.Name == context.EndpointNameOfSendingEndpoint));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver));
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
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string EndpointNameOfSendingEndpoint { get; set; }
        }
    }
}