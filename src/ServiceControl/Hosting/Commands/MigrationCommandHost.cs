namespace ServiceControl.Hosting.Commands;

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// What the migration commands share: the host they build and how they print a failure.
/// </summary>
static class MigrationCommandHost
{
    /// <summary>
    /// A builder for a host with logging and persistence only. The commands build it and never start it.
    /// </summary>
    public static WebApplicationBuilder CreateBuilder(Settings settings)
    {
        var builder = WebApplication.CreateBuilder();
        // Persistence also registers runtime-only services whose dependencies these commands do not need.
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.Logging.ClearProviders();
        builder.Logging.ConfigureLogging(settings.LoggingSettings.LogLevel);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddPersistence(settings);
        return builder;
    }

    /// <summary>
    /// The type and message of the exception and of every exception inside it, one per line, so the cause of a wrapped failure is shown.
    /// A cause the line above already quotes is left out.
    /// A checkpoint conflict also gets advice to stop whatever is still writing and run <paramref name="option" /> again.
    /// </summary>
    public static string ErrorText(Exception exception, string option)
    {
        var lines = new List<string>();
        var conflict = false;

        string printed = null;

        for (var current = exception; current is not null; current = current.InnerException)
        {
            conflict |= current is MigrationCheckpointConflictException;

            // A wrapper often quotes its cause, so printing the cause too would repeat it.
            if (printed?.Contains(current.Message, StringComparison.Ordinal) == true)
            {
                continue;
            }

            lines.Add($"{current.GetType().Name}: {current.Message}");
            printed = current.Message;
        }

        if (conflict)
        {
            lines.Add($"ServiceControl or a migration is still running on this database. Stop it and run {option} again.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
