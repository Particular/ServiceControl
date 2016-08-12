namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EventLog;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using Microsoft.AspNet.SignalR.Client;
    using Microsoft.AspNet.SignalR.Client.Transports;

    public class When_a_message_has_failed : AcceptanceTest
    {
        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            FailedMessage failedMessage = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => c.MessageId != null && TryGet("/api/errors/" + c.UniqueMessageId, out failedMessage))
                .Run();

            Assert.AreEqual(context.UniqueMessageId, failedMessage.UniqueMessageId);
         
           // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, failedMessage.ProcessingAttempts.Last().MessageId,
                "The returned message should match the processed one");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failedMessage.Status, "Status should be set to unresolved");
            Assert.AreEqual(1, failedMessage.ProcessingAttempts.Count(), "Failed count should be 1");
            Assert.AreEqual("Simulated exception", failedMessage.ProcessingAttempts.Single().FailureDetails.Exception.Message,
                "Exception message should be captured");
        }

        [Test]
        public void Should_be_listed_in_the_error_list()
        {
            var context = new MyContext();

            FailedMessageView failure = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();

            // The message Ids may contain a \ if they are from older versions. 
            Assert.AreEqual(context.MessageId, failure.MessageId.Replace(@"\", "-"), "The returned message should match the processed one");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(1, failure.NumberOfProcessingAttempts, "One attempt should be stored");
        }

        [Test]
        public void Should_be_listed_in_the_messages_list()
        {
            var context = new MyContext();

            var failure = new MessagesView();

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetSingle("/api/messages", out failure,m=>m.MessageId == c.MessageId))
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(context.UniqueMessageId, failure.Id, "The unique id should be returned");
            
            Assert.AreEqual(MessageStatus.Failed, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failure.SendingEndpoint.Name);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failure.ReceivingEndpoint.Name);
        }

        [Test]
        public void Should_add_an_event_log_item()
        {
            var context = new MyContext();

            EventLogItem entry = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c => TryGetSingle("/api/eventlogitems/", out entry, e => e.RelatedTo.Any(r => r.Contains(c.UniqueMessageId)) && e.EventType == typeof(MessageFailed).Name))
                .Run();

            Assert.AreEqual(Severity.Error, entry.Severity, "Failures should be treated as errors");
            Assert.IsTrue(entry.Description.Contains("exception"), "For failed messages, the description should contain the exception information");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/message/" + context.UniqueMessageId), "Should contain the api url to retrieve additional details about the failed message");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/endpoint/" + context.EndpointNameOfReceivingEndpoint), "Should contain the api url to retrieve additional details about the endpoint where the message failed");
        }

        [Test]
        public void Should_raise_a_signalr_event()
        {
            var context = new MyContext
            {
                Handler = () => Handler
            };

            Define(context)
                .WithEndpoint<Receiver>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run();

            Assert.IsNotNull(context.SignalrData);
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }

            class SignalRStarter : IWantToRunWhenBusStartsAndStops
            {
                private readonly MyContext context;
                private readonly IBus bus;
                private Connection connection;

                public SignalRStarter(MyContext context, IBus bus)
                {
                    this.context = context;
                    this.bus = bus;
                    connection = new Connection("http://localhost/api/messagestream")
                    {
                        JsonSerializer = Newtonsoft.Json.JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault())
                    };
                }

                public void Start()
                {
                    connection.Received += ConnectionOnReceived;
                    connection.Start(new ServerSentEventsTransport(new SignalRHttpClient(context.Handler()))).GetAwaiter().GetResult();
                    
                    bus.Send(new MyMessage());
                }

                private void ConnectionOnReceived(string s)
                {
                    if (s.IndexOf("\"MessageFailuresUpdated\"") > 0)
                    {
                        context.SignalrData = s;
                        context.SignalrEventReceived = true;
                    }
                }

                public void Stop()
                {
                    connection.Stop();
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
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

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();

            public Func<HttpMessageHandler> Handler { get; set; }
            public bool SignalrEventReceived { get; set; }
            public string SignalrData { get; set; }
        }
    }
}