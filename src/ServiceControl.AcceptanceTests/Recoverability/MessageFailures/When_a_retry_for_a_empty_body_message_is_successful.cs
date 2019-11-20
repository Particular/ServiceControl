namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_retry_for_a_empty_body_message_is_successful : AcceptanceTest
    {
        [Test]
        public async Task Should_show_up_as_resolved_when_doing_a_single_retry()
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>()
                .Done(async c =>
                {
                    var result = await GetFailedMessage(c);
                    failure = result;
                    if (!c.RetryIssued && result)
                    {
                        await IssueRetry(c, () => this.Post<object>($"/api/errors/{c.UniqueMessageId}/retry"));

                        return false;
                    }

                    var afterRetryResult = await GetFailedMessage(c, x => x.Status == FailedMessageStatus.Resolved);
                    failure = afterRetryResult;
                    return c.Done && afterRetryResult;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        async Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c, Predicate<FailedMessage> condition = null)
        {
            var result = await this.TryGet("/api/errors/" + c.UniqueMessageId, condition);
            if (string.IsNullOrEmpty(c.UniqueMessageId) || !result)
            {
                return SingleResult<FailedMessage>.Empty;
            }

            return result;
        }

        async Task IssueRetry(MyContext c, Func<Task> retryAction)
        {
            if (!c.RetryIssued)
            {
                c.RetryIssued = true;
                await retryAction().ConfigureAwait(false);
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoDelayedRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                    c.Pipeline.Register(cc => new LookForControlMessage(cc.Build<MyContext>()), "Look for control messages");
                });
            }

            public class SendControlMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    context.EndpointNameOfReceivingEndpoint = Conventions.EndpointNamingConvention(typeof(FailureEndpoint));
                    context.MessageId = Guid.NewGuid().ToString();
                    context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, context.EndpointNameOfReceivingEndpoint).ToString();

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.ProcessingEndpoint] = context.EndpointNameOfReceivingEndpoint,
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707",
                        ["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804",
                        ["$.diagnostics.hostdisplayname"] = "SELENE",
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.HelpLink"] = String.Empty,
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(FailureEndpoint)),
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                        ["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z",
                        [Headers.ControlMessageHeader] = Boolean.TrueString,
                        [Headers.ReplyToAddress] = Conventions.EndpointNamingConvention(typeof(FailureEndpoint))
                    };

                    var outgoingMessage = new OutgoingMessage(context.MessageId, headers, new byte[0]);

                    return new TransportOperations(
                        new TransportOperation(outgoingMessage, new UnicastAddressTag("error"))
                    );
                }
            }

            public class LookForControlMessage : Behavior<IIncomingPhysicalMessageContext>
            {
                public LookForControlMessage(MyContext context)
                {
                    myContext = context;
                }

                public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
                {
                    if (context.Message.Headers[Headers.MessageId] == myContext.MessageId)
                    {
                        myContext.Done = true;
                    }

                    return next();
                }

                readonly MyContext myContext;
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool RetryIssued { get; set; }

            public string UniqueMessageId { get; set; }

            public bool Done { get; set; }
        }
    }
}