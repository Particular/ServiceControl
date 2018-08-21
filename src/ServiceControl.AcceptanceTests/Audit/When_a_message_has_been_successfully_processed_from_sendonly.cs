namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_a_message_has_been_successfully_processed_from_sendonly : AcceptanceTest
    {
        [Test]
        public async Task Should_import_messages_from_sendonly_endpoint()
        {
            await Define<MyContext>(ctx => { ctx.MessageId = Guid.NewGuid().ToString(); })
                .WithEndpoint<SendOnlyEndpoint>()
                .Done(async c =>
                {
                    if (!await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId))
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

            class SendMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ProcessingEndpoint] = Conventions.EndpointNamingConvention(typeof(SendOnlyEndpoint))
                    };
                    return new TransportOperations(new TransportOperation(new OutgoingMessage(context.MessageId, headers, new byte[0]), new UnicastAddressTag("audit")));
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
        }
    }
}