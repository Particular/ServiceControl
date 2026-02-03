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

    class When_a_group_is_retried : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task Only_unresolved_issues_should_be_retried(CancellationToken cancellationToken)
        {
            FailedMessage messageToBeRetriedAsPartOfGroupRetry = null;
            FailedMessage messageToBeArchived = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
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
                .Run(cancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(messageToBeArchived.Status, Is.EqualTo(FailedMessageStatus.Archived), "Non retried message should be archived");
                Assert.That(messageToBeRetriedAsPartOfGroupRetry.Status, Is.EqualTo(FailedMessageStatus.Resolved), "Retried Message should not be set to Archived when group is retried");
            });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.Recoverability().Delayed(x => x.NumberOfRetries(0));
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
                        scenarioContext.MessageToBeRetriedByGroupId = uniqueMessageId;
                    }
                    else
                    {
                        scenarioContext.MessageToBeArchivedId = uniqueMessageId;
                    }

                    if (!scenarioContext.RetryIssued)
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

        public class MyContext : ScenarioContext
        {
            public string MessageToBeRetriedByGroupId { get; set; }
            public string MessageToBeArchivedId { get; set; }

            public bool ArchiveIssued { get; set; }
            public bool RetryIssued { get; set; }
        }
    }
}