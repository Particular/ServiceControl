namespace ServiceControl.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceBus.Management.Infrastructure.Settings;

public class HeartbeatEndpointSettingsSyncHostedService(
    IMonitoringDataStore monitoringDataStore,
    IEndpointSettingsStore endpointSettingsStore,
    IEndpointInstanceMonitoring endpointInstanceMonitoring,
    Settings settings,
    TimeProvider timeProvider,
    ILogger<HeartbeatEndpointSettingsSyncHostedService> logger)
    : BackgroundService
{
    public TimeSpan DelayStart { get; set; } = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(HeartbeatEndpointSettingsSyncHostedService)}");

        try
        {
            await Task.Delay(DelayStart, timeProvider, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromHours(6), timeProvider);

            do
            {
                try
                {
                    await PerformSync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex,
                        $"Failed to perform sync between data in {nameof(IEndpointInstanceMonitoring)} and {nameof(IEndpointSettingsStore)}");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation($"Stopping {nameof(HeartbeatEndpointSettingsSyncHostedService)}");
        }
    }

    async Task PerformSync(CancellationToken cancellationToken)
    {
        var monitorEndpoints = (await monitoringDataStore.GetAllKnownEndpoints())
            .Select(endpoint => endpoint.EndpointDetails.Name).Distinct().ToHashSet();

        await InitialiseSettings(monitorEndpoints, cancellationToken);

        await PurgeMonitoringDataThatDoesNotNeedToBeTracked(cancellationToken);
    }

    async Task PurgeMonitoringDataThatDoesNotNeedToBeTracked(CancellationToken cancellationToken)
    {
        ILookup<string, Guid> monitorEndpointsLookup = endpointInstanceMonitoring.GetEndpoints()
            .Where(view => view.IsNotSendingHeartbeats)
            .ToLookup(view => view.Name, view => view.Id);
        await foreach (EndpointSettings endpointSetting in endpointSettingsStore.GetAllEndpointSettings()
                           .WithCancellation(cancellationToken))
        {
            if (!endpointSetting.TrackInstances)
            {
                if (monitorEndpointsLookup.Contains(endpointSetting.Name))
                {
                    foreach (Guid endpointId in monitorEndpointsLookup[endpointSetting.Name])
                    {
                        endpointInstanceMonitoring.RemoveEndpoint(endpointId);
                        await monitoringDataStore.Delete(endpointId);
                    }
                }
            }
        }
    }

    async Task InitialiseSettings(HashSet<string> monitorEndpoints, CancellationToken cancellationToken)
    {
        bool hasDefault = false;
        bool userSetTrackInstances = settings.TrackInstancesInitialValue;
        HashSet<string> settingsNames = [];

        // Delete any endpoints data that no longer exists
        await foreach (EndpointSettings endpointSetting in endpointSettingsStore.GetAllEndpointSettings()
                           .WithCancellation(cancellationToken))
        {
            if (endpointSetting.Name == string.Empty)
            {
                hasDefault = true;
                userSetTrackInstances = endpointSetting.TrackInstances;
                continue;
            }

            if (!monitorEndpoints.Contains(endpointSetting.Name))
            {
                await endpointSettingsStore.Delete(endpointSetting.Name, cancellationToken);
            }

            settingsNames.Add(endpointSetting.Name);
        }

        // We set the default if not previously set
        if (!hasDefault)
        {
            await endpointSettingsStore.UpdateEndpointSettings(
                new EndpointSettings { Name = string.Empty, TrackInstances = userSetTrackInstances },
                cancellationToken);
        }

        // Initialise settings for any missing endpoint
        foreach (string name in monitorEndpoints.Except(settingsNames))
        {
            await endpointSettingsStore.UpdateEndpointSettings(
                new EndpointSettings { Name = name, TrackInstances = userSetTrackInstances },
                cancellationToken);
        }
    }
}