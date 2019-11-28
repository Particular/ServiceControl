namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

    class When_message_processed_successfully_from_sendonly2 : AcceptanceTest
    {
        [Test]
        public async Task Should_import_messages_from_sendonly_endpoint()
        {
            await Define<MyContext>(ctx =>
                {
                    for (var i = 0; i < 20; i++)
                    {
                        ctx.MessageIds.Add(new Guid().ToString());
                    }
                })
                .WithEndpoint<Sendonly>()
                .Done(async c =>
                {
                    var tryGetMany = await this.TryGetMany<MessagesView>("/api/messages?include_system_messages=false&sort=id");
                    if (!tryGetMany && c.MessageIds.SequenceEqual(tryGetMany.Items.Select(x => x.MessageId)))
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
                    var operations= new List<TransportOperation>();
                    foreach (var messageId in context.MessageIds)
                    {
                        var headers = new Dictionary<string, string>
                        {
                            [Headers.MessageId] = messageId,
                            [Headers.ProcessingEndpoint] = Conventions.EndpointNamingConvention(typeof(Sendonly))
                        };
                        operations.Add(new TransportOperation(new OutgoingMessage(messageId, headers, new byte[0]), new UnicastAddressTag("audit")));
                    }
                    return new TransportOperations(operations.ToArray());
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public List<string> MessageIds { get;  } = new List<string>();
        }
    }
}