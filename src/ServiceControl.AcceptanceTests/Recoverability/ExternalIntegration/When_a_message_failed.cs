namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    abstract class When_a_message_failed : AcceptanceTest
    {
        public class ErrorSender : EndpointConfigurationBuilder
        {
            static ErrorSender()
            {
                MessageId = "014b048-2b7b-4f94-8eda-d5be0fe50e93";
                FailedMessageId = Infrastructure.DeterministicGuid.MakeId(MessageId, nameof(ErrorSender));
            }

            public ErrorSender() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoDelayedRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });

            class SendFailedMessages : DispatchRawMessages<When_a_failed_message_is_archived.Context>
            {
                protected override TransportOperations CreateMessage(When_a_failed_message_is_archived.Context context)
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

            public static Guid FailedMessageId;

            static string MessageId;
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
                public Context Context { get; set; }

                public Task Handle(FailedMessagesArchived message, IMessageHandlerContext context)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message);
                    Context.Event = serializedMessage;
                    Context.EventDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}