

namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Queuing;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    [Serializable]
    public class When_a_retry_fails_to_be_sent : AcceptanceTest
    {
        [Test]
        public void SubsequentBatchesShouldBeProcessed()
        {
            FailedMessage decomissionedFailure = null, successfullyRetried = null;

            Define<MyContext>()
                .WithEndpoint<ManagementEndpointEx>(ctx => ctx.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given((bus, ctx) =>
                {
                    ctx.DecommissionedEndpointName = "DecommissionedEndpoint";
                    ctx.DecommissionedEndpointMessageId = Guid.NewGuid().ToString();
                    ctx.DecommissionedEndpointUniqueMessageId = DeterministicGuid.MakeId(ctx.DecommissionedEndpointMessageId, ctx.DecommissionedEndpointName).ToString();
                })
                    .When(ctx =>
                    {
                        FailedMessage failure;
                        return !ctx.RetryForInvalidAddressIssued && TryGetSingle("/api/errors/", out failure, m => m.Id == ctx.DecommissionedEndpointUniqueMessageId);
                    },
                        (bus, ctx) =>
                        {
                            Post<object>($"/api/errors/{ctx.DecommissionedEndpointUniqueMessageId}/retry");
                            bus.SendLocal(new MessageThatWillFail());
                            ctx.RetryForInvalidAddressIssued = true;
                        })
                    .When(ctx =>
                    {
                        FailedMessage failure;
                        return !ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued && TryGetSingle("/api/errors/", out failure, m => m.Id == ctx.MessageThatWillFailUniqueMessageId);
                    },
                        (bus, ctx) =>
                        {
                            Post<object>($"/api/errors/{ctx.MessageThatWillFailUniqueMessageId}/retry");
                            ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued = true;
                        }))
                .Done(ctx =>
                {
                    if (!ctx.Done)
                    {
                        return false;
                    }

                    return TryGetSingle("/api/errors/", out decomissionedFailure, m => m.Id == ctx.DecommissionedEndpointUniqueMessageId && m.Status == FailedMessageStatus.Unresolved) &&
                           TryGetSingle("/api/errors/", out successfullyRetried, m => m.Id == ctx.MessageThatWillFailUniqueMessageId && m.Status == FailedMessageStatus.Resolved);
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.NotNull(decomissionedFailure);
            Assert.NotNull(successfullyRetried);
            Assert.AreEqual(FailedMessageStatus.Unresolved, decomissionedFailure.Status);
            Assert.AreEqual(FailedMessageStatus.Resolved, successfullyRetried.Status);
        }

        public class ManagementEndpointEx : EndpointConfigurationBuilder
        {
            public ManagementEndpointEx()
            {
                //Need to override the ISendMessages, because Azure does not throw exception when sending to a non existent queue :(
                EndpointSetup<ManagementEndpointSetup>(c => c.RegisterComponents(components => components.ConfigureComponent(b => new ReturnToSenderDequeuer(new SendMessagesWrapper(b.Build<ISendMessages>()), b.Build<IDocumentStore>(), b.Build<IBus>(), b.Build<Configure>()), DependencyLifecycle.SingleInstance)));
            }

            class SendMessagesWrapper : ISendMessages
            {
                readonly ISendMessages original;

                public SendMessagesWrapper(ISendMessages original)
                {
                    this.original = original;
                }

                public void Send(TransportMessage message, SendOptions sendOptions)
                {
                    if (sendOptions.Destination.Queue == "nonexistingqueue")
                    {
                        throw new QueueNotFoundException();
                    }

                    original.Send(message, sendOptions);
                }
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    })
                    .AuditTo(Address.Parse("audit"));
            }

            public class MessageThatWillFailHandler: IHandleMessages<MessageThatWillFail>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MessageThatWillFail message)
                {
                    Context.MessageThatWillFailUniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.EndpointName()).ToString();

                    if (!Context.RetryForMessageThatWillFailAndThenBeResolvedIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }

                    Context.Done = true;
                }
            }

            public class SendFailedMessage : IWantToRunWhenBusStartsAndStops
            {
                readonly ISendMessages sendMessages;
                readonly MyContext context;

                public SendFailedMessage(ISendMessages sendMessages, MyContext context)
                {
                    this.sendMessages = sendMessages;
                    this.context = context;
                }

                public void Start()
                {
                    var transportMessage = new TransportMessage(context.DecommissionedEndpointMessageId, new Dictionary<string, string>());
                    transportMessage.Headers[Headers.ProcessingEndpoint] = context.DecommissionedEndpointName;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty;
                    transportMessage.Headers["NServiceBus.FailedQ"] = "nonexistingqueue";
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";
                    
                    sendMessages.Send(transportMessage, new SendOptions(Address.Parse("error")));
                }

                public void Stop()
                {

                }
            }
        }

        [Serializable]
        public class MyContext : ScenarioContext
        {
            public string DecommissionedEndpointMessageId { get; set; }
            public string DecommissionedEndpointName { get; set; }
            public string DecommissionedEndpointUniqueMessageId { get; set; }
            public bool RetryForInvalidAddressIssued { get; set; }
            public bool RetryForMessageThatWillFailAndThenBeResolvedIssued { get; set; }
            public string MessageThatWillFailUniqueMessageId { get; set; }
            public bool Done { get; set; }
        }

        [Serializable]
        public class MessageThatWillFail : ICommand
        {
        }
    }
}
