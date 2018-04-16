namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.MessageFailures.Api;

    public class When_a_message_is_retried_with_a_replyTo_header : AcceptanceTest
    {
        // TODO: Add these transports back if/when then are updated to match this behavior
        [Test, IgnoreTransports("AzureServiceBus", "AzureStorageQueues", "RabbitMq")]
        public async Task The_header_should_not_be_changed()
        {
            var context = new ReplyToContext
            {
                ReplyToAddress = "ReplyToAddress@SOMEMACHINE"
            };

            await Define(context)
                .WithEndpoint<VerifyHeaderEndpoint>()
                .Done(async x =>
                {
                    if (!x.RetryIssued && await TryGetMany<FailedMessageView>("/api/errors"))
                    {
                        x.RetryIssued = true;
                        await Post<object>("/api/errors/retry/all");
                    }

                    return x.Done;
                })
                .Run();

            Assert.AreEqual(context.ReplyToAddress, context.ReceivedReplyToAddress);
        }

        class OriginalMessage : IMessage { }

        class ReplyToContext : ScenarioContext
        {
            public string ReplyToAddress { get; set; }
            public string ReceivedReplyToAddress { get; set; }
            public bool RetryIssued { get; set; }
            public bool Done { get; set; }
        }

        class VerifyHeaderEndpoint : EndpointConfigurationBuilder
        {
            public VerifyHeaderEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(
                    c => c.RegisterComponents(cc => cc.ConfigureComponent<VerifyHeaderIsUnchanged>(DependencyLifecycle.SingleInstance))
                );
            }

            public class FakeSender : IWantToRunWhenBusStartsAndStops
            {
                public void Start()
                {
                    var transportMessage = new TransportMessage(Guid.NewGuid().ToString(),
                        new Dictionary<string, string>
                        {
                            [Headers.ReplyToAddress] = context.ReplyToAddress,
                            [Headers.ProcessingEndpoint] = settings.EndpointName(),
                            ["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName,
                            ["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened",
                            ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                            ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                            ["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty,
                            ["NServiceBus.FailedQ"] = settings.LocalAddress().ToString(),
                            ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                            [Headers.EnclosedMessageTypes] = typeof(OriginalMessage).AssemblyQualifiedName,
                            [Headers.MessageIntent] = MessageIntentEnum.Send.ToString()
                        });

                    messageSender.Send(transportMessage, new SendOptions("error"));
                }

                public void Stop() { }

                private ISendMessages messageSender;
                private ReplyToContext context;
                private ReadOnlySettings settings;

                public FakeSender(ISendMessages messageSender, ReplyToContext context, ReadOnlySettings settings)
                {
                    this.messageSender = messageSender;
                    this.context = context;
                    this.settings = settings;
                }
            }

            class VerifyHeaderIsUnchanged : IMutateIncomingTransportMessages
            {
                public void MutateIncoming(TransportMessage transportMessage)
                {
                    string replyToAddress;
                    if (transportMessage.Headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
                    {
                        context.ReceivedReplyToAddress = replyToAddress;
                    }
                    context.Done = true;
                }

                private ReplyToContext context;

                public VerifyHeaderIsUnchanged(ReplyToContext context)
                {
                    this.context = context;
                }
            }
        }
    }
}