namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;

    public class When_a_message_has_been_successfully_processed : AcceptanceTest
    {

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => AuditDataAvailable(context, c))
                .Run(TimeSpan.FromSeconds(40));

            Assert.NotNull(context.ReturnedMessage, "No message was returned by the management api");
            Assert.AreEqual(context.MessageId, context.ReturnedMessage.PhysicalMessage.MessageId,
                "The returned message should match the processed one");
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, context.ReturnedMessage.ReceivingEndpoint.Name,
                "Receiving endpoint name should be parsed correctly");
            Assert.AreEqual(Environment.MachineName, context.ReturnedMessage.ReceivingEndpoint.Machine,
                "Receiving machine should be parsed correctly");
            Assert.AreEqual(context.EndpointNameOfSendingEndpoint, context.ReturnedMessage.SendingEndpoint.Name,
                "Sending endpoint name should be parsed correctly");
            Assert.AreEqual(Environment.MachineName, context.ReturnedMessage.SendingEndpoint.Machine,
                "Sending machine should be parsed correctly");
            Assert.True(context.ReturnedMessage.Body.StartsWith("{\"Messages\":{"),
                "The body should be converted to json");
            //Assert.True(Encoding.UTF8.GetString(context.ReturnedMessage.BodyRaw).Contains("<MyMessage"),
            //"The raw body should be stored");
            Assert.AreEqual(typeof(MyMessage).FullName, context.ReturnedMessage.MessageProperties["MessageType"].Value,
                "AuditMessage type should be set to the fullname of the message type");
            Assert.False(((bool)context.ReturnedMessage.MessageProperties["IsSystemMessage"].Value), "AuditMessage should not be marked as a system message");
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

            public ProcessedMessage ReturnedMessage { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string EndpointNameOfSendingEndpoint { get; set; }
        }


        bool AuditDataAvailable(MyContext context, MyContext c)
        {
            lock (context)
            {
                if (c.ReturnedMessage != null)
                {
                    return true;
                }

                if (c.MessageId == null)
                {
                    return false;
                }

                c.ReturnedMessage = Get<ProcessedMessage>("/api/messages/" + context.MessageId + "-" +
                                 context.EndpointNameOfReceivingEndpoint);

                if (c.ReturnedMessage == null)
                {
                    return false;
                }

                return true;
            }
        }

     
    }
}