namespace ServiceControl.Migration;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ServiceBus.Management.Infrastructure.Settings;

/// <summary>
/// Runs the required copy as the host starts, before any hosted service of its own does.
/// A refusal throws, which fails the start and stops the host, as the copy requires.
/// </summary>
sealed class RequiredCopyBeforeTheHostOpens(IServiceProvider services, Settings settings) : IHostedLifecycleService
{
    // Inside the host rather than before it, because a Windows service reports itself started only once the host
    // starts, and the Service Control Manager kills a process that has said nothing for 30 seconds. Every
    // StartingAsync runs before any hosted service, so nothing has bound a port or begun ingesting.
    public Task StartingAsync(CancellationToken cancellationToken = default) =>
        MigrationStartup.RunRequiredCopy(services, settings, cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
