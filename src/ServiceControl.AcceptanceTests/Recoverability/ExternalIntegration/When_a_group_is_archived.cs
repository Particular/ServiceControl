namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using ServiceBus.Management.Infrastructure.Settings;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.MessageFailures;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using Newtonsoft.Json;

    class When_a_group_is_archived : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.ExternalProcessorSubscribed = true;
                }
            });

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(c => c.ExternalProcessorSubscribed, async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1)
                        .ConfigureAwait(false);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2)
                        .ConfigureAwait(false);
                }).DoNotFailOnErrorMessages())
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<FailedMessagesArchived>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilGroupesContainsFaildMessages", async ctx =>
                {
                    if (ctx.FirstMessageId == null || ctx.SecondMessageId == null)
                    {
                        return false;
                    }

                    // Don't retry until the message has been added to a group
                    var groups = await this.TryGetMany<FailedMessage.FailureGroup>("/api/recoverability/groups/");
                    if (!groups)
                    {
                        return false;
                    }

                    ctx.GroupId = groups.Items[0].Id;
                    return true;
                })
                .Do("WaitUntilGroupContainsBothMessages", async ctx =>
                {
                    var failedMessages = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{ctx.GroupId}/errors").ConfigureAwait(false);
                    return failedMessages && failedMessages.Items.Count == 2;
                })
                .Do("Archive", async ctx => { await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive"); })
                .Do("EnsureFirstArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FirstMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Do("EnsureSecondArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.SecondMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<FailedMessagesArchived>(context.Event);
            Assert.IsNotNull(deserializedEvent.FailedMessagesIds);
            CollectionAssert.AreEquivalent(deserializedEvent.FailedMessagesIds, new[] { context.FirstMessageId, context.SecondMessageId });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoDelayedRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.EndpointName()).ToString();

                    if (message.MessageNumber == 1)
                    {
                        Context.FirstMessageId = uniqueMessageId;
                    }
                    else
                    {
                        Context.SecondMessageId = uniqueMessageId;
                    }

                    if (Context.FailProcessing)
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.FromResult(0);
                }
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(FailedMessagesArchived).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<FailedMessagesArchived>(Settings.DEFAULT_SERVICE_NAME); });
            }

            public class FailureHandler : IHandleMessages<FailedMessagesArchived>
            {
                public MyContext Context { get; set; }

                public Task Handle(FailedMessagesArchived message, IMessageHandlerContext context)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message);
                    Context.Event = serializedMessage;
                    Context.EventDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
            public int MessageNumber { get; set; }
        }

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public string FirstMessageId { get; set; }
            public string SecondMessageId { get; set; }
            public string GroupId { get; set; }
            public bool FailProcessing { get; set; } = true;
            public int Step { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}