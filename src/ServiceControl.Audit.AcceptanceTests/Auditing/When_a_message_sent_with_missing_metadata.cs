﻿namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_sent_with_missing_metadata : AcceptanceTest
    {
        [Test]
        public async Task Should_not_be_cast_TimeSent_to_DateTimeMin()
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

            Assert.That(auditedMessage, Is.Not.Null);
            Assert.That(auditedMessage.TimeSent, Is.Null);
        }

        public class ThirdPartyEndpoint : EndpointConfigurationBuilder
        {
            public ThirdPartyEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();

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