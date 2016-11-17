namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_group_is_retried : AcceptanceTest
    {
        [Test]
        public void Only_unresolved_issues_should_be_retried()
        {
            var context = new MyContext();

            FailedMessage messageToBeRetriedAsPartOfGroupRetry = null;
            FailedMessage messageToBeArchived = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
                }))
                .Done(c =>
                {
                    if (c.MessageToBeRetriedByGroupId == null || c.MessageToBeArchivedId == null)
                    {
                        return false;
                    }

                    //First we are going to issue an archive to one of the messages
                    if (!c.ArchiveIssued)
                    {
                        if (!TryGet("/api/errors/" + c.MessageToBeArchivedId, out messageToBeArchived, e => e.Status == FailedMessageStatus.Unresolved))
                        {
                            return false;
                        }

                        Patch<object>($"/api/errors/{messageToBeArchived.UniqueMessageId}/archive");

                        c.ArchiveIssued = true;

                        return false;
                    }

                    //We are now going to issue a retry group
                    if (!c.RetryIssued)
                    {
                        // Ensure message is being retried
                        if (!TryGet("/api/errors/" + c.MessageToBeRetriedByGroupId, out messageToBeRetriedAsPartOfGroupRetry, e => e.Status == FailedMessageStatus.Unresolved))
                        {
                            return false;
                        }

                        c.RetryIssued = true;
                        
                        Post<object>($"/api/recoverability/groups/{messageToBeRetriedAsPartOfGroupRetry.FailureGroups[0].Id}/errors/retry");

                        return false;
                    }

                    if (!TryGet("/api/errors/" + c.MessageToBeRetriedByGroupId, out messageToBeRetriedAsPartOfGroupRetry, e => e.Status == FailedMessageStatus.Resolved))
                    {
                        return false;
                    }

                    if (!TryGet("/api/errors/" + c.MessageToBeArchivedId, out messageToBeArchived, e => e.Status == FailedMessageStatus.Archived))
                    {
                        return false;
                    }

                    return true;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Archived, messageToBeArchived.Status, "Non retried message should be archived");
            Assert.AreEqual(FailedMessageStatus.Resolved, messageToBeRetriedAsPartOfGroupRetry.Status, "Retried Message should not be set to Archived when group is retried");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress().ToString()).ToString();

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
            public string MessageToBeRetriedByGroupId { get; set; }
            public string MessageToBeArchivedId { get; set; }

            public bool ArchiveIssued { get; set; }
            public bool RetryIssued { get; set; }
        }
    }
}
