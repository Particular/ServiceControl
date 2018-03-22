namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using Particular.HealthMonitoring.Uptime;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Contracts.Operations;

    public class When_a_message_has_been_successfully_processed : AcceptanceTest
    {
        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            const string Payload = "PAYLOAD";
            var context = new MyContext();
            MessagesView auditedMessage = null;
            byte[] body = null;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage
                    {
                        PropertyToSearchFor = Payload
                    });
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }
                    if (!TryGetSingle("/api/messages?include_system_messages=false&sort=id", out auditedMessage, m => m.MessageId == c.MessageId))
                    {
                        return false;
                    }

                    body = DownloadData(auditedMessage.BodyUrl);

                    return true;

                })
                .Run(TimeSpan.FromSeconds(40));


            Assert.AreEqual(context.MessageId, auditedMessage.MessageId);
            Assert.AreEqual(MessageStatus.Successful, auditedMessage.Status);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, auditedMessage.ReceivingEndpoint.Name,
                "Receiving endpoint name should be parsed correctly");

            Assert.AreNotEqual(Guid.Empty, auditedMessage.ReceivingEndpoint.HostId, "Host id should be set");
            Assert.False(string.IsNullOrEmpty(auditedMessage.ReceivingEndpoint.Host), "Host display name should be set");

            Assert.AreEqual(typeof(MyMessage).FullName, auditedMessage.MessageType,
                "AuditMessage type should be set to the FullName of the message type");
            Assert.False(auditedMessage.IsSystemMessage, "AuditMessage should not be marked as a system message");

            Assert.NotNull(auditedMessage.ConversationId);

            Assert.AreNotEqual(DateTime.MinValue, auditedMessage.TimeSent, "Time sent should be correctly set");
            Assert.AreNotEqual(DateTime.MinValue, auditedMessage.ProcessedAt, "Processed At should be correctly set");

            Assert.Less(TimeSpan.Zero, auditedMessage.ProcessingTime, "Processing time should be calculated");
            Assert.Less(TimeSpan.Zero, auditedMessage.CriticalTime, "Critical time should be calculated");
            Assert.AreEqual(MessageIntentEnum.Send, auditedMessage.MessageIntent, "Message intent should be set");

            var bodyAsString = Encoding.UTF8.GetString(body);

            Assert.True(bodyAsString.Contains(Payload), bodyAsString);

            Assert.AreEqual(body.Length, auditedMessage.BodySize);

            Assert.True(auditedMessage.Headers.Any(_=>_.Key == Headers.MessageId));
        }

        [Test]
        public void Should_be_found_in_search_by_messageType()
        {
            var context = new MyContext();
            List<MessagesView> response;

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => TryGetMany("/api/messages/search/" + searchString, out response))
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public void Should_be_found_in_search_by_messageId()
        {
            var context = new MyContext();
            List<MessagesView> response;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>c.MessageId != null && TryGetMany("/api/messages/search/" + c.MessageId, out response))
                .Run(TimeSpan.FromSeconds(40));
        }


        [Test]
        public void Should_be_found_in_search_by_debug_session_id()
        {
            var context = new MyContext();
            List<MessagesView> response;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    var message = new MyMessage();

                    bus.SetMessageHeader(message, "ServiceControl.DebugSessionId", "DANCO-WIN8@Application1@2014-01-26T11:33:51");

                    bus.Send(message);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageId != null && TryGetMany("/api/messages/search/DANCO-WIN8@Application1@2014-01-26T11:33:51", out response))
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public void Should_be_found_in_search_by_messageId_for_the_specific_endpoint()
        {
            var context = new MyContext();
            List<MessagesView> response;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>c.MessageId != null && TryGetMany($"/api/endpoints/{c.EndpointNameOfReceivingEndpoint}/messages/search/{c.MessageId}", out response))
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public void Should_be_found_in_search_by_messageBody()
        {
            var context = new MyContext
            {
                PropertyToSearchFor = Guid.NewGuid().ToString()
            };

            List<MessagesView> response;

             Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
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

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
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

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = Bus.CurrentMessageContext.Id;

                    Thread.Sleep(200);
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

            public string PropertyToSearchFor { get; set; }
        }
    }
}