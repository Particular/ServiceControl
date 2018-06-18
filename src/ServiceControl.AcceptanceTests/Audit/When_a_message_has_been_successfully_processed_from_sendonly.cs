namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_has_been_successfully_processed_from_sendonly: AcceptanceTest
    {
        [Test]
        public async Task Should_import_messages_from_sendonly_endpoint()
        {
            await Define<MyContext>(ctx => { ctx.MessageId = Guid.NewGuid().ToString(); })
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

            class SendMessage : DispatchRawMessages
            {
                public MyContext MyContext { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Stop()
                {
                }

                protected override TransportOperations CreateMessage()
                {
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = MyContext.MessageId,
                        [Headers.ProcessingEndpoint] = Settings.EndpointName()
                    };
                    return new TransportOperations(new TransportOperation(new OutgoingMessage(MyContext.MessageId, headers, new byte[0]), new UnicastAddressTag("audit")));
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
        }
    }
}