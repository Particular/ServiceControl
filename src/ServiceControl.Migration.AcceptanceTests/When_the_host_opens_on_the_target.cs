namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Hosting.Commands;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_the_host_opens_on_the_target : MigrationAcceptanceTest
{
    const string HostOpenedSetting = "Migration/HostOpenedOnTarget";

    [Test]
    public async Task Importing_failed_errors_is_refused_while_a_copy_is_unfinished_and_starts_and_stamps_the_row_once_only_optional_work_is_left()
    {
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var host = ImportFailedErrorsCommand.BuildHost(Settings);

        var refusal = Assert.ThrowsAsync<Exception>(async () => await host.StartAsync(cancellation.Token));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain("--import-failed-errors will not start").And.Contain("KnownEndpoints is Copying (InProgress)"));
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "a refused import has opened on nothing");
        }

        await SeedRequiredCategoriesComplete();
        await SeedCheckpoint(MigrationCategoryIds.EventLog, MigrationCategoryState.InProgress);
        await SeedCheckpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryState.CompleteWithErrors);

        using var opened = ImportFailedErrorsCommand.BuildHost(Settings);

        Assert.That(async () => await opened.StartAsync(cancellation.Token), Throws.Nothing, "an optional category copies in the background after the host opens, so it never holds the import back");

        try
        {
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null, "the import writes failed messages into the target, so it has opened on it as surely as the main host has");
        }
        finally
        {
            await opened.StopAsync(cancellation.Token);
        }
    }

    async Task SeedRequiredCategoriesComplete()
    {
        foreach (var category in MigrationCategoryRegistry.All.Where(category => category.Kind == MigrationCategoryKind.Required))
        {
            await SeedCheckpoint(category.Id);
        }
    }
}
