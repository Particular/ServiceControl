namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    public class When_a_message_has_failed_from_send_only_endpoint : AcceptanceTest
    {
        [Test]
        public void Should_be_listed_in_the_error_list_when_processing_endpoint_header_is_not_present()
        {
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString(),
                IncludeProcessingEndpointHeader = false
            };
         
            FailedMessageView failure = null;
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.ReceivingEndpoint.Name.Contains("SomeEndpoint"), string.Format("The sending endpoint should be SomeEndpoint and not {0}", failure.ReceivingEndpoint.Name));
            
         }

        [Test]
        public void Should_be_listed_in_the_error_list_when_processing_endpoint_header_is_present()
        {
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString(),
                IncludeProcessingEndpointHeader = true
            };

            FailedMessageView failure = null;
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(c => TryGetSingle("/api/errors", out failure, r => r.MessageId == c.MessageId))
                .Run();
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure.ReceivingEndpoint.Name.Contains("SomeEndpoint"), string.Format("The sending endpoint should be SomeEndpoint and not {0}", failure.ReceivingEndpoint.Name));

        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class Foo : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public MyContext MyContext { get; set; }

                public void Start()
                {
                    // Transport message has no headers for Processing endpoint and the ReplyToAddress is set to null
                    var transportMessage = new TransportMessage();
                    if (MyContext.IncludeProcessingEndpointHeader)
                    {
                        transportMessage.Headers[Headers.ProcessingEndpoint] = "SomeEndpoint";
                    }
                    transportMessage.Headers[Headers.MessageId] = MyContext.MessageId;
                    transportMessage.Headers[Headers.ConversationId] = "a59395ee-ec80-41a2-a728-a3df012fc707";
                    transportMessage.Headers["$.diagnostics.hostid"] = "bdd4b0510bff5a6d07e91baa7e16a804";
                    transportMessage.Headers["$.diagnostics.hostdisplayname"] = "SELENE";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.HelpLink"] = "";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = "";
                    transportMessage.Headers["NServiceBus.FailedQ"] = "SomeEndpoint@SELENE";
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";
                    transportMessage.Headers["NServiceBus.TimeSent"] = "2014-11-11 02:26:01:174786 Z";
                    transportMessage.Headers[Headers.EnclosedMessageTypes] = "SendOnlyError.SendSomeCommand, TestSendOnlyError, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    transportMessage.ReplyToAddress = null;
                    SendMessages.Send(transportMessage, Address.Parse("error"));
                }

                public void Stop()
                {
                }
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public bool IncludeProcessingEndpointHeader { get; set; }
        }
    }
}