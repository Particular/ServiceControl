namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_a_copy_is_killed_mid_category : MigrationAcceptanceTest
{
    [Test]
    public async Task It_resumes_rather_than_starting_over()
    {
        await SeedSourceKnownEndpoints("A", "B", "C", "D", "E");
        await SeedSourceEndpointSettings(("A", true), ("B", true), ("C", true), ("D", true), ("E", true));

        // Bounded rather than None: a copy that did not fail would otherwise hold this call open forever.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var killed = Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(builder => builder.FailTheSecondMigrationWrite()), cancellation.Token));

        Assert.That(killed, Is.Not.Null);
        Assert.That(await TargetEndpointSettingsCount(), Is.EqualTo(2), "the first batch and its cursor should have committed together");

        await RunHostUntilTheApiAnswers();

        var checkpoint = await ReadCheckpoint("EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await GetEndpointSettingsNames(), Is.EquivalentTo(new[] { "A", "B", "C", "D", "E" }), "no gaps");
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(5), "no row copied twice");
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.AlreadyPresentCount, Is.Zero, "a resumed run must not re-read rows the first run committed");
        }
    }

    // Proves the already-present count in the test above can fail: both runs end with five correct rows, so that count is the only thing telling a resume from a fresh copy.
    [Test]
    public async Task Without_its_checkpoint_the_second_run_re_reads_what_the_first_already_wrote()
    {
        await SeedSourceKnownEndpoints("A", "B", "C", "D", "E");
        await SeedSourceEndpointSettings(("A", true), ("B", true), ("C", true), ("D", true), ("E", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(builder => builder.FailTheSecondMigrationWrite()), cancellation.Token));

        await DeleteCheckpoint("EndpointSettings");
        await RunHostUntilTheApiAnswers();

        var checkpoint = await ReadCheckpoint("EndpointSettings");

        Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(2));
    }

    Task<int> TargetEndpointSettingsCount() =>
        QueryTarget(dbContext => dbContext.EndpointSettings.CountAsync());

    Task<string[]> GetEndpointSettingsNames() =>
        QueryTarget(dbContext => dbContext.EndpointSettings.Select(settings => settings.Name).ToArrayAsync());
}
