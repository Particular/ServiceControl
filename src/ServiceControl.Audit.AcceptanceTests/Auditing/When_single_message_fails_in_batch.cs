namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing;
    using Audit.Auditing.MessagesView;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_single_message_fails_in_batch : AcceptanceTest
    {
        [Test]
        public async Task Should_import_all_messages()
        {
            CustomizeHostBuilder = hostBuilder =>
                //Make sure the audit import attempt fails
                hostBuilder.Services.AddSingleton<IEnrichImportedAuditMessages, FailOnceEnricher>();

            var maximumConcurrencyLevel = 5;

            SetSettings = settings =>
            {
                settings.MaximumConcurrencyLevel = maximumConcurrencyLevel;
            };

            await Define<MyContext>(ctx =>
                {
                    ctx.MessageId = Guid.NewGuid().ToString();
                })
                .WithEndpoint<Sendonly>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/api/messages?include_system_messages=false&sort=id");
                    List<MessagesView> messages = result;
                    if (result)
                    {
                        return messages.Count == 9 && messages.Select(m => m.MessageId).OrderBy(t => t).SequenceEqual(c.MessageIds.OrderBy(t => t));
                    }

                    return false;
                })
                .Run();
        }

        class FailOnceEnricher(MyContext testContext) : IEnrichImportedAuditMessages
        {
            public void Enrich(AuditEnricherContext context)
            {
                if (context.Headers[Headers.MessageId] == testContext.MessageId && Interlocked.Increment(ref attempt) == 1)
                {
                    TestContext.Out.WriteLine("Simulating message processing failure");
                    throw new InvalidOperationException("ID", null);
                }

                TestContext.Out.WriteLine("Message processed correctly");
            }

            int attempt;
        }

        class Sendonly : EndpointConfigurationBuilder
        {
            public Sendonly() => EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<SendMessage>());

            class SendMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    // put the message that will fail somewhere in the middle of the first batch
                    var operations = new TransportOperation[9];
                    for (var i = 0; i < 9; i++)
                    {
                        var headers = new Dictionary<string, string>
                        {
                            [Headers.MessageId] = i == 2 ? context.MessageId : Guid.NewGuid().ToString(),
                            [Headers.ProcessingEndpoint] = Conventions.EndpointNamingConvention(typeof(Sendonly))
                        };
                        var messageId = headers[Headers.MessageId];
                        operations[i] = new TransportOperation(new OutgoingMessage(messageId, headers, Array.Empty<byte>()), new UnicastAddressTag("audit"));
                        context.MessageIds.Add(messageId);
                    }

                    return new TransportOperations(operations);
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public List<string> MessageIds { get; } = [];
        }
    }
}