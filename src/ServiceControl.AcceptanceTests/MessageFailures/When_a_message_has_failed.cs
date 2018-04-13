namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
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
    using NServiceBus.Config;
    using ServiceBus.Management.Infrastructure.Settings;

    public class When_a_message_has_failed : AcceptanceTest
    {
        [Test]
        public async Task Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            FailedMessage failedMessage = null;

            await Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId);
                    failedMessage = result;
                    return c.MessageId != null && result;
                })
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
        public async Task Should_be_listed_in_the_error_list()
        {
            var context = new MyContext();

            FailedMessageView failure = null;

            await Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await TryGetSingle<FailedMessageView>("/api/errors", r => r.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run();

            // The message Ids may contain a \ if they are from older versions.
            Assert.AreEqual(context.MessageId, failure.MessageId.Replace(@"\", "-"), "The returned message should match the processed one");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(1, failure.NumberOfProcessingAttempts, "One attempt should be stored");
        }

        [Test]
        public async Task Should_be_listed_in_the_messages_list()
        {
            var context = new MyContext();

            var failure = new MessagesView();

            await Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await TryGetSingle<MessagesView>("/api/messages", m=>m.MessageId == c.MessageId);
                    failure = result;
                    return result;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(context.UniqueMessageId, failure.Id, "The unique id should be returned");

            Assert.AreEqual(MessageStatus.Failed, failure.Status, "Status of new messages should be failed");
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failure.SendingEndpoint.Name);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failure.ReceivingEndpoint.Name);
        }

        [Test]
        public async Task Should_add_an_event_log_item()
        {
            var context = new MyContext();

            EventLogItem entry = null;

            await Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.RelatedTo.Any(r => r.Contains(c.UniqueMessageId)) && e.EventType == typeof(MessageFailed).Name);
                    entry = result;
                    return result;
                })
                .Run();

            Assert.AreEqual(Severity.Error, entry.Severity, "Failures should be treated as errors");
            Assert.IsTrue(entry.Description.Contains("exception"), "For failed messages, the description should contain the exception information");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/message/" + context.UniqueMessageId), "Should contain the api url to retrieve additional details about the failed message");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/endpoint/" + context.EndpointNameOfReceivingEndpoint), "Should contain the api url to retrieve additional details about the endpoint where the message failed");
        }

        [Test]
        public async Task Should_raise_a_signalr_event()
        {
            var context = new MyContext
            {
                Handler = () => Handlers[Settings.DEFAULT_SERVICE_NAME]
            };

            await Define(context)
                .WithEndpoint<Receiver>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run();

            Assert.IsNotNull(context.SignalrData);
        }

        [Test]
        public async Task Should_be_able_to_search_queueaddresses()
        {
            var searchResults = new List<QueueAddress>();

            await Define<QueueSearchContext>()
                .WithEndpoint<FailingEndpoint>(b =>
                {
                    b.Given(bus => bus.SendLocal(new MyMessage())).When(async c =>
                    {
                        if (c.FailedMessageCount >= 1)
                        {
                            var result = await TryGetMany<QueueAddress>("/api/errors/queues/addresses?search=failing");
                            searchResults = result;
                            return result;
                        }

                        return false;
                    }, _ => { });
                })
                .Done(c => searchResults.Count == 1)
                .Run();

            Assert.AreEqual(1, searchResults.Count, "Result count did not match");
        }

        [Test]
        public async Task Should_only_return_queueaddresses_that_startswith_search()
        {
            var searchResults = new List<QueueAddress>();
            const string searchTerm = "another";
            const string searchEndpointName = "AnotherFailingEndpoint";

            await Define<QueueSearchContext>()
                .WithEndpoint<FailingEndpoint>(b =>
                {
                    b.Given(bus => bus.SendLocal(new MyMessage()));
                })
                .WithEndpoint<FailingEndpoint>(b =>
                {
                    b.CustomConfig(configuration => configuration.EndpointName(searchEndpointName));
                    b.Given(bus => bus.SendLocal(new MyMessage()));
                })
                .WithEndpoint<FailingEndpoint>(b =>
                {
                    b.CustomConfig(configuration => configuration.EndpointName("YetAnotherEndpoint"));
                    b.Given(bus => bus.SendLocal(new MyMessage()));
                }).Done(async c =>
                {
                    if (c.FailedMessageCount < 3)
                    {
                        return false;
                    }
                    var result = await TryGetMany<QueueAddress>($"/api/errors/queues/addresses/search/{searchTerm}");
                    searchResults = result;
                    return result;
                })
                .Run();


            Assert.AreEqual(1, searchResults.Count, "Result count did not match");
            Assert.IsTrue(searchResults[0].PhysicalAddress.StartsWith(searchEndpointName));
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
                    Context.LocalAddress = Settings.LocalAddress().ToString();
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 0;
                    });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public QueueSearchContext Context { get; set; }
                static object lockObj = new object();

                public void Handle(MyMessage message)
                {
                    lock (lockObj)
                    {
                        Context.FailedMessageCount++;
                    }
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

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, Address.Parse(LocalAddress).Queue).ToString();

            public Func<HttpMessageHandler> Handler { get; set; }
            public bool SignalrEventReceived { get; set; }
            public string SignalrData { get; set; }
            public string LocalAddress { get; set; }
        }

        public class QueueSearchContext : ScenarioContext
        {
            public int FailedMessageCount { get; set; }
        }
    }
}
