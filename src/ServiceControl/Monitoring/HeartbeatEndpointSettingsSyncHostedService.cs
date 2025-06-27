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
        logger.LogInformation("Starting {ServiceName}", nameof(HeartbeatEndpointSettingsSyncHostedService));

        try
        {
            await Task.Delay(DelayStart, timeProvider, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromHours(6), timeProvider);

            do
            {
                try
                {
                    logger.LogInformation("Performing sync for {ServiceName}", nameof(HeartbeatEndpointSettingsSyncHostedService));
                    await PerformSync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex,
                        $"Failed to perform sync between data in {nameof(IEndpointInstanceMonitoring)} and {nameof(IEndpointSettingsStore)}");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", nameof(HeartbeatEndpointSettingsSyncHostedService));
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
        EndpointsView[] endpointsViews = endpointInstanceMonitoring.GetEndpoints();
        ILookup<string, Guid> monitorEndpointsLookup = endpointsViews
            .Where(view => !view.IsSendingHeartbeats)
            .ToLookup(view => view.Name, view => view.Id);
        await foreach (EndpointSettings endpointSetting in endpointSettingsStore.GetAllEndpointSettings()
                           .WithCancellation(cancellationToken))
        {
            if (!endpointSetting.TrackInstances)
            {
                if (monitorEndpointsLookup.Contains(endpointSetting.Name))
                {
                    // We leave one dead instance behind, so that in ServicePulse we still display the endpoint as unhealthy, and is up to the user to manually delete it.
                    // Otherwise, we would delete all dead instances and it could be that the endpoint should be alive but all instances are down and then we display nothing in ServicePulse which is no good!
                    foreach (Guid endpointId in monitorEndpointsLookup[endpointSetting.Name].SkipLast(1))
                    {
                        endpointInstanceMonitoring.RemoveEndpoint(endpointId);
                        await monitoringDataStore.Delete(endpointId);
                        logger.LogInformation("Removed endpoint '{EndpointName}' from monitoring data", endpointSetting.Name);
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
                logger.LogInformation(
                    "Removed EndpointTracking setting for '{Setting}' endpoint, since this endpoint is no longer monitored", endpointSetting.Name);
            }

            settingsNames.Add(endpointSetting.Name);
        }

        // We set the default if not previously set
        if (!hasDefault)
        {
            await endpointSettingsStore.UpdateEndpointSettings(
                new EndpointSettings { Name = string.Empty, TrackInstances = userSetTrackInstances },
                cancellationToken);
            logger.LogInformation(
                "Initialized default value of EndpointTracking to {TrackInstancesEnabled}", userSetTrackInstances);
        }

        // Initialise settings for any missing endpoint
        foreach (string name in monitorEndpoints.Except(settingsNames))
        {
            await endpointSettingsStore.UpdateEndpointSettings(
                new EndpointSettings { Name = name, TrackInstances = userSetTrackInstances },
                cancellationToken);
            logger.LogInformation(
                "Initialized '{Setting}' value of EndpointTracking to {TrackInstances}", name, userSetTrackInstances ? "tracking" : "not tracking");
        }
    }
}