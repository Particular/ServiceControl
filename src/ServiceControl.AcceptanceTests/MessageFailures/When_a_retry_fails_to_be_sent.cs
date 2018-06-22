namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_retry_fails_to_be_sent : AcceptanceTest
    {
        [Test]
        public async Task SubsequentBatchesShouldBeProcessed()
        {
            FailedMessage decomissionedFailure = null, successfullyRetried = null;

            // TODO: Figure out how to replicate a send failure on a retry
            //CustomConfiguration = config => { config.RegisterComponents(components => components.ConfigureComponent(b => new ReturnToSenderDequeuer(b.Build<IBodyStorage>(), new SendMessagesWrapper(b.Build<ISendMessages>(), b.Build<MyContext>()), b.Build<IDocumentStore>(), b.Build<IDomainEvents>(), b.Build<Configure>()), DependencyLifecycle.SingleInstance)); };

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When((bus, ctx) =>
                {
                    ctx.DecommissionedEndpointName = "DecommissionedEndpoint";
                    ctx.DecommissionedEndpointMessageId = Guid.NewGuid().ToString();
                    ctx.DecommissionedEndpointUniqueMessageId = DeterministicGuid.MakeId(ctx.DecommissionedEndpointMessageId, ctx.DecommissionedEndpointName).ToString();
                    return Task.FromResult(0);
                })
                    .When(async ctx =>
                    {
                        return !ctx.RetryForInvalidAddressIssued && await this.TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.DecommissionedEndpointUniqueMessageId);
                    },
                        async (bus, ctx) =>
                        {
                            await this.Post<object>($"/api/errors/{ctx.DecommissionedEndpointUniqueMessageId}/retry");
                            await bus.SendLocal(new MessageThatWillFail());
                            ctx.RetryForInvalidAddressIssued = true;
                        })
                    .When(async ctx =>
                    {
                        return !ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued && await this.TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.MessageThatWillFailUniqueMessageId);
                    },
                        async (bus, ctx) =>
                        {
                            await this.Post<object>($"/api/errors/{ctx.MessageThatWillFailUniqueMessageId}/retry");
                            ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued = true;
                        }))
                .Done(async ctx =>
                {
                    if (!ctx.Done)
                    {
                        return false;
                    }

                    var decomissionedFailureResult = await this.TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.DecommissionedEndpointUniqueMessageId && m.Status == FailedMessageStatus.Unresolved);
                    decomissionedFailure = decomissionedFailureResult;
                    var successfullyRetriedResult = await this.TryGetSingle<FailedMessage>("/api/errors/", m => m.Id == ctx.MessageThatWillFailUniqueMessageId && m.Status == FailedMessageStatus.Resolved);
                    successfullyRetried = successfullyRetriedResult;
                    return decomissionedFailureResult && successfullyRetriedResult;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.NotNull(decomissionedFailure);
            Assert.NotNull(successfullyRetried);
            Assert.AreEqual(FailedMessageStatus.Unresolved, decomissionedFailure.Status);
            Assert.AreEqual(FailedMessageStatus.Resolved, successfullyRetried.Status);
        }

        // TODO: Figure out how to replicate a send failure on retry
        //private class SendMessagesWrapper : ISendMessages
        //{
        //    private readonly ISendMessages original;
        //    private readonly MyContext context;

        //    public SendMessagesWrapper(ISendMessages original, MyContext context)
        //    {
        //        this.original = original;
        //        this.context = context;
        //    }

        //    public void Send(TransportMessage message, SendOptions sendOptions)
        //    {
        //        if (sendOptions.Destination.Queue == context.DecommissionedEndpointName)
        //        {
        //            throw new QueueNotFoundException();
        //        }

        //        original.Send(message, sendOptions);
        //    }
        //}

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                    {
                        c.NoRetries();
                    });
            }

            public class MessageThatWillFailHandler : IHandleMessages<MessageThatWillFail>
            {
                public MyContext Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MessageThatWillFail message, IMessageHandlerContext context)
                {
                    Context.MessageThatWillFailUniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), Settings.LocalAddress()).ToString();

                    if (!Context.RetryForMessageThatWillFailAndThenBeResolvedIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }

                    Context.Done = true;
                    return Task.FromResult(0);
                }
            }

            class SendFailedMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var headers = new Dictionary<string, string>
                    {
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                        ["NServiceBus.FailedQ"] = context.DecommissionedEndpointName,
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z"
                    };

                    var outgoingMessage = new OutgoingMessage(context.DecommissionedEndpointMessageId, headers, new byte[0]);

                    return new TransportOperations(
                        new TransportOperation(outgoingMessage, new UnicastAddressTag("error"))
                    );
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

        
        public class MessageThatWillFail : ICommand
        {
        }
    }
}