namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
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
            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                    {
                        await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1)
                            .ConfigureAwait(false);
                        await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2)
                            .ConfigureAwait(false);
                    })
                    .When(async ctx =>
                    {
                        if (ctx.ArchiveIssued || ctx.FirstMessageId == null || ctx.SecondMessageId == null)
                        {
                            return false;
                        }

                        var result = await this.TryGetMany<FailedMessage.FailureGroup>("/api/recoverability/groups/").ConfigureAwait(false);
                        List<FailedMessage.FailureGroup> beforeArchiveGroups = result;
                        if (!result)
                        {
                            return false;
                        }

                        foreach (var group in beforeArchiveGroups)
                        {
                            var failedMessagesResult = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{@group.Id}/errors").ConfigureAwait(false);
                            List<FailedMessage> failedMessages = failedMessagesResult;
                            if (failedMessagesResult)
                            {
                                if (failedMessages.Count == 2)
                                {
                                    ctx.GroupId = @group.Id;
                                    return true;
                                }
                            }
                        }
                        return false;

                    }, async (bus, ctx) =>
                    {
                        await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive");
                        ctx.ArchiveIssued = true;
                    })
                )
                .Done(async c =>
                {
                    if (c.FirstMessageId == null || c.SecondMessageId == null)
                        return false;

                    var firstFailureResult = await this.TryGet<FailedMessage>("/api/errors/" + c.FirstMessageId, e => e.Status == FailedMessageStatus.Archived);
                    firstFailure = firstFailureResult;
                    if (!firstFailureResult)
                    {
                        return false;
                    }

                    var secondFailureResult = await this.TryGet<FailedMessage>("/api/errors/" + c.SecondMessageId, e => e.Status == FailedMessageStatus.Archived);
                    secondFailure = secondFailureResult;
                    if (!secondFailureResult)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.AreEqual(FailedMessageStatus.Archived, firstFailure.Status, "First Message should be archived");
            Assert.AreEqual(FailedMessageStatus.Archived, secondFailure.Status, "Second Message should be archived");
        }

        [Test]
        public async Task Only_unresolved_issues_should_be_archived()
        {
            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;
            string failureGroupId = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1)
                        .ConfigureAwait(false);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2)
                        .ConfigureAwait(false);
                }))
                .Done(async c =>
                {
                    if (c.FirstMessageId == null || c.SecondMessageId == null)
                        return false;

                    if (!c.RetryIssued)
                    {
                        // Don't retry until the message has been added to a group
                        var result = await this.TryGetMany<FailedMessage.FailureGroup>("/api/recoverability/groups/");
                        List<FailedMessage.FailureGroup> beforeArchiveGroups = result;
                        if (!result)
                        {
                            return false;
                        }

                        failureGroupId = beforeArchiveGroups[0].Id;

                        var unresolvedSecondFailureResult = await this.TryGet<FailedMessage>("/api/errors/" + c.SecondMessageId, e => e.Status == FailedMessageStatus.Unresolved);
                        secondFailure = unresolvedSecondFailureResult;
                        if (!unresolvedSecondFailureResult)
                        {
                            return false;
                        }

                        c.RetryIssued = true;
                        await this.Post<object>($"/api/errors/{c.SecondMessageId}/retry");
                    }

                    if (!c.ArchiveIssued)
                    {
                        // Ensure message is being retried
                        var notUnresolvedSecondFailureResult = await this.TryGet<FailedMessage>("/api/errors/" + c.SecondMessageId, e => e.Status != FailedMessageStatus.Unresolved);
                        secondFailure = notUnresolvedSecondFailureResult;
                        if (!notUnresolvedSecondFailureResult)
                        {
                            return false;
                        }

                        await this.Post<object>($"/api/recoverability/groups/{failureGroupId}/errors/archive");
                        c.ArchiveIssued = true;
                    }

                    var firstFailureResult = await GetFailedMessage(c.FirstMessageId, e => e.Status == FailedMessageStatus.Archived);
                    firstFailure = firstFailureResult;
                    if (!firstFailureResult)
                    {
                        return false;
                    }

                    var secondFailureResult = await GetFailedMessage(c.SecondMessageId, e => e.Status == FailedMessageStatus.Resolved);
                    secondFailure = secondFailureResult;
                    return secondFailureResult;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Archived, firstFailure.Status, "Non retried message should be archived");
            Assert.AreNotEqual(FailedMessageStatus.Archived, secondFailure.Status, "Retried Message should not be set to Archived when group is archived");
        }

        async Task<SingleResult<FailedMessage>> GetFailedMessage(string messageId, Predicate<FailedMessage> condition = null)
        {
            var result = await this.TryGet("/api/errors/" + messageId, condition);
            if (string.IsNullOrEmpty(messageId) || !result)
            {
                return result;
            }

            FailedMessage failure = result;
            Console.WriteLine($"Message {messageId} status: {failure.Status}");

            return result;
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.Recoverability().Delayed(x => x.NumberOfRetries(0)));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress()).ToString();

                    if (message.MessageNumber == 1)
                    {
                        Context.FirstMessageId = uniqueMessageId;
                    }
                    else
                    {
                        Context.SecondMessageId = uniqueMessageId;
                    }

                    if (!Context.RetryIssued)
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public int MessageNumber { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string FirstMessageId { get; set; }
            public string SecondMessageId { get; set; }
            public bool ArchiveIssued { get; set; }
            public bool RetryIssued { get; set; }
            public string GroupId { get; set; }
        }
    }
}
