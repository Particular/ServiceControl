namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using TestSupport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    abstract class ExternalIntegrationAcceptanceTest : AcceptanceTest
    {
        public class ErrorSender : EndpointConfigurationBuilder
        {
            public ErrorSender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoDelayedRetries();
                    //TODO: Get back to this to determine whether or not this duplication for simulating the production code is really needed
                    c.ReportSuccessfulRetriesToServiceControl();
                });

            public class AHandler : IHandleMessages<AMessage>
            {
                public Task Handle(AMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }

            class SendFailedMessages : DispatchRawMessages<Context>
            {
                protected override TransportOperations CreateMessage(Context context)
                {
                    var errorAddress = new UnicastAddressTag("error");

                    var messageId = Guid.NewGuid().ToString();
                    context.FailedMessageId = Infrastructure.DeterministicGuid.MakeId(messageId, nameof(ErrorSender));

                    return new TransportOperations(new TransportOperation(CreateTransportMessage(messageId), errorAddress));
                }

                OutgoingMessage CreateTransportMessage(string messageId)
                {
                    var date = new DateTime(2015, 9, 20, 0, 0, 0);
                    var msg = new OutgoingMessage(messageId, new Dictionary<string, string>
                    {
                        {Headers.MessageId, messageId},
                        {"NServiceBus.ExceptionInfo.ExceptionType", "System.Exception"},
                        {"NServiceBus.ExceptionInfo.Message", "An error occurred"},
                        {"NServiceBus.ExceptionInfo.Source", "NServiceBus.Core"},
                        {"NServiceBus.FailedQ", Conventions.EndpointNamingConvention(typeof(ErrorSender))},
                        {"NServiceBus.TimeOfFailure", "2014-11-11 02:26:58:000462 Z"},
                        {"NServiceBus.ProcessingEndpoint", nameof(ErrorSender)},
                        {Headers.TimeSent, DateTimeOffsetHelper.ToWireFormattedString(date)},
                        {Headers.EnclosedMessageTypes, typeof(AMessage).AssemblyQualifiedName}
                    }, Array.Empty<byte>());
                    return msg;
                }
            }
        }

        public class AMessage : ICommand;

        public class Context : ScenarioContext, ISequenceContext
        {
            public Guid FailedMessageId { get; set; }
            public string GroupId { get; set; }
            public int Step { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}