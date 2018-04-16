

namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures.Api;

    public class When_a_message_sent_from_third_party_endpoint_with_missing_metadata_failed : AcceptanceTest
    {
        [Test]
        public async Task Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            FailedMessageView failure = null;

            var context = new MyContext();

            await Define(context)
                .WithEndpoint<FailureEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == c.UniqueMessageId);
                    failure = result;
                    return result;
                })
                .Run();

            Assert.IsNotNull(failure);
            Assert.IsNull(failure.TimeSent);
        }

        [Test]
        public async Task TimeSent_should_not_be_casted()
        {
            FailedMessageView failure = null;

            var sentTime = DateTime.Parse("2014-11-11 02:26:58:000462 Z");
            var context = new MyContext
            {
                TimeSent = sentTime
            };

            await Define(context)
                .WithEndpoint<FailureEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return c.UniqueMessageId != null & result;
                })
                .Run();

            Assert.IsNotNull(failure);
            Assert.AreEqual(sentTime, failure.TimeSent);
        }

        [Test]
        public async Task Should_be_able_to_get_the_message_by_id()
        {
            FailedMessageView failure = null;

            var context = new MyContext();

            await Define(context)
                .WithEndpoint<FailureEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return c.UniqueMessageId != null & result;
                })
                .Run();

            Assert.IsNotNull(failure);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.DisableFeature<SecondLevelRetries>();
                });
            }

            public class SendFailedMessage : IWantToRunWhenBusStartsAndStops
            {
                readonly ISendMessages sendMessages;
                readonly MyContext context;
                readonly ReadOnlySettings settings;

                public SendFailedMessage(ISendMessages sendMessages, MyContext context, ReadOnlySettings settings)
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

                    var transportMessage = new TransportMessage(context.MessageId, new Dictionary<string, string>());
                    transportMessage.Headers[Headers.ProcessingEndpoint] = context.EndpointNameOfReceivingEndpoint;
                    transportMessage.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core";
                    transportMessage.Headers["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty;
                    transportMessage.Headers["NServiceBus.FailedQ"] = settings.LocalAddress().ToString();
                    transportMessage.Headers["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z";

                    if (context.TimeSent.HasValue)
                    {
                        transportMessage.Headers["NServiceBus.TimeSent"] = DateTimeExtensions.ToWireFormattedString(context.TimeSent.Value);
                    }

                    sendMessages.Send(transportMessage, new SendOptions("error"));
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

            public DateTime? TimeSent { get; set; }
        }
    }
}
