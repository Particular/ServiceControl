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
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_duplicated_messages_are_ingested : AcceptanceTest
    {
        [Test]
        public async Task Should_deduplicate()
        {
            var context = await Define<MyContext>()
                .WithEndpoint<AnEndpoint>(b => b.When(async s =>
                {
                    await s.SendLocal(new DuplicatedMessage());
                    await s.SendLocal(new TrailingMessage());
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/api/messages?per_page=10");
                    List<MessagesView> messages = result;
                    if (result)
                    {
                        // both message types need to be available to make sure there is no order dependency
                        if (!messages.Any(m => m.MessageType.Contains(nameof(DuplicatedMessage))) ||
                            !messages.Any(m => m.MessageType.Contains(nameof(TrailingMessage))))
                        {
                            return false;
                        }
                        c.Messages = messages;
                        return true;
                    }

                    return false;
                })
                .Run();

            var duplicatedMessageEntry = context.Messages.Count(m => m.MessageType.Contains(nameof(DuplicatedMessage)));

            Assert.AreEqual(1, duplicatedMessageEntry, "Duplicated audit message should be de-duplicated on ingestion.");
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.Pipeline.Register(typeof(DuplicatingBehavior), "Duplicates outgoing messages");
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }

            class DuplicatingBehavior : Behavior<IAuditContext>
            {
                public override Task Invoke(IAuditContext context, Func<Task> next)
                {
                    if (context.Message.Headers[Headers.EnclosedMessageTypes].Contains(nameof(DuplicatedMessage)))
                    {
                        // create a few duplicate audit messages
                        return Task.WhenAll(next(), next(), next(), next());
                    }
                    return next();
                }
            }

            class AMessageHandler : IHandleMessages<DuplicatedMessage>, IHandleMessages<TrailingMessage>
            {
                public Task Handle(DuplicatedMessage message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }

                public Task Handle(TrailingMessage message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public List<MessagesView> Messages { get; set; }
        }

        public class DuplicatedMessage : ICommand
        {
        }

        public class TrailingMessage : ICommand
        {
        }
    }
}