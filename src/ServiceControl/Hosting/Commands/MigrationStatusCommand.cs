namespace ServiceControl.Hosting.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;

class MigrationStatusCommand : AbstractCommand
{
    public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            await Report(settings, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // The console text leaves out a cause its wrapper already quotes, so the log keeps the whole exception.
            LoggerUtil.CreateStaticLogger<MigrationStatusCommand>().LogError(e, "The migration status report failed");
            await Console.Error.WriteLineAsync(MigrationCommandHost.ErrorText(e, "--migration-status"));
            Environment.ExitCode = 1;
        }
    }

    internal static async Task Report(Settings settings, Action<WebApplicationBuilder> customize = null, CancellationToken cancellationToken = default)
    {
        var builder = MigrationCommandHost.CreateBuilder(settings);
        customize?.Invoke(builder);

        // Never start this host, because starting it would run the retention sweeper the persistence registers.
        await using var app = builder.Build();

        var checkpointStore = app.Services.GetService<IMigrationCheckpointStore>()
            ?? throw new InvalidOperationException($"There is no migration to report: the {settings.PersistenceType} persistence keeps no migration checkpoints.");
        var checkpoints = await checkpointStore.ReadAll(cancellationToken);
        // This build copies no optional category, so none is selected.
        var view = MigrationStatusView.Build(checkpoints, []);
        var byId = checkpoints.ToDictionary(checkpoint => checkpoint.CategoryId, StringComparer.Ordinal);

        Console.Out.WriteLine("ServiceControl migration status");
        Console.Out.WriteLine();
        Console.Out.WriteLine("Read from the target's checkpoint store only. The source is never opened, so this command");
        Console.Out.WriteLine("works while ServiceControl is running, even on a source only one process can hold open.");
        Console.Out.WriteLine();
        Console.Out.WriteLine($"{"Category",-40}{"Kind",-10}{"Selected",-10}{"State",-32}{"Copied",12}{"Skipped",10}{"Present",10}{"Done",6}");

        foreach (var status in view)
        {
            var checkpoint = byId.GetValueOrDefault(status.Category.Id);
            // A row the copy saved before reaching its category has not started, so it must not read as a copy under way.
            var notStarted = checkpoint is null || (checkpoint.State == MigrationCategoryState.NotStarted && checkpoint.StartedAt is null);
            var state = notStarted ? "Not started" : $"{status.CustomerState} ({status.State})";
            // An abandoned category stops for good, so a share of its rows would read as a copy still to finish.
            var done = checkpoint is not null && checkpoint.State != MigrationCategoryState.Abandoned && CopyProgress.Percent(checkpoint) is { } percent ? $"{percent}%" : "";

            Console.Out.WriteLine($"{status.Category.Id,-40}{status.Category.Kind,-10}{(status.Selected ? "yes" : "no"),-10}{state,-32}{status.CopiedCount,12:N0}{status.SkippedCount,10:N0}{status.AlreadyPresentCount,10:N0}{done,6}");

            if (status.StartedAt is not null || status.LastError is not null)
            {
                Console.Out.WriteLine($"  started {Time(status.StartedAt)}, last progress {Time(status.LastProgressAt)}, settled {Time(status.SettledAt)}");
            }

            if (status.LastError is not null)
            {
                Console.Out.WriteLine($"  last error: {status.LastError}");
            }

            if (status.NextCommands is not null)
            {
                Console.Out.WriteLine($"  stays Failed on every start until you stop ServiceControl and run: {status.NextCommands}");
            }

            // The same rows the host's start guard refuses.
            if (status.Category.Kind == MigrationCategoryKind.Required && !status.State.IsFinished() && checkpoint is not null)
            {
                Console.Out.WriteLine("  ServiceControl will not start until this is Done or Abandoned, and only --start-data-migration, run with ServiceControl stopped, copies it");
            }
        }

        Console.Out.WriteLine();
        Console.Out.WriteLine("Copied, Skipped and Present are rows the copier is finished with. Done is their share of the rows");
        Console.Out.WriteLine("--start-data-migration counted in the source, and is blank for a category it did not count or that was abandoned.");
        Console.Out.WriteLine("Present is rows the target already held: rows merged into another, rows an earlier pass copied before");
        Console.Out.WriteLine("a retry, or for an optional category rows ServiceControl wrote first. Settled is the moment a category");
        Console.Out.WriteLine("stopped running, whether it ended Done, Failed or Abandoned; read it beside the state.");
        Console.Out.WriteLine("A category whose last progress is old and whose state is still InProgress has stalled, not slowed down.");
    }

    static string Time(DateTime? time) => time?.ToString("u") ?? "not yet";
}
