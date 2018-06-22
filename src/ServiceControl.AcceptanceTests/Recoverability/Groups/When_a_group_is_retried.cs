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

    public class When_a_group_is_retried : AcceptanceTest
    {
        [Test]
        public async Task Only_unresolved_issues_should_be_retried()
        {
            FailedMessage messageToBeRetriedAsPartOfGroupRetry = null;
            FailedMessage messageToBeArchived = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1)
                        .ConfigureAwait(false);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2)
                        .ConfigureAwait(false);
                }).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.MessageToBeRetriedByGroupId == null || c.MessageToBeArchivedId == null)
                    {
                        return false;
                    }

                    //First we are going to issue an archive to one of the messages
                    if (!c.ArchiveIssued)
                    {
                        var messageToBeArchivedUnresolvedResult = await this.TryGet<FailedMessage>($"/api/errors/{c.MessageToBeArchivedId}", e => e.Status == FailedMessageStatus.Unresolved);
                        messageToBeArchived = messageToBeArchivedUnresolvedResult;
                        if (!messageToBeArchivedUnresolvedResult)
                        {
                            return false;
                        }

                        await this.Patch<object>($"/api/errors/{messageToBeArchived.UniqueMessageId}/archive");

                        c.ArchiveIssued = true;

                        return false;
                    }

                    //We are now going to issue a retry group
                    if (!c.RetryIssued)
                    {
                        // Ensure message is being retried
                        var messageToBeRetriedAsPartOfGroupUnresolvedRetryResult = await this.TryGet<FailedMessage>($"/api/errors/{c.MessageToBeRetriedByGroupId}", e => e.Status == FailedMessageStatus.Unresolved);
                        messageToBeRetriedAsPartOfGroupRetry = messageToBeRetriedAsPartOfGroupUnresolvedRetryResult;
                        if (!messageToBeRetriedAsPartOfGroupUnresolvedRetryResult)
                        {
                            return false;
                        }

                        c.RetryIssued = true;

                        await this.Post<object>($"/api/recoverability/groups/{messageToBeRetriedAsPartOfGroupRetry.FailureGroups[0].Id}/errors/retry");

                        return false;
                    }

                    var messageToBeRetriedAsPartOfGroupResolvedRetryResult = await this.TryGet<FailedMessage>($"/api/errors/{c.MessageToBeRetriedByGroupId}", e => e.Status == FailedMessageStatus.Resolved);
                    messageToBeRetriedAsPartOfGroupRetry = messageToBeRetriedAsPartOfGroupResolvedRetryResult;
                    if (!messageToBeRetriedAsPartOfGroupResolvedRetryResult)
                    {
                        return false;
                    }

                    var messageToBeArchivedArchivedResult = await this.TryGet<FailedMessage>($"/api/errors/{c.MessageToBeArchivedId}", e => e.Status == FailedMessageStatus.Archived);
                    messageToBeArchived = messageToBeArchivedArchivedResult;
                    return messageToBeArchivedArchivedResult;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Archived, messageToBeArchived.Status, "Non retried message should be archived");
            Assert.AreEqual(FailedMessageStatus.Resolved, messageToBeRetriedAsPartOfGroupRetry.Status, "Retried Message should not be set to Archived when group is retried");
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

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.EndpointName()).ToString();

                    if (message.MessageNumber == 1)
                    {
                        Context.MessageToBeRetriedByGroupId = uniqueMessageId;
                    }
                    else
                    {
                        Context.MessageToBeArchivedId = uniqueMessageId;
                    }

                    if (!Context.RetryIssued)
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

        public class MyContext : ScenarioContext
        {
            public string MessageToBeRetriedByGroupId { get; set; }
            public string MessageToBeArchivedId { get; set; }

            public bool ArchiveIssued { get; set; }
            public bool RetryIssued { get; set; }
        }
    }
}
