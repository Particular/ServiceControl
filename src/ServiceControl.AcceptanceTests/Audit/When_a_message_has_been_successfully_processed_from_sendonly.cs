namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_has_been_successfully_processed_from_sendonly: AcceptanceTest
    {
        [Test]
        public async Task Should_import_messages_from_sendonly_endpoint()
        {
            var context = new MyContext
            {
                MessageId = Guid.NewGuid().ToString()
            };

            await Define(context)
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(async c =>
                {
                    if (!await TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId))
                    {
                        return false;
                    }
                    return true;
                })
                .Run(TimeSpan.FromSeconds(40));
        }

        public class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SendMessage : IWantToRunWhenBusStartsAndStops
            {
                public ISendMessages SendMessages { get; set; }

                public MyContext MyContext { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Start()
                {
                    var transportMessage = new TransportMessage();
                    transportMessage.Headers[Headers.MessageId] = MyContext.MessageId;
                    transportMessage.Headers[Headers.ProcessingEndpoint] = Settings.EndpointName();
                    SendMessages.Send(transportMessage, new SendOptions(Address.Parse("audit")));
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

            public string EndpointNameOfSendingEndpoint { get; set; }

            public string PropertyToSearchFor { get; set; }
        }
    }
}