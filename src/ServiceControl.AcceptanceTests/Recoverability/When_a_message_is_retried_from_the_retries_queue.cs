namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_message_is_retried_from_the_retries_queue : AcceptanceTest
    {
        private const string ExceptionMessage = "Simulated Exception";

        [Test]
        public void Should_successfully_retry()
        {
            var context = Define<Context>()
                .WithEndpoint<FailingEndpoint>()
                .Done(c =>
                {
                    if (!c.ErrorMessageSent)
                    {
                        return false;
                    }

                    FailedMessage failure;

                    if (c.RetryIssued)
                    {
                        Thread.Sleep(1000);

                        if (TryGet("/api/errors/" + c.UniqueMessageId, out failure) && failure.ProcessingAttempts.Count == 1)
                        {
                            Thread.Sleep(1000);
                        }

                        c.FailedMessage = failure;

                        return failure.ProcessingAttempts.Count > 1 || c.MessageProcessed;
                    }

                    if (!TryGet("/api/errors/" + c.UniqueMessageId, out failure))
                    {
                        Thread.Sleep(1000);
                        return false;
                    }

                    Post<object>($"/api/errors/{c.UniqueMessageId}/retry");
                    c.RetryIssued = true;

                    return false;
                })
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(1, context.FailedMessage.ProcessingAttempts.Count, "Message should only be sent to the error queue once");
            Assert.IsTrue(context.MessageProcessed, "Message never reached the handler");
        }

        class FailingMessage : ICommand {}

        class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public bool ErrorMessageSent { get; set; }
            public bool RetryIssued { get; set; }
            public FailedMessage FailedMessage { get; set; }
            public bool MessageProcessed { get; set; }
        }

        class FailingEndpoint : EndpointConfigurationBuilder
        {
            public Context TestContext { get; set; }

            public FailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(config =>
                {
                    config.SecondLevelRetries().CustomRetryPolicy(msg => TimeSpan.Zero);
                })
                .WithConfig<TransportConfig>(c =>
                {
                    c.MaxRetries = 1;
                });
            }

            class SendMessage : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public Context TestContext { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Start()
                {
                    var retriesQueue = Settings.LocalAddress().ToString().Replace("@", ".Retries@");

                    var messageId = Guid.NewGuid().ToString();

                    TestContext.UniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress().Queue + ".Retries").ToString();

                    var transportMessage = new TransportMessage();
                    transportMessage.Headers[Headers.MessageId] = messageId;
                    transportMessage.Headers["$.diagnostics.hostid"] = Settings.Get<Guid>("NServiceBus.HostInformation.HostId").ToString();
                    transportMessage.Headers["$.diagnostics.hostdisplayname"] = Settings.Get<string>("NServiceBus.HostInformation.DisplayName");
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = ExceptionMessage;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = null;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.HelpLink"] = String.Empty;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty;
                    transportMessage.Headers["NServiceBus.FailedQ"] = retriesQueue;
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";
                    transportMessage.Headers["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z";
                    transportMessage.Headers[Headers.EnclosedMessageTypes] = typeof(FailingMessage).AssemblyQualifiedName;

                    SendMessages.Send(transportMessage, new SendOptions(Address.Parse("error")));

                    TestContext.ErrorMessageSent = true;
                }

                public void Stop()
                {
                }
            }

            public class FailedMessageHandler : IHandleMessages<FailingMessage>
            {
                public Context TestContext { get; set; }

                //public ReadOnlySettings Settings { get; set; }
                public IBus Bus { get; set; }

                public void Handle(FailingMessage message)
                {
                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    Console.WriteLine($"Handling message with Id {messageId}");

                    TestContext.MessageProcessed = true;
                }
            }
        }
    }
}
