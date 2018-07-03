namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_group_is_archived : AcceptanceTest
    {
        [Test]
        public async Task All_messages_in_group_should_get_archived()
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1)
                        .ConfigureAwait(false);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2)
                        .ConfigureAwait(false);
                }).DoNotFailOnErrorMessages())
                .Do("WaitUntilGrouped", async ctx =>
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
                .Done(ctx => true) //Done when sequence is finished
                .Run();
        }

        [Test]
        public async Task Only_unresolved_issues_should_be_archived()
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1)
                        .ConfigureAwait(false);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2)
                        .ConfigureAwait(false);
                }).DoNotFailOnErrorMessages())
                .Do("WaitUntilGrouped", async ctx =>
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
                .Do("DetectFirstFailure", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FirstMessageId}",
                        e => e.Status == FailedMessageStatus.Unresolved);
                })
                .Do("DetectSecondFailure", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.SecondMessageId}", 
                        e => e.Status == FailedMessageStatus.Unresolved);
                })
                .Do("RetrySecond", async ctx =>
                {
                    ctx.FailProcessing = false;
                    await this.Post<object>($"/api/errors/{ctx.SecondMessageId}/retry");
                })
                .Do("DetectSecondResolved", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.SecondMessageId}",
                        e => e.Status == FailedMessageStatus.Resolved);
                })
                .Do("Archive", async ctx =>
                {
                    await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive");
                })
                .Do("EnsureFirstArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FirstMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Do("EnsureSecondResolved", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.SecondMessageId}",
                        e => e.Status == FailedMessageStatus.Resolved);
                })
                .Done(ctx => true) //Done when sequence is finished
                .Run(TimeSpan.FromMinutes(2));
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.NoDelayedRetries());
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

        
        public class MyMessage : ICommand
        {
            public int MessageNumber { get; set; }
        }

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public string FirstMessageId { get; set; }
            public string SecondMessageId { get; set; }
            public string GroupId { get; set; }
            public int Step { get; set; }
            public bool FailProcessing { get; set; } = true;
        }
    }
}
