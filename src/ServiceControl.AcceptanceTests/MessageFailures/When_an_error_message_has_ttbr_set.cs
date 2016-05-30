namespace ServiceBus.Management.AcceptanceTests.Error
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;

    class When_an_error_message_has_ttbr_set : AcceptanceTest
    {
        [Test]
        public void Ttbr_is_stripped_before_being_forwarded_to_error_queue()
        {
            var context = new MyContext();

            Define(context)
                .WithEndpoint<ManagementEndpoint>(
                    c => c.AppConfig(PathToAppConfig)
                          .CustomConfig(config =>
                            {
                                Settings.ErrorLogQueue = Address.Parse("Error.LogPeekEndpoint");
                            })
                )
                .WithEndpoint<SourceEndpoint>()
                .WithEndpoint<LogPeekEndpoint>(
                    c => c.CustomConfig(config => config.RegisterComponents(components => components.ConfigureComponent<LogPeekEndpoint.MutateIncomingTransportMessages>(DependencyLifecycle.InstancePerCall)))
                )
                .Done(c => c.Done)
                .Run();

            Assert.IsTrue(context.ErrorForwardedToLog, "Failed message never made it to Error Log");
            Assert.IsTrue(context.TtbrStipped, "TTBR still set");
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }


            public class SendFailedMessageWithTtbrSet : IWantToRunWhenBusStartsAndStops
            {
                ISendMessages sendMessages;
                MyContext context;

                public SendFailedMessageWithTtbrSet(ISendMessages sendMessages, MyContext context)
                {
                    this.sendMessages = sendMessages;
                    this.context = context;
                }

                public void Start()
                {
                    var message = new TransportMessage();
                    context.MessageId = message.Id;
                    message.Headers[Headers.ProcessingEndpoint] = "Error.SourceEndpoint";
                    message.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName;
                    message.Headers["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened";
                    message.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    message.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    message.Headers["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty;
                    message.Headers["NServiceBus.FailedQ"] = "Error.SourceEndpoint";
                    message.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";

                    message.TimeToBeReceived = TimeSpan.Parse("10:00:00");

                    sendMessages.Send(message, new SendOptions("error"));
                }

                public void Stop()
                {
                }
            }
        }

        public class LogPeekEndpoint : EndpointConfigurationBuilder
        {
            public LogPeekEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            public class MutateIncomingTransportMessages : IMutateIncomingTransportMessages
            {
                readonly MyContext context;

                public MutateIncomingTransportMessages(MyContext context)
                {
                    this.context = context;
                }

                public void MutateIncoming(TransportMessage transportMessage)
                {
                    if (transportMessage.Id == Guid.Empty.ToString("N"))
                    {
                        // This is the forwarding smoke test
                        return;
                    }

                    if (transportMessage.Id == context.MessageId)
                    {
                        // MSMQ gives incoming messages a magic value so we can't compare against MaxValue
                        // Ensure that the TTBR given is greater than the 10:00:00 configured
                        context.TtbrStipped = transportMessage.TimeToBeReceived > TimeSpan.Parse("10:00:00");
                        context.ErrorForwardedToLog = true;
                    }
                    context.Done = true;
                }
            }
        }

        [Serializable]
        public class MyContext : ScenarioContext
        {
            public bool ErrorForwardedToLog { get; set; }
            public bool Done { get; set; }
            public bool TtbrStipped { get; set; }
            public string MessageId { get; set; }
        }
    }
}
