namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_message_processed_successfully_from_sendonly : AcceptanceTest
    {
        [Test]
        public async Task Should_import_messages_from_sendonly_endpoint()
        {
            await Define<MyContext>(ctx => { ctx.MessageId = Guid.NewGuid().ToString(); })
                .WithEndpoint<Sendonly>()
                .Done(async c =>
                {
                    if (!await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId))
                    {
                        return false;
                    }

                    return true;
                })
                .Run();
        }

        class Sendonly : EndpointConfigurationBuilder
        {
            public Sendonly()
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
                        [Headers.ProcessingEndpoint] = Conventions.EndpointNamingConvention(typeof(Sendonly))
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