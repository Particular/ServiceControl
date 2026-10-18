namespace ServiceControl.Federation.Members;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Probes every member's root document on an interval, which re-enables members marked unavailable
/// and keeps the last known versions current for the version header.
/// </summary>
sealed class MemberHealthMonitor(MemberRegistry registry, MemberGateway gateway, FederationSettings settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        using var timer = new PeriodicTimer(settings.HealthProbeInterval);
        try
        {
            do
            {
                var probes = registry.All
                    .SelectMany(member => new[] { Surface.Primary, Surface.Monitoring }.Where(member.Serves).Select(surface => gateway.Probe(member, surface, cancellationToken)));
                await Task.WhenAll(probes);
            }
            while (await WaitForNextTick(timer, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is stopping
        }
    }

    static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
