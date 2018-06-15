
namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_sent_from_third_party_endpoint_with_missing_metadata : AcceptanceTest
    {
        [Test]
        public async Task Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            MessagesView auditedMessage = null;
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString()
            };

            await Define(context)
                .WithEndpoint<ThirdPartyEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);
                    auditedMessage = result;
                    return result;
                })
                .Run();

            Assert.IsNotNull(auditedMessage);
            Assert.IsNull(auditedMessage.TimeSent);
        }

        public class ThirdPartyEndpoint : EndpointConfigurationBuilder
        {
            public ThirdPartyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SendMessage : IWantToRunWhenBusStartsAndStops
            {
                readonly ISendMessages sendMessages;
                readonly MyContext context;
                readonly ReadOnlySettings settings;

                public SendMessage(ISendMessages sendMessages, MyContext context, ReadOnlySettings settings)
                {
                    this.sendMessages = sendMessages;
                    this.context = context;
                    this.settings = settings;
                }

                public void Start()
                {
                    var transportMessage = new TransportMessage(context.MessageId, new Dictionary<string, string> { { Headers.ProcessingEndpoint, settings.EndpointName() } });
                    sendMessages.Send(transportMessage, new SendOptions("audit"));
                }

                public void Stop()
                {
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
        }
    }
}

