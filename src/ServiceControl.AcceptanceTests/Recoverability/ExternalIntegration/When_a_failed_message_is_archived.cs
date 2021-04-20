namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using ServiceBus.Management.Infrastructure.Settings;
    using NUnit.Framework;
    using Contracts;
    using ServiceControl.MessageFailures;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using Newtonsoft.Json;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_failed_message_is_archived : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                ctx.ExternalProcessorSubscribed = s.SubscriberReturnAddress.Contains("ExternalProcessor");
            });

            var uniqueMessageId = Infrastructure.DeterministicGuid.MakeId(ErrorSender.MessageId, nameof(ErrorSender)).ToString();

            var context = await Define<MyContext>()
                .WithEndpoint<ErrorSender>(b => b.When(session => Task.CompletedTask).DoNotFailOnErrorMessages())
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<FailedMessagesArchived>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilErrorsContainsFailedMessage", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{uniqueMessageId}",
                        e => e.Status == FailedMessageStatus.Unresolved);
                })
                .Do("Archive", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{uniqueMessageId}/archive");
                })
                .Do("EnsureMessageIsArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{uniqueMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<FailedMessagesArchived>(context.Event);
            CollectionAssert.Contains(deserializedEvent.FailedMessagesIds, uniqueMessageId);
        }

        public class ErrorSender : EndpointConfigurationBuilder
        {
            public ErrorSender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoDelayedRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });
            }

            class SendFailedMessages : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var errorAddress = new UnicastAddressTag("error");

                    return new TransportOperations(new TransportOperation(CreateTransportMessage(), errorAddress));
                }

                OutgoingMessage CreateTransportMessage()
                {
                    var date = new DateTime(2015, 9, 20, 0, 0, 0);
                    var msg = new OutgoingMessage(MessageId, new Dictionary<string, string>
                    {
                        {Headers.MessageId, MessageId},
                        {"NServiceBus.ExceptionInfo.ExceptionType", "System.Exception"},
                        {"NServiceBus.ExceptionInfo.Message", "An error occurred"},
                        {"NServiceBus.ExceptionInfo.Source", "NServiceBus.Core"},
                        {"NServiceBus.FailedQ", Conventions.EndpointNamingConvention(typeof(ErrorSender))},
                        {"NServiceBus.TimeOfFailure", "2014-11-11 02:26:58:000462 Z"},
                        {"NServiceBus.ProcessingEndpoint", nameof(ErrorSender)},
                        {Headers.TimeSent, DateTimeExtensions.ToWireFormattedString(date)},
                        {Headers.EnclosedMessageTypes, $"MessageThatWillFail"}
                    }, new byte[0]);
                    return msg;
                }
            }

            public static string MessageId = "014b048-2b7b-4f94-8eda-d5be0fe50e93";
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(FailedMessagesArchived).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<FailedMessagesArchived>(Settings.DEFAULT_SERVICE_NAME); });
            }

            public class FailureHandler : IHandleMessages<FailedMessagesArchived>
            {
                public MyContext Context { get; set; }

                public Task Handle(FailedMessagesArchived message, IMessageHandlerContext context)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message);
                    Context.Event = serializedMessage;
                    Context.EventDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}