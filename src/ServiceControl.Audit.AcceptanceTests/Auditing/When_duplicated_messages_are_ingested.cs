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
            SetSettings = settings =>
            {
                settings.MaximumConcurrencyLevel = 1;
            };
            var conversationId = Guid.NewGuid().ToString();

            var context = await Define<MyContext>()
                .WithEndpoint<AnEndpoint>(b => b.When(async s =>
                {
                    var options = new SendOptions();
                    options.StartNewConversation(conversationId);
                    options.RouteToThisEndpoint();
                    await s.Send(new DuplicatedMessage(), options);

                    options = new SendOptions();
                    options.StartNewConversation(conversationId);
                    options.RouteToThisEndpoint();
                    await s.Send(new TrailingMessage(), options);
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>($"/api/conversations/{conversationId}");
                    List<MessagesView> messages = result;
                    if (result)
                    {
                        var trailingMessageIngested = messages.Any(m => m.MessageType.Contains(typeof(TrailingMessage).Name));

                        if (trailingMessageIngested)
                        {
                            c.Messages = messages;
                            return true;
                        }
                    }

                    return false;
                })
                .Run();

            var duplicatedMessageEntry = context.Messages.Count(m => m.MessageType.Contains(typeof(DuplicatedMessage).Name));

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
                public override async Task Invoke(IAuditContext context, Func<Task> next)
                {
                    await next();
                    await next();
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