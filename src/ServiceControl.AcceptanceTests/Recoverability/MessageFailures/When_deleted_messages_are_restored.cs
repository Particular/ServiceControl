namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Recoverability;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_deleted_messages_are_restored : AcceptanceTest
    {
        [Test]
        public async Task Should_restore_a_deleted_selection_and_a_deleted_group()
        {
            string[] afterSelectionDeleted = null;
            string[] afterRangeRestored = null;
            string[] afterGroupDeleted = null;
            string[] afterGroupRestored = null;
            FailedMessage afterSelection = null;
            FailedMessage afterGroup = null;

            var context = await Define<Context>()
                .WithEndpoint<Broken>(b => b.When(async bus =>
                {
                    await bus.SendLocal(new BrokenCommand());
                    await bus.SendLocal(new BrokenCommand());
                    await bus.SendLocal(new BrokenCommand());
                }).DoNotFailOnErrorMessages())
                .WithEndpoint<Unrelated>(b => b.When(bus => bus.SendLocal(new UnrelatedCommand())).DoNotFailOnErrorMessages())
                .Do("Wait for all four failures to be grouped", async ctx =>
                {
                    var failures = await this.TryGetMany<FailedMessageView>("/api/errors?per_page=50");

                    var broken = failures.Items.Where(failure => failure.ReceivingEndpoint.Name == BrokenEndpoint).ToArray();

                    if (failures.Items.Count != 4 || broken.Length != 3)
                    {
                        return false;
                    }

                    var message = await this.TryGet<FailedMessage>($"/api/errors/{broken[0].Id}",
                        found => found.FailureGroups.Any(group => group.Type == DefaultClassifier));

                    if (!message.HasResult)
                    {
                        return false;
                    }

                    ctx.GroupId = message.Item.FailureGroups.Single(group => group.Type == DefaultClassifier).Id;
                    ctx.Unrelated = failures.Items.Single(failure => failure.ReceivingEndpoint.Name == UnrelatedEndpoint).Id;
                    ctx.Deleted = broken.Take(2).Select(failure => failure.Id).ToArray();
                    ctx.Kept = broken[2].Id;

                    return true;
                })
                .Do("Delete two of the three failures", async ctx =>
                    await this.Patch("/api/errors/archive", ctx.Deleted.ToList()))
                .Do("Wait until those two are gone", async ctx =>
                {
                    afterSelectionDeleted = await StatusesOnceArchived(ctx.Deleted);

                    if (afterSelectionDeleted == null)
                    {
                        return false;
                    }

                    afterSelection = await this.TryGet<FailedMessage>($"/api/errors/{ctx.Unrelated}");

                    return true;
                })
                .Do("Restore everything deleted in the window", async _ =>
                {
                    var from = DateTime.UtcNow.AddHours(-1).ToString(Iso8601);
                    var to = DateTime.UtcNow.AddHours(1).ToString(Iso8601);

                    await this.Patch<object>($"/api/errors/{from}...{to}/unarchive");
                })
                .Do("Wait until they are back", async ctx =>
                {
                    afterRangeRestored = await StatusesOnceUnresolved(ctx.Deleted);

                    return afterRangeRestored != null;
                })
                .Do("Delete the whole group instead", async ctx =>
                    await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive"))
                .Do("Wait until the whole group is gone", async ctx =>
                {
                    afterGroupDeleted = await StatusesOnceArchived([.. ctx.Deleted, ctx.Kept]);

                    if (afterGroupDeleted == null)
                    {
                        return false;
                    }

                    afterGroup = await this.TryGet<FailedMessage>($"/api/errors/{ctx.Unrelated}");

                    var archived = await this.TryGet<FailureGroupView>($"/api/archive/groups/id/{ctx.GroupId}",
                        group => group.Count == 3);

                    return archived.HasResult;
                })
                .Do("Restore the whole group", async ctx =>
                    await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/unarchive"))
                .Do("Wait until the whole group is back", async ctx =>
                {
                    afterGroupRestored = await StatusesOnceUnresolved([.. ctx.Deleted, ctx.Kept]);

                    return afterGroupRestored != null;
                })
                .Done(_ => true)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterSelectionDeleted, Has.All.EqualTo(nameof(FailedMessageStatus.Archived)));

                Assert.That(afterRangeRestored, Has.All.EqualTo(nameof(FailedMessageStatus.Unresolved)),
                    "Restoring by range has to bring back everything the selection deleted");

                Assert.That(afterGroupRestored, Has.All.EqualTo(nameof(FailedMessageStatus.Unresolved)),
                    "Restoring a group has to bring back every message the group delete took, not only the ones deleted individually");

                Assert.That(afterSelection.Status, Is.EqualTo(FailedMessageStatus.Unresolved),
                    "Deleting a selection must reach only the ids it was given");

                Assert.That(afterGroup.Status, Is.EqualTo(FailedMessageStatus.Unresolved),
                    "Deleting a group must reach only that group, and the other endpoint's failure grouped separately");
            }
        }

        async Task<string[]> StatusesOnceArchived(IReadOnlyCollection<string> ids) => await StatusesOnce(ids, FailedMessageStatus.Archived);

        async Task<string[]> StatusesOnceUnresolved(IReadOnlyCollection<string> ids) => await StatusesOnce(ids, FailedMessageStatus.Unresolved);

        async Task<string[]> StatusesOnce(IReadOnlyCollection<string> ids, FailedMessageStatus expected)
        {
            var statuses = new List<string>();

            foreach (var id in ids)
            {
                var message = await this.TryGet<FailedMessage>($"/api/errors/{id}", found => found.Status == expected);

                if (!message.HasResult)
                {
                    return null;
                }

                statuses.Add(message.Item.Status.ToString());
            }

            return [.. statuses];
        }

        const string DefaultClassifier = "Exception Type and Stack Trace";
        const string Iso8601 = "yyyy-MM-ddTHH:mm:ssZ";

        static string BrokenEndpoint => Conventions.EndpointNamingConvention(typeof(Broken));
        static string UnrelatedEndpoint => Conventions.EndpointNamingConvention(typeof(Unrelated));

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }

            public string GroupId { get; set; }

            public string[] Deleted { get; set; }

            public string Kept { get; set; }

            public string Unrelated { get; set; }
        }

        public class Broken : EndpointConfigurationBuilder
        {
            public Broken() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class BrokenCommandHandler : IHandleMessages<BrokenCommand>
            {
                public Task Handle(BrokenCommand message, IMessageHandlerContext context) =>
                    throw new Exception("Simulated exception");
            }
        }

        public class Unrelated : EndpointConfigurationBuilder
        {
            public Unrelated() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class UnrelatedCommandHandler : IHandleMessages<UnrelatedCommand>
            {
                public Task Handle(UnrelatedCommand message, IMessageHandlerContext context) =>
                    throw new Exception("Simulated exception on an endpoint nothing here selects");
            }
        }

        public class BrokenCommand : ICommand;

        public class UnrelatedCommand : ICommand;
    }
}
