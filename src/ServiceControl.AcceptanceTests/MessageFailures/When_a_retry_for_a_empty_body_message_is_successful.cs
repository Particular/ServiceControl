namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.MessageMutator;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_retry_for_a_empty_body_message_is_successful : AcceptanceTest
    {
        [Test]
        public void Should_show_up_as_resolved_when_doing_a_single_retry()
        {
            FailedMessage failure = null;

            var context = new MyContext();

            Define(context)
                .WithEndpoint<FailureEndpoint>()
                .Done(c =>
                {
                    if (!c.RetryIssued && GetFailedMessage(c, out failure))
                    {
                        IssueRetry(c, () => Post<object>($"/api/errors/{c.UniqueMessageId}/retry"));
                           
                        return false;
                    }

                    return c.Done && GetFailedMessage(c, out failure, x => x.Status == FailedMessageStatus.RetryIssued);
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.RetryIssued, failure.Status);
        }

        bool GetFailedMessage(MyContext c, out FailedMessage failure, Predicate<FailedMessage> condition = null)
        {
            failure = null;

            if (String.IsNullOrEmpty(c.UniqueMessageId) || !TryGet("/api/errors/" + c.UniqueMessageId, out failure, condition))
            {
                return false;
            }
            return true;
        }

        void IssueRetry(MyContext c, Action retryAction)
        {
            if (c.RetryIssued)
            {
                Thread.Sleep(1000);
            }
            else
            {
                c.RetryIssued = true;

                retryAction();
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.DisableFeature<SecondLevelRetries>();
                    c.RegisterComponents(cc => cc.ConfigureComponent<LookForControlMessage>(DependencyLifecycle.SingleInstance));
                });
            }

            public class SendControlMessage : IWantToRunWhenBusStartsAndStops
            {
                readonly ISendMessages sendMessages;
                readonly MyContext context;
                readonly ReadOnlySettings settings;

                public SendControlMessage(ISendMessages sendMessages, MyContext context, ReadOnlySettings settings)
                {
                    this.sendMessages = sendMessages;
                    this.context = context;
                    this.settings = settings;
                }

                public void Start()
                {
                    context.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    context.MessageId = Guid.NewGuid().ToString();
                    context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, context.EndpointNameOfReceivingEndpoint).ToString();

                    var transportMessage = new TransportMessage();
                    transportMessage.Headers[Headers.ProcessingEndpoint] = context.EndpointNameOfReceivingEndpoint;
                    transportMessage.Headers[Headers.MessageId] = context.MessageId;
                    transportMessage.Headers[Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707";
                    transportMessage.Headers["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804";
                    transportMessage.Headers["$.diagnostics.hostdisplayname"] = "SELENE";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.HelpLink"] = String.Empty;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty;
                    transportMessage.Headers["NServiceBus.FailedQ"] = settings.LocalAddress().ToString();
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";
                    transportMessage.Headers["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z";
                    transportMessage.Headers[Headers.ControlMessageHeader] = Boolean.TrueString;
                    transportMessage.Headers[Headers.ReplyToAddress] = settings.LocalAddress().ToString();

                    sendMessages.Send(transportMessage, new SendOptions(Address.Parse("error")));
                }

                public void Stop()
                {
                    
                }
            }

            public class LookForControlMessage : IMutateIncomingTransportMessages
            {
                readonly IBus bus;
                readonly MyContext context;

                public LookForControlMessage(IBus bus, MyContext context)
                {
                    this.bus = bus;
                    this.context = context;
                }

                public void MutateIncoming(TransportMessage transportMessage)
                {
                    if (transportMessage.Id == context.MessageId)
                    {
                        bus.DoNotContinueDispatchingCurrentMessageToHandlers();
                        context.Done = true;
                    }
                }
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