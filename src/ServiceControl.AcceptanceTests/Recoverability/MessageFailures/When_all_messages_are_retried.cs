﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    class When_all_messages_are_retried : AcceptanceTest
    {
        [Test]
        public async Task Only_unresolved_issues_should_be_retried()
        {
            FailedMessage messageToBeRetriedAsPartOfRetryAll = null;
            FailedMessage messageToBeArchived = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    await bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
                }).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.MessageToBeRetriedAsPartOfRetryAllId == null || c.MessageToBeArchivedId == null)
                    {
                        return false;
                    }

                    //First we are going to issue an archive to one of the messages
                    if (!c.ArchiveIssued)
                    {
                        var result = await this.TryGet<FailedMessage>("/api/errors/" + c.MessageToBeArchivedId, e => e.Status == FailedMessageStatus.Unresolved);
                        messageToBeArchived = result;
                        if (!result)
                        {
                            return false;
                        }

                        await this.Patch<object>($"/api/errors/{messageToBeArchived.UniqueMessageId}/archive");

                        c.ArchiveIssued = true;

                        return false;
                    }

                    //We are now going to issue a retry group
                    if (!c.RetryAllIssued)
                    {
                        // Ensure message is being retried
                        var unresolvedResult = await this.TryGet<FailedMessage>("/api/errors/" + c.MessageToBeRetriedAsPartOfRetryAllId, e => e.Status == FailedMessageStatus.Unresolved);
                        messageToBeRetriedAsPartOfRetryAll = unresolvedResult;
                        if (!unresolvedResult)
                        {
                            return false;
                        }

                        c.RetryAllIssued = true;

                        await this.Post<object>("/api/errors/retry/all");

                        return false;
                    }

                    var resolvedResult = await this.TryGet<FailedMessage>("/api/errors/" + c.MessageToBeRetriedAsPartOfRetryAllId, e => e.Status == FailedMessageStatus.Resolved);
                    messageToBeRetriedAsPartOfRetryAll = resolvedResult;
                    if (!resolvedResult)
                    {
                        return false;
                    }

                    var archivedResult = await this.TryGet<FailedMessage>("/api/errors/" + c.MessageToBeArchivedId, e => e.Status == FailedMessageStatus.Archived);
                    messageToBeArchived = archivedResult;
                    return archivedResult;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.AreEqual(FailedMessageStatus.Archived, messageToBeArchived.Status, "Non retried message should be archived");
            Assert.AreEqual(FailedMessageStatus.Resolved, messageToBeRetriedAsPartOfRetryAll.Status, "Retried Message should not be set to Archived when group is retried");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext scenarioContext;
                readonly ReadOnlySettings settings;

                public MyMessageHandler(MyContext scenarioContext, ReadOnlySettings settings)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();

                    if (message.MessageNumber == 1)
                    {
                        scenarioContext.MessageToBeRetriedAsPartOfRetryAllId = uniqueMessageId;
                    }
                    else
                    {
                        scenarioContext.MessageToBeArchivedId = uniqueMessageId;
                    }

                    if (!scenarioContext.RetryAllIssued)
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
            public string MessageToBeRetriedAsPartOfRetryAllId { get; set; }
            public string MessageToBeArchivedId { get; set; }

            public bool ArchiveIssued { get; set; }
            public bool RetryAllIssued { get; set; }
        }
    }
}