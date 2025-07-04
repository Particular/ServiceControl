﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;
    using TestSupport;

    class When_a_retry_fails_to_be_sent : AcceptanceTest
    {
        [Test]
        public async Task SubsequentBatchesShouldBeProcessed()
        {
            FailedMessage decomissionedFailure = null, successfullyRetried = null;

            CustomizeHostBuilder = hostBuilder =>
            {
                hostBuilder.Services.AddSingleton<ReturnToSender>(provider => new FakeReturnToSender(provider.GetRequiredService<IErrorMessageDataStore>(), provider.GetRequiredService<MyContext>()));
            };

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.DoNotFailOnErrorMessages()
                    .When(async ctx => { return !ctx.RetryForInvalidAddressIssued && await this.TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == ctx.DecommissionedEndpointUniqueMessageId); },
                        async (bus, ctx) =>
                        {
                            await this.Post<object>($"/api/errors/{ctx.DecommissionedEndpointUniqueMessageId}/retry");
                            await bus.SendLocal(new MessageThatWillFail());
                            ctx.RetryForInvalidAddressIssued = true;
                        }).DoNotFailOnErrorMessages()
                    .When(async ctx => { return !ctx.RetryForMessageThatWillFailAndThenBeResolvedIssued && await this.TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == ctx.MessageThatWillFailUniqueMessageId); },
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

            Assert.Multiple(() =>
            {
                Assert.That(decomissionedFailure, Is.Not.Null);
                Assert.That(successfullyRetried, Is.Not.Null);
            });

            Assert.Multiple(() =>
            {
                Assert.That(decomissionedFailure.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
                Assert.That(successfullyRetried.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            });
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });

            public class MessageThatWillFailHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MessageThatWillFail>
            {
                public Task Handle(MessageThatWillFail message, IMessageHandlerContext context)
                {
                    scenarioContext.MessageThatWillFailUniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();

                    if (!scenarioContext.RetryForMessageThatWillFailAndThenBeResolvedIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }

                    scenarioContext.Done = true;
                    return Task.CompletedTask;
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


        public class MessageThatWillFail : ICommand;

        public class FakeReturnToSender(IErrorMessageDataStore errorMessageStore, MyContext myContext)
            : ReturnToSender(errorMessageStore, NullLogger<ReturnToSender>.Instance)
        {
            public override Task HandleMessage(MessageContext message, IMessageDispatcher sender, string errorQueueTransportAddress, CancellationToken cancellationToken = default)
            {
                if (message.Headers[Headers.MessageId] == myContext.DecommissionedEndpointMessageId)
                {
                    throw new Exception("This endpoint is unreachable");
                }

                return base.HandleMessage(message, sender, "error", cancellationToken);
            }
        }
    }
}