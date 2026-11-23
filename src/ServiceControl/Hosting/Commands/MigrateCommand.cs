namespace ServiceControl.Hosting.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Migration;
using ServiceControl.Migration.Checks;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

class MigrateCommand : AbstractCommand
{
    /// <summary>
    /// True when this process logs to its own migration file instead of logfile.txt and the console.
    /// Only a migration run does, so its help, the status report, setup and the service keep their usual log.
    /// </summary>
    public static bool LogsToItsOwnFile(HostArguments args) => args.Command == typeof(MigrateCommand) && !args.Help;

    public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
    {
        GuidedMigrationArguments arguments;

        try
        {
            arguments = GuidedMigrationArguments.Parse(args.NonInteractive, args.Abandon, args.Retry);
        }
        catch (ArgumentException e)
        {
            Console.Out.WriteLine(e.Message);
            Environment.ExitCode = 1;
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancel = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;

        try
        {
            await Finish(Run(settings, arguments, MigrationConsole.System(), cancellationToken: cancellation.Token), cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    /// <summary>
    /// Waits for the run and sets the exit code from it. Whatever the outcome, it then names the run's log file last.
    /// </summary>
    internal static async Task Finish(Task<int> run, CancellationToken cancellationToken = default)
    {
        try
        {
            Environment.ExitCode = await run;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.Out.WriteLine("The offline migration stopped. Run --start-data-migration again to resume from the last committed batch.");
            Environment.ExitCode = 1;
        }
        catch (Exception e)
        {
            // Outside a container the log goes only to the run's file, which the line below names, so the stack must be there.
            LoggerUtil.CreateStaticLogger<MigrateCommand>().LogError(e, "The offline migration stopped on an error");
            await Console.Error.WriteLineAsync(MigrationCommandHost.ErrorText(e, "--start-data-migration"));
            Environment.ExitCode = 1;
        }
        finally
        {
            if (LoggingConfigurator.CurrentLogFile() is { } logFile)
            {
                Console.Out.WriteLine();
                Console.Out.WriteLine($"The log for this run is {logFile}.");
            }
        }
    }

    internal static Task<int> Run(Settings settings, Action<WebApplicationBuilder> customize = null, CancellationToken cancellationToken = default) =>
        Run(settings, GuidedMigrationArguments.Unattended, MigrationConsole.System(), customize, cancellationToken);

    internal static async Task<int> Run(Settings settings, GuidedMigrationArguments arguments, MigrationConsole console, Action<WebApplicationBuilder> customize = null, CancellationToken cancellationToken = default)
    {
        // Before the host is built, because a persistence that registers no migration target would otherwise fail on a missing service instead of this advice.
        await MigrationStartupCheckRunner.Run([new MigrationPairIsSupportedCheck(settings)], cancellationToken);

        var builder = MigrationCommandHost.CreateBuilder(settings);
        customize?.Invoke(builder);

        // Never start this host: only the copier may write during the offline phase.
        await using var app = builder.Build();
        await using var source = PersistenceFactory.CreateMigrationSource(settings);

        var services = app.Services;
        var run = new MigrationRun(
            settings,
            source,
            services.GetRequiredService<IMigrationTarget>(),
            services.GetRequiredService<IMigrationCheckpointStore>(),
            services.GetRequiredService<IMigrationTargetReadiness>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILoggerFactory>(),
            arguments,
            console);

        return await GuidedMigration.Run(run, cancellationToken);
    }
}
