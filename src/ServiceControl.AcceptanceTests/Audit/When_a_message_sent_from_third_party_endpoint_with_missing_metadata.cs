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

    public class When_a_message_sent_from_third_party_endpoint_with_missing_metadata : AcceptanceTest
    {
        [Test]
        public async Task Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            MessagesView auditedMessage = null;

            await Define<MyContext>(ctx => { ctx.MessageId = Guid.NewGuid().ToString(); })
                .WithEndpoint<ThirdPartyEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);
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

            class SendMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var headers = new Dictionary<string, string>
                    {
                        {Headers.ProcessingEndpoint, Conventions.EndpointNamingConvention(typeof(ThirdPartyEndpoint))},
                        {Headers.MessageId, context.MessageId}
                    };
                    return new TransportOperations(new TransportOperation(new OutgoingMessage(context.MessageId, headers, new byte[0]), new UnicastAddressTag("audit")));
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
        }
    }
}