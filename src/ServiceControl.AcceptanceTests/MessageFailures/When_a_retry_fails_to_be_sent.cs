﻿namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
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
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;

    public class When_a_retry_fails_to_be_sent : AcceptanceTest
    {
        [Test]
        public async Task SubsequentBatchesShouldBeProcessed()
        {
            FailedMessage decomissionedFailure = null, successfullyRetried = null;

            CustomConfiguration = config => { config.RegisterComponents(components => components.ConfigureComponent(b => new ReturnToSenderDequeuer(b.Build<IBodyStorage>(), new SendMessagesWrapper(b.Build<ISendMessages>(), b.Build<MyContext>()), b.Build<IDocumentStore>(), b.Build<IDomainEvents>(), b.Build<Configure>()), DependencyLifecycle.SingleInstance)); };

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.Given((bus, ctx) =>
                {
                    ctx.DecommissionedEndpointName = "DecommissionedEndpoint";
                    ctx.DecommissionedEndpointMessageId = Guid.NewGuid().ToString();
                    ctx.DecommissionedEndpointUniqueMessageId = DeterministicGuid.MakeId(ctx.DecommissionedEndpointMessageId, ctx.DecommissionedEndpointName).ToString();
                })
                    .When(async ctx =>
                    {
                        return !ctx.RetryForInvalidAddressIssued && await TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.DecommissionedEndpointUniqueMessageId);
                    },
                        async (bus, ctx) =>
                        {
                            await Post<object>($"/api/errors/{ctx.DecommissionedEndpointUniqueMessageId}/retry");
                            bus.SendLocal(new MessageThatWillFail());
                            ctx.RetryForInvalidAddressIssued = true;
                        })
                    .When(async ctx =>
                    {
                        return !ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued && await TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.MessageThatWillFailUniqueMessageId);
                    },
                        async (bus, ctx) =>
                        {
                            await Post<object>($"/api/errors/{ctx.MessageThatWillFailUniqueMessageId}/retry");
                            ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued = true;
                        }))
                .Done(async ctx =>
                {
                    if (!ctx.Done)
                    {
                        return false;
                    }

                    var decomissionedFailureResult = await TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.DecommissionedEndpointUniqueMessageId && m.Status == FailedMessageStatus.Unresolved);
                    decomissionedFailure = decomissionedFailureResult;
                    var successfullyRetriedResult = await TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.MessageThatWillFailUniqueMessageId && m.Status == FailedMessageStatus.Resolved);
                    successfullyRetried = successfullyRetriedResult;
                    return decomissionedFailureResult && successfullyRetriedResult;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.NotNull(decomissionedFailure);
            Assert.NotNull(successfullyRetried);
            Assert.AreEqual(FailedMessageStatus.Unresolved, decomissionedFailure.Status);
            Assert.AreEqual(FailedMessageStatus.Resolved, successfullyRetried.Status);
        }

        private class SendMessagesWrapper : ISendMessages
        {
            private readonly ISendMessages original;
            private readonly MyContext context;

            public SendMessagesWrapper(ISendMessages original, MyContext context)
            {
                this.original = original;
                this.context = context;
            }

            public void Send(TransportMessage message, SendOptions sendOptions)
            {
                if (sendOptions.Destination.Queue == context.DecommissionedEndpointName)
                {
                    throw new QueueNotFoundException();
                }

                original.Send(message, sendOptions);
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 0;
                    });
            }

            public class MessageThatWillFailHandler : IHandleMessages<MessageThatWillFail>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MessageThatWillFail message)
                {
                    Context.MessageThatWillFailUniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.LocalAddress().Queue).ToString();

                    if (!Context.RetryForMessageThatWillFailAndThenBeResolvedIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }

                    Context.Done = true;
                }
            }

            public class SendFailedMessage : IWantToRunWhenBusStartsAndStops
            {
                private readonly MyContext context;
                private readonly ISendMessages sendMessages;

                public SendFailedMessage(ISendMessages sendMessages, MyContext context)
                {
                    this.sendMessages = sendMessages;
                    this.context = context;
                }

                public void Start()
                {
                    var transportMessage = new TransportMessage(context.DecommissionedEndpointMessageId, new Dictionary<string, string>());
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty;
                    transportMessage.Headers["NServiceBus.FailedQ"] = context.DecommissionedEndpointName;
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";

                    sendMessages.Send(transportMessage, new SendOptions(Address.Parse("error")));
                }

                public void Stop()
                {
                }
            }
        }

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