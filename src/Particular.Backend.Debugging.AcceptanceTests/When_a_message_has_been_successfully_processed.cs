namespace Particular.Backend.Debugging.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using global::ServiceControl.Contracts.Operations;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using Particular.Backend.Debugging.AcceptanceTests.Contexts;
    using Particular.Backend.Debugging.Api;

    public class When_a_message_has_been_successfully_processed : AcceptanceTest
    {
        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();
            MessagesView auditedMessage = null;
            byte[] body = null;

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
            //Assert.False(string.IsNullOrEmpty(auditedMessage.ReceivingEndpoint.Host), "Host display name should be set"); TODO

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

            Assert.True(bodyAsString.Contains("MyMessage"), bodyAsString);

            Assert.AreEqual(body.Length, auditedMessage.BodySize);

            Assert.True(auditedMessage.Headers.Any(_ => _.Key == Headers.MessageId));
        }

        [Test]
        public void Should_be_found_in_search_by_messageType()
        {
            var context = new MyContext();
            List<MessagesView> response;

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
        public void Should_be_found_in_search_by_messageId()
        {
            var context = new MyContext();
            List<MessagesView> response;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageId != null && TryGetMany("/api/messages/search/" + c.MessageId, out response))
                .Run(TimeSpan.FromSeconds(40));
        }


        [Test]
        public void Should_be_found_in_search_by_debug_session_id()
        {
            var context = new MyContext();
          

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    var queueCounter = new PerformanceCounter(
   "MSMQ Queue",
   "Messages in Queue",
   string.Format(@"{0}\private$\audit", Environment.MachineName));


                    c.MessagesAlreadyInAuditQueue =
                    queueCounter.NextValue();

                    var messsage = new MyMessage();

                    Headers.SetMessageHeader(messsage, "ServiceControl.DebugSessionId", "DANCO");
                    bus.Send(messsage);
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    var count = Get<List<MessagesView>>("/api/messages").Count;
                    Console.Out.WriteLine("Messages found:" + count);

                    if (count == 0)
                    {
                        return false;
                    }

                    List<MessagesView> response;
                    
                    return TryGetMany("/api/messages/search/DANCO", out response);
                })
                .Run(TimeSpan.FromSeconds(40));


            Console.WriteLine("Audit Queue contained {0} messages before test",
                     context.MessagesAlreadyInAuditQueue);
        }

        [Test]
        public void Should_be_found_in_search_by_messageId_for_the_specific_endpoint()
        {
            var context = new MyContext();
            List<MessagesView> response;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfSendingEndpoint = Configure.EndpointName;
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageId != null && TryGetMany(string.Format("/api/endpoints/{0}/messages/search/{1}", c.EndpointNameOfReceivingEndpoint, c.MessageId), out response))
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
        public void Should_import_messages_from_sendonly_endpoint()
        {
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString()
            };

            Scenario.Define(context)
               .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
               .WithEndpoint<SendOnlyEndpoint>()
               .Done(c =>
               {
                   MessagesView auditedMessage;
                   if (!TryGetSingle("/api/messages?include_system_messages=false&sort=id", out auditedMessage, m => m.MessageId == c.MessageId))
                   {
                       return false;
                   }
                   return true;
               })
               .Run(TimeSpan.FromSeconds(40));
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
            public float MessagesAlreadyInAuditQueue { get; set; }
        }
    }
}