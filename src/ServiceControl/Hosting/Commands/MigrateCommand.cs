namespace ServiceControl.Hosting.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Migration;
using ServiceControl.Persistence;

class MigrateCommand : AbstractCommand
{
    public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancel = (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        var migrationToken = cancellation.Token;

        try
        {
            Console.Out.WriteLine("Stop ServiceControl and all ingestion workers before running the offline migration.");
            await Run(settings, cancellationToken: migrationToken);
            Console.Out.WriteLine("The offline migration completed. Start the ServiceControl service or container to complete the online migration.");
        }
        catch (OperationCanceledException) when (migrationToken.IsCancellationRequested)
        {
            Console.Out.WriteLine("The offline migration stopped. Run --migrate again to resume from the last committed batch.");
            Environment.ExitCode = 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    internal static async Task Run(Settings settings, Action<WebApplicationBuilder> customize = null, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        // Persistence also registers runtime-only services whose dependencies this command does not need.
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.Logging.ConfigureLogging(settings.LoggingSettings.LogLevel);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddPersistence(settings);
        customize?.Invoke(builder);

#if DEBUG
        RunCommand.AllowUnreleasedMigrationWhenAsked(builder.Services, Environment.GetEnvironmentVariable(RunCommand.AllowUnreleasedMigrationVariable));
#endif

        // Never start this host: only the copier may write during the offline phase.
        await using var app = builder.Build();
        await MigrationStartup.RunRequiredCopy(app.Services, settings, cancellationToken);
    }
}
