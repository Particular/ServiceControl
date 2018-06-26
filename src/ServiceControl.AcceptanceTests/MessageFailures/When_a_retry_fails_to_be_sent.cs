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
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability;

    public class When_a_retry_fails_to_be_sent : AcceptanceTest
    {
        [Test]
        public async Task SubsequentBatchesShouldBeProcessed()
        {
            FailedMessage decomissionedFailure = null, successfullyRetried = null;

            CustomConfiguration = config => config.RegisterComponents(components => components.ConfigureComponent<ReturnToSender>(b => new FakeReturnToSender(b.Build<IBodyStorage>(), b.Build<MyContext>()), DependencyLifecycle.SingleInstance));


            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.DoNotFailOnErrorMessages()
                    .When(async ctx =>
                    {
                        return !ctx.RetryForInvalidAddressIssued && await this.TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == ctx.DecommissionedEndpointUniqueMessageId);
                    },
                        async (bus, ctx) =>
                        {
                            await this.Post<object>($"/api/errors/{ctx.DecommissionedEndpointUniqueMessageId}/retry");
                            await bus.SendLocal(new MessageThatWillFail());
                            ctx.RetryForInvalidAddressIssued = true;
                        }).DoNotFailOnErrorMessages()
                    .When(async ctx =>
                    {
                        return !ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued && await this.TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == ctx.MessageThatWillFailUniqueMessageId);
                    },
                        async (bus, ctx) =>
                        {
                            await this.Post<object>($"/api/errors/{ctx.MessageThatWillFailUniqueMessageId}/retry");
                            ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued = true;
                        }).DoNotFailOnErrorMessages())
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
                    Context.MessageThatWillFailUniqueMessageId = DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();

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
                    context.DecommissionedEndpointName = "DecommissionedEndpointName";
                    context.DecommissionedEndpointMessageId = Guid.NewGuid().ToString();
                    context.DecommissionedEndpointUniqueMessageId = DeterministicGuid.MakeId(context.DecommissionedEndpointMessageId, context.DecommissionedEndpointName).ToString();

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = context.DecommissionedEndpointMessageId,
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                        [Headers.ProcessingEndpoint] = context.DecommissionedEndpointName,
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

        public class FakeReturnToSender : ReturnToSender
        {
            private MyContext myContext;

            public FakeReturnToSender(IBodyStorage bodyStorage, MyContext myContext) : base(bodyStorage)
            {
                this.myContext = myContext;
            }

            public override Task HandleMessage(MessageContext message, IDispatchMessages sender)
            {
                if (message.Headers[Headers.MessageId] == myContext.DecommissionedEndpointMessageId)
                {
                    throw new Exception("This endpoint is unreachable");
                }
                return base.HandleMessage(message, sender);
            }
        }
    }
}