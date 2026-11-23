namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class When_an_operator_retries_or_abandons_a_failed_category : MigrationAcceptanceTest
{
    [Test]
    public async Task A_retry_of_retry_operations_deletes_what_it_copied()
    {
        await SeedSourceRetryHistory(RetryResetMigrationTests.TwoHistoricAndTwoPending());
        await CopyCategory(MigrationCategoryIds.RetryOperations);
        await SeedCheckpoint(await ReadCheckpoint(MigrationCategoryIds.RetryOperations) with { State = MigrationCategoryState.Halted, LastError = "test" });

        await ExpectExitCode(MigrateCommand.Run(Settings, GuidedMigrationArguments.Unattended with { Retry = new HashSet<string>(StringComparer.Ordinal) { "RetryOperations" } }, MigrationConsole.System()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Target.Count(RetryOperations), Is.EqualTo(4), "a historic row gets a new key on every insert, so 4 and not 6 shows the retry emptied the tables before the copy read the category again");
            Assert.That((await ReadCheckpoint(MigrationCategoryIds.RetryOperations)).State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That((await ReadCheckpoint(MigrationCategoryIds.RetryOperations)).AlreadyPresentCount, Is.Zero, "the pending table was emptied too, so the re-read finds none of its rows already present");
        }
    }

    [Test]
    public async Task An_abandoned_required_category_lets_the_host_open()
    {
        await SeedCheckpoint(Halted(MigrationCategoryIds.MessageRedirects));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.That(async () => await RunCommand.Run(Settings, null, cancellation.Token),
            Throws.Exception.With.Message.Contain("--abandon=MessageRedirects"), "the host stays closed behind a Failed required category, and its refusal names the way out");

        var before = DateTime.UtcNow;
        await ExpectExitCode(MigrateCommand.Run(Settings, GuidedMigrationArguments.Unattended with { Abandon = new HashSet<string>(StringComparer.Ordinal) { "MessageRedirects" } }, MigrationConsole.System()));
        var after = DateTime.UtcNow;

        var abandoned = await ReadCheckpoint(MigrationCategoryIds.MessageRedirects);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(abandoned.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That(abandoned.SettledAt, Is.InRange(before.AddMilliseconds(-1), after.AddMilliseconds(1)), "the abandon is stamped with the time it ran, as UTC, which PostgreSQL insists on");
            Assert.That((abandoned.CopiedCount, abandoned.SkippedCount, abandoned.LastError), Is.EqualTo((40L, 3L, HaltReason)), "the counts and the error are the size of the loss and why it happened");
        }

        await RunHostUntilTheApiAnswers();
    }

    const string HaltReason = "Halted by the test.";

    static MigrationCheckpoint Halted(string categoryId) =>
        new(categoryId, MigrationCategoryState.Halted, "cursor-40", 40, 3, null, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 3 },
            new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc), null, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc), HaltReason);

    static MigrationCategory RetryOperations => MigrationCategoryRegistry.Find(MigrationCategoryIds.RetryOperations)!;
}
