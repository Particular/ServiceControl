namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
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

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    bus.SendLocal<MyMessage>(m => m.MessageNumber = 1);
                    bus.SendLocal<MyMessage>(m => m.MessageNumber = 2);
                }))
                .Done(c =>
                {
                    if (c.FirstMessageId == null || c.SecondMessageId == null)
                        return false;

                    if (!c.ArchiveIssued)
                    {
                        List<FailedMessage.FailureGroup> beforeArchiveGroups;

                        if (!TryGetMany("/api/recoverability/groups/", out beforeArchiveGroups))
                            return false;

                        Post<object>(String.Format("/api/recoverability/groups/{0}/errors/archive", beforeArchiveGroups[0].Id));
                        c.ArchiveIssued = true;
                    }

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
        public void Only_unresolved_issues_should_be_retried()
        {
            var context = new MyContext();

            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;
            string failureGroupId = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
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
                        Post<object>(String.Format("/api/errors/{0}/retry", c.SecondMessageId));
                    }

                    if (!c.ArchiveIssued)
                    {
                        // Ensure message is being retried
                        if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure, e => e.Status != FailedMessageStatus.Unresolved))
                            return false;

                        Post<object>(String.Format("/api/recoverability/groups/{0}/errors/archive", failureGroupId));
                        c.ArchiveIssued = true;
                    }

                    if (!TryGet("/api/errors/" + c.FirstMessageId, out firstFailure, e => e.Status == FailedMessageStatus.Archived))
                        return false;

                    if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure, e => e.Status != FailedMessageStatus.Unresolved))
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
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    })
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {

                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.EndpointName()).ToString();

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
        }

    }
}
