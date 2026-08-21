namespace ServiceControl.AcceptanceTests.Recoverability.Groups
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
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_failing_endpoint_is_triaged_and_retried : AcceptanceTest
    {
        [Test]
        public async Task Should_narrow_to_the_group_annotate_it_and_clear_it_once_retried()
        {
            Dictionary<string, Dictionary<string, int>> summary = null;
            List<FailedMessageView> broken = null;
            FailureGroupView group = null;
            GroupOperation annotated = null;
            GroupOperation corrected = null;
            GroupOperation cleared = null;
            RetryHistory afterRetry = null;
            RetryHistory afterDismissal = null;

            await Define<Context>()
                .WithEndpoint<Broken>(b => b.When(async bus =>
                {
                    await bus.SendLocal(new BrokenCommand());
                    await bus.SendLocal(new BrokenCommand());
                }).DoNotFailOnErrorMessages())
                .WithEndpoint<Unrelated>(b => b.When(bus => bus.SendLocal(new UnrelatedCommand())).DoNotFailOnErrorMessages())
                .Do("Wait for all three failures to be grouped", async ctx =>
                {
                    var failures = await this.TryGetMany<FailedMessageView>("/api/errors?per_page=50");

                    var forBroken = failures.Items.Where(failure => failure.ReceivingEndpoint.Name == BrokenEndpoint).ToArray();

                    if (failures.Items.Count != 3 || forBroken.Length != 2)
                    {
                        return false;
                    }

                    // A failure belongs to one group per classifier, in no guaranteed order, so the
                    // group has to be picked by the classifier the groups list defaults to rather
                    // than by position: the persisters order them differently.
                    var message = await this.TryGet<FailedMessage>($"/api/errors/{forBroken[0].Id}",
                        found => found.FailureGroups.Any(group => group.Type == DefaultClassifier));

                    ctx.GroupId = message.HasResult
                        ? message.Item.FailureGroups.Single(group => group.Type == DefaultClassifier).Id
                        : null;

                    return ctx.GroupId != null;
                })
                .Do("Read the summary the page opens with", async _ =>
                {
                    summary = await this.TryGet<Dictionary<string, Dictionary<string, int>>>("/api/errors/summary");
                })
                .Do("Narrow the list to the endpoint that broke", async _ =>
                {
                    broken = (await this.TryGetMany<FailedMessageView>($"/api/endpoints/{BrokenEndpoint}/errors?per_page=50")).Items;
                })
                .Do("Open the group behind those failures", async ctx =>
                {
                    var opened = await this.TryGet<FailureGroupView>($"/api/recoverability/groups/id/{ctx.GroupId}");

                    group = opened.Item;

                    return opened.HasResult;
                })
                .Do("Leave a note on the group while the fix is in flight", async ctx =>
                {
                    await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/comment?comment={Uri.EscapeDataString(FirstNote)}");

                    annotated = await NoteOn(ctx.GroupId, FirstNote);

                    return annotated != null;
                })
                .Do("Correct the note once the cause is known", async ctx =>
                {
                    await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/comment?comment={Uri.EscapeDataString(SecondNote)}");

                    corrected = await NoteOn(ctx.GroupId, SecondNote);

                    return corrected != null;
                })
                .Do("Remove the note", async ctx =>
                {
                    await this.Delete($"/api/recoverability/groups/{ctx.GroupId}/comment");

                    cleared = await NoteOn(ctx.GroupId, null);

                    return cleared != null;
                })
                .Do("Retry the group now the fix is out", async ctx =>
                {
                    ctx.FixDeployed = true;

                    await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/retry");
                })
                .Do("Wait for the retry to finish and report itself", async ctx =>
                {
                    var history = await this.TryGet<RetryHistory>("/api/recoverability/history",
                        found => found.UnacknowledgedOperations.Any(operation => operation.RequestId == ctx.GroupId));

                    afterRetry = history.Item;

                    return history.HasResult;
                })
                .Do("Dismiss the completed operation", async ctx =>
                {
                    await this.Delete($"/api/recoverability/unacknowledgedgroups/{ctx.GroupId}");

                    var history = await this.TryGet<RetryHistory>("/api/recoverability/history",
                        found => !found.UnacknowledgedOperations.Any(operation => operation.RequestId == ctx.GroupId));

                    afterDismissal = history.Item;

                    return history.HasResult;
                })
                .Done(_ => true)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary[FailedMessageSummaryKeys.Endpoints], Is.EquivalentTo(new Dictionary<string, int>
                {
                    [BrokenEndpoint] = 2,
                    [UnrelatedEndpoint] = 1
                }), "The summary counts failures per endpoint, which is how the page shows where the trouble is");

                Assert.That(broken.Select(failure => failure.ReceivingEndpoint.Name), Is.EquivalentTo(new[] { BrokenEndpoint, BrokenEndpoint }),
                    "Narrowing to an endpoint has to leave out the other endpoint's failure, not just include this one's");

                Assert.That(group.Count, Is.EqualTo(2),
                    "The group holds only the broken endpoint's failures, so the other endpoint's failure grouped separately");

                Assert.That(annotated.Comment, Is.EqualTo(FirstNote));

                Assert.That(corrected.Comment, Is.EqualTo(SecondNote),
                    "Posting a second note replaces the first rather than adding to it");

                Assert.That(cleared.Comment, Is.Null,
                    "Deleting the note clears it, or an operator cannot withdraw what they wrote");

                var completed = afterRetry.UnacknowledgedOperations.Single(operation => operation.RequestId == group.Id);

                Assert.That(completed.RetryType, Is.EqualTo(RetryType.FailureGroup),
                    "Only a FailureGroup operation can be dismissed through the unacknowledgedgroups route");

                Assert.That(completed.NumberOfMessagesProcessed, Is.EqualTo(2),
                    "The banner tells the operator how many messages the retry moved");

                Assert.That(afterDismissal.HistoricOperations.Select(operation => operation.RequestId), Does.Contain(group.Id),
                    "Dismissing clears the banner without erasing the retry from the history panel");
            }
        }

        // The comment is surfaced on the groups list rather than by the single group route, which
        // returns the title and counts only. Both persisters agree, and ServicePulse reads the note
        // from the list too.
        async Task<GroupOperation> NoteOn(string groupId, string expected)
        {
            var groups = await this.TryGetMany<GroupOperation>("/api/recoverability/groups",
                candidate => candidate.Id == groupId && candidate.Comment == expected);

            return groups.HasResult ? groups.Items.Single() : null;
        }

        const string DefaultClassifier = "Exception Type and Stack Trace";

        const string FirstNote = "Waiting on the fix";
        const string SecondNote = "Caused by the bad release";

        static string BrokenEndpoint => Conventions.EndpointNamingConvention(typeof(Broken));
        static string UnrelatedEndpoint => Conventions.EndpointNamingConvention(typeof(Unrelated));

        public class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }

            public string GroupId { get; set; }

            public bool FixDeployed { get; set; }
        }

        public class Broken : EndpointConfigurationBuilder
        {
            public Broken() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class BrokenCommandHandler(Context scenarioContext) : IHandleMessages<BrokenCommand>
            {
                public Task Handle(BrokenCommand message, IMessageHandlerContext context)
                {
                    if (!scenarioContext.FixDeployed)
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class Unrelated : EndpointConfigurationBuilder
        {
            public Unrelated() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class UnrelatedCommandHandler : IHandleMessages<UnrelatedCommand>
            {
                public Task Handle(UnrelatedCommand message, IMessageHandlerContext context) =>
                    throw new Exception("Simulated exception in an endpoint that is not being triaged");
            }
        }

        public class BrokenCommand : ICommand;

        public class UnrelatedCommand : ICommand;
    }
}
