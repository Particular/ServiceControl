namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport;

    class When_a_group_is_archived : AcceptanceTest
    {
        [Test]
        public async Task All_messages_in_group_should_get_archived()
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
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
                    var failedMessages = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{ctx.GroupId}/errors");
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
        public async Task All_archived_messages_should_be_grouped()
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
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
                    var failedMessages = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{ctx.GroupId}/errors");
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
                .Do("WaitUntilArchiveGroupContainsBothMessages", async ctx =>
                {
                    var failedMessages = await this.TryGetMany<ServiceControl.Recoverability.FailureGroupView>("/api/errors/groups/");
                    return failedMessages && failedMessages.Items.Count == 1 && failedMessages.Items[0].Count == 2;
                })
                .Done(ctx => true) //Done when sequence is finished
                .Run();
        }

        [Test]
        public async Task Archived_messages_group_info_should_be_accessible()
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
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
                    var failedMessages = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{ctx.GroupId}/errors");
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
                .Do("WaitUntilArchiveGroupContainsBothMessages", async ctx =>
                {
                    var failedMessages = await this.TryGet<ServiceControl.Recoverability.FailureGroupView>($"/api/archive/groups/id/{ctx.GroupId}");
                    return failedMessages && failedMessages.Item.Count == 2;
                })
                .Done(ctx => true) //Done when sequence is finished
                .Run();
        }

        [Test]
        [CancelAfter(120_000)]
        public async Task Only_unresolved_issues_should_be_archived(CancellationToken cancellationToken)
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
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
                .Do("Archive", async ctx => { await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive"); })
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
                .Run(cancellationToken);
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoDelayedRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });

            public class MyMessageHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();

                    if (message.MessageNumber == 1)
                    {
                        scenarioContext.FirstMessageId = uniqueMessageId;
                    }
                    else
                    {
                        scenarioContext.SecondMessageId = uniqueMessageId;
                    }

                    if (scenarioContext.FailProcessing)
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.CompletedTask;
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
        }
    }
}