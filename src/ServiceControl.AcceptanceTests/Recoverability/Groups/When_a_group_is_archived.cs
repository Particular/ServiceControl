namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_group_is_archived : AcceptanceTest
    {
        [Test]
        public void All_messages_in_group_should_get_archived()
        {
            var context = new MyContext();

            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                    {
                        bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                        bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
                    })
                    .When(ctx =>
                    {
                        if (ctx.ArchiveIssued || ctx.FirstMessageId == null || ctx.SecondMessageId == null)
                        {
                            return false;
                        }

                        List<FailedMessage.FailureGroup> beforeArchiveGroups;
                        if (!TryGetMany("/api/recoverability/groups/", out beforeArchiveGroups))
                        {
                            return false;
                        }

                        foreach (var group in beforeArchiveGroups)
                        {
                            List<FailedMessage> failedMessages;
                            if (TryGetMany($"/api/recoverability/groups/{@group.Id}/errors", out failedMessages))
                            {
                                if (failedMessages.Count == 2)
                                {
                                    ctx.GroupId = group.Id;
                                    return true;
                                }
                            }
                        }

                        Thread.Sleep(1000);

                        return false;

                    }, (bus, ctx) =>
                    {
                        Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive");
                        ctx.ArchiveIssued = true;
                    })
                )
                .Done(c =>
                {
                    if (c.FirstMessageId == null || c.SecondMessageId == null)
                        return false;

                    if (!TryGet("/api/errors/" + c.FirstMessageId, out firstFailure, e => e.Status == FailedMessageStatus.Archived))
                        return false;

                    if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure, e => e.Status == FailedMessageStatus.Archived))
                        return false;

                    return true;
                })
                .Run();

            Assert.AreEqual(FailedMessageStatus.Archived, firstFailure.Status, "First Message should be archived");
            Assert.AreEqual(FailedMessageStatus.Archived, secondFailure.Status, "Second Message should be archived");
        }

        [Test]
        public void Only_unresolved_issues_should_be_archived()
        {
            var context = new MyContext();

            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;
            string failureGroupId = null;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
                }))
                .Done(c =>
                {
                    if (c.FirstMessageId == null || c.SecondMessageId == null)
                        return false;

                    if (!c.RetryIssued)
                    {
                        List<FailedMessage.FailureGroup> beforeArchiveGroups;

                        // Don't retry until the message has been added to a group
                        if (!TryGetMany("/api/recoverability/groups/", out beforeArchiveGroups))
                            return false;

                        failureGroupId = beforeArchiveGroups[0].Id;

                        if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure, e => e.Status == FailedMessageStatus.Unresolved))
                            return false;

                        c.RetryIssued = true;
                        Post<object>($"/api/errors/{c.SecondMessageId}/retry");
                    }

                    if (!c.ArchiveIssued)
                    {
                        // Ensure message is being retried
                        if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure, e => e.Status != FailedMessageStatus.Unresolved))
                            return false;

                        Post<object>($"/api/recoverability/groups/{failureGroupId}/errors/archive");
                        c.ArchiveIssued = true;
                    }

                    if (!TryGet("/api/errors/" + c.FirstMessageId, out firstFailure, e => e.Status == FailedMessageStatus.Archived))
                        return false;

                    if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure, e => e.Status == FailedMessageStatus.Resolved))
                        return false;

                    return true;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Archived, firstFailure.Status, "Non retried message should be archived");
            Assert.AreNotEqual(FailedMessageStatus.Archived, secondFailure.Status, "Retried Message should not be set to Archived when group is archived");
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

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress().Queue).ToString();

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
