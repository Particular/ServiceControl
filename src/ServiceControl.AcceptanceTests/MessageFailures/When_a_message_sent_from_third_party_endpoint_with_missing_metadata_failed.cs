

namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures.Api;

    public class When_a_message_sent_from_third_party_endpoint_with_missing_metadata_failed : AcceptanceTest
    {
        [Test]
        public void Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            FailedMessageView failure = null;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given((bus, c) =>
                {
                    c.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    c.MessageId = Guid.NewGuid().ToString();
                    c.UniqueMessageId = DeterministicGuid.MakeId(c.MessageId, c.EndpointNameOfReceivingEndpoint).ToString();
                }))
                .Done(c => TryGetSingle("/api/errors/", out failure, m => m.Id == c.UniqueMessageId))
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsNotNull(failure);
            Assert.IsNull(failure.TimeSent);
        }


        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    Configure.Features.Disable<SecondLevelRetries>();
                })
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    })
                    .AuditTo(Address.Parse("audit"));
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
                    var transportMessage = new TransportMessage(context.MessageId, new Dictionary<string, string>());
                    transportMessage.Headers[Headers.ProcessingEndpoint] = context.EndpointNameOfReceivingEndpoint;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = "";
                    transportMessage.Headers["NServiceBus.FailedQ"] = Address.Local.ToString();
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";
                    
                    sendMessages.Send(transportMessage, Address.Parse("error"));
                }

                public void Stop()
                {

                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId { get; set; }
        }
    }
}
