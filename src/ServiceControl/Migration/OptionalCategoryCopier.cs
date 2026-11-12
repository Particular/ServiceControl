namespace ServiceControl.Migration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

sealed class OptionalCategoryCopier(IServiceProvider services, Settings settings, IHostApplicationLifetime lifetime, ILogger<OptionalCategoryCopier> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(cancellationToken);
            await Copy(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The online migration stopped. ServiceControl remains available. Check migration progress before restarting.");
        }
    }

    async Task Copy(CancellationToken cancellationToken)
    {
        var store = services.GetService<IMigrationCheckpointStore>();
        if (store is null)
        {
            return;
        }

        var checkpoints = await store.ReadAll(cancellationToken);
        var pending = checkpoints
            .Where(checkpoint => MigrationCategoryRegistry.Find(checkpoint.CategoryId)?.Kind == MigrationCategoryKind.Optional)
            .Where(checkpoint => !checkpoint.State.IsFinished() && !checkpoint.State.IsFailed())
            .Select(checkpoint => checkpoint.CategoryId)
            .ToHashSet(StringComparer.Ordinal);
        if (pending.Count == 0)
        {
            return;
        }

        await new MigrationIsReleasedCheck(services.GetService<AllowUnreleasedMigration>()).Run(cancellationToken);
        var target = services.GetRequiredService<IMigrationTarget>();
        await using var source = PersistenceFactory.CreateMigrationSource(settings);
        var copyable = MigrationStartup.CopyableCategoryIds(source.SupportedCategoryIds, target.SupportedCategoryIds);
        var options = MigrationEngineOptions.FromSettings(Settings.SettingsRootNamespace, settings.EventsRetentionPeriod, settings.ErrorRetentionPeriod);
        var engine = new MigrationEngine(source, target, store, services.GetRequiredService<TimeProvider>(), options,
            services.GetRequiredService<ILogger<MigrationEngine>>());
        var categories = engine.SelectCategories(MigrationCategoryKind.Optional)
            .Where(category => pending.Contains(category.Id) && copyable.Contains(category.Id))
            .ToArray();
        if (categories.Length == 0)
        {
            return;
        }

        await target.Open(cancellationToken);
        try
        {
            await source.Open(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MigrationStartup.RecordSourceOutage(store, categories, exception, cancellationToken);
            logger.LogError(exception, "The online migration cannot open its source. ServiceControl remains available. Restart ServiceControl to try again, or abandon the outstanding optional categories.");
            return;
        }

        await MigrationStartupCheckRunner.Run(source.ContributedChecks(), cancellationToken);
        await engine.RunCategories(categories, cancellationToken);
    }
}
