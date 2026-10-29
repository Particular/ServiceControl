#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationStartupCheckRunnerTests
{
    [Test]
    public async Task Checks_run_in_the_order_they_were_given()
    {
        var ran = new List<string>();

        await MigrationStartupCheckRunner.Run(
        [
            Check("the first", _ => ran.Add("first")),
            Check("the second", _ => ran.Add("second")),
            Check("the third", _ => ran.Add("third"))
        ]);

        Assert.That(ran, Is.EqualTo(new[] { "first", "second", "third" }), "the databases are opened by checks that sit behind the ones refusing the configuration outright");
    }

    [Test]
    public void The_first_check_to_refuse_stops_the_ones_behind_it()
    {
        var ran = new List<string>();
        var failure = new InvalidOperationException("ServiceControl/RetryHistoryDepth is 0");

        var exception = Assert.ThrowsAsync<Exception>(() => MigrationStartupCheckRunner.Run(
        [
            Check("the first", _ => ran.Add("first")),
            Check("the migration target is ready", _ => throw failure),
            Check("the last", _ => ran.Add("last"))
        ]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ran, Is.EqualTo(new[] { "first" }), "a check behind a refusal would run against a configuration already known to be wrong, and the two that open a database are at the end of the list");
            Assert.That(exception!.Message, Does.Contain("the migration target is ready").And.Contain(failure.Message), "the operator needs to know which check refused and why");
            Assert.That(exception.InnerException, Is.SameAs(failure), "the original carries the stack and the detail the summary leaves out");
        }
    }

    [Test]
    public void A_host_being_stopped_is_not_a_check_refusing()
    {
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        var exception = Assert.ThrowsAsync<OperationCanceledException>(() => MigrationStartupCheckRunner.Run(
            [Check("the first", token => token.ThrowIfCancellationRequested())], stopping.Token));

        Assert.That(exception!.Message, Does.Not.Contain("Migration startup check"), "a stop dressed up as a refusal sends the operator after a setting that was never wrong");
    }

    static IMigrationStartupCheck Check(string name, Action<CancellationToken> run) => new FakeCheck(name, run);

    sealed class FakeCheck(string name, Action<CancellationToken> run) : IMigrationStartupCheck
    {
        public string Name => name;

        public Task Run(CancellationToken cancellationToken = default)
        {
            run(cancellationToken);
            return Task.CompletedTask;
        }
    }
}
