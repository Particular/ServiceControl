namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;

[TestFixture]
[NonParallelizable]
class When_the_source_holds_unfinished_operations : MigrationAcceptanceTest
{
    [Test]
    public async Task The_offline_command_refuses_before_any_category_is_copied()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));
        var batchId = $"RetryBatches/{Guid.NewGuid()}";
        await SeedSource((batchId, new { Status = 2 }));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var refusal = Assert.ThrowsAsync<Exception>(async () => await MigrateCommand.Run(Settings, AllowingAnUnreleasedMigration(), cancellation.Token));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain(batchId).And.Contain("Start ServiceControl on RavenDB again"));
            Assert.That(await QueryTarget(dbContext => dbContext.EndpointSettings.CountAsync()), Is.Zero, "a required row was copied before the refusal");
            Assert.That(await QueryTarget(dbContext => dbContext.MigrationCheckpoints.CountAsync()), Is.Zero, "a refusal initializes no checkpoint");
        }
    }

    // An operation found once ServiceControl runs on SQL is not a reason to advise going back to RavenDB or to hold the copy.
    [Test]
    public async Task The_online_copy_ignores_an_operation_left_in_the_source_after_the_offline_copy()
    {
        MigrationSettings();
        var failure = new IngestedFailure();
        await SeedSourceWithBody(failure.ToFailedMessage(), failure.Body, failure.ContentType);
        await MigrateCommand.Run(Settings, AllowingAnUnreleasedMigration());
        await SeedSource(($"RetryBatches/{Guid.NewGuid()}", new { Status = 2 }));

        await RunHostUntilTheApiAnswers();
        await WaitUntil(
            async () => (await ReadCheckpoint(MigrationCategoryIds.UnresolvedFailedMessages))?.State.IsFinished() == true,
            "the unresolved copy finished",
            TimeSpan.FromMinutes(1));

        var checkpoint = await ReadCheckpoint(MigrationCategoryIds.UnresolvedFailedMessages);
        var response = await HttpClient.GetAsync($"api/errors/last/{failure.UniqueMessageIdString}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(1), "the message is the newest unresolved one, so the offline copy took it");
            Assert.That(checkpoint.LastError, Is.Null);
            Assert.That(response.IsSuccessStatusCode, Is.True, "the copied message is not reachable");
        }
    }
}
