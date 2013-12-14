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
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;
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

            var auditedMessage = response.SingleOrDefault();

            Assert.NotNull(auditedMessage, "No message was returned by the management api");
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, auditedMessage.ReceivingEndpointName,
                "Receiving endpoint name should be parsed correctly");
            Assert.AreEqual(typeof(MyMessage).FullName, auditedMessage.MessageType,
                "AuditMessage type should be set to the fullname of the message type");
            Assert.False(auditedMessage.IsSystemMessage, "AuditMessage should not be marked as a system message");

            Assert.Greater(DateTime.UtcNow,auditedMessage.TimeSent, "Time sent should be correctly set");

            Assert.Less(TimeSpan.Zero, auditedMessage.ProcessingTime, "Processing time should be calculated");
            Assert.Less(TimeSpan.Zero, auditedMessage.CriticalTime, "Critical time should be calculated");
            Assert.AreEqual(MessageIntentEnum.Send, auditedMessage.MessageIntent, "Message intent should be set");
        }


        [Test]
        public void Should_be_found_in_search_by_messagetype()
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
        public void Should_be_found_in_search_by_messageid()
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
                .Done(c => TryGetMany("/api/messages/search/" + c.MessageId, out response))
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public void Should_be_found_in_search_by_messagebody()
        {
            var context = new MyContext
            {
                PropertyToSearchFor = Guid.NewGuid().ToString()
            };

            var response = new List<MessagesView>();

             Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage
                    {
                        PropertyToSearchFor = c.PropertyToSearchFor
                    });
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetMany("/api/messages/search/" + c.PropertyToSearchFor, out response))
                .Run(TimeSpan.FromSeconds(40));
        }


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
                .Done(c => TryGetMany("/api/endpoints",out knownEndpoints))
                .Run(TimeSpan.FromSeconds(40));

            Assert.AreEqual(context.EndpointNameOfSendingEndpoint, knownEndpoints.Single(e=>e.Name == context.EndpointNameOfSendingEndpoint).Name);
            Assert.AreEqual(Environment.MachineName, knownEndpoints.Single(e => e.Name == context.EndpointNameOfSendingEndpoint).Machines.Single());
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, knownEndpoints.Single(e => e.Name == context.EndpointNameOfReceivingEndpoint).Name);
            Assert.AreEqual(Environment.MachineName, knownEndpoints.Single(e => e.Name == context.EndpointNameOfReceivingEndpoint).Machines.Single());
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