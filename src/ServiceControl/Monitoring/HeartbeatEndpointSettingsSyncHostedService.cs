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

class HeartbeatEndpointSettingsSyncHostedService : BackgroundService
{
    readonly IMonitoringDataStore monitor;
    readonly IEndpointSettingsStore store;
    readonly Settings settings;
    readonly ILogger<HeartbeatEndpointSettingsSyncHostedService> logger;
    static TimeSpan DelayStart { get; } = TimeSpan.FromSeconds(20);

    public HeartbeatEndpointSettingsSyncHostedService(IMonitoringDataStore monitor, IEndpointSettingsStore store,
        Settings settings, ILogger<HeartbeatEndpointSettingsSyncHostedService> logger)
    {
        this.monitor = monitor;
        this.store = store;
        this.settings = settings;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(HeartbeatEndpointSettingsSyncHostedService)}");

        try
        {
            await Task.Delay(DelayStart, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromHours(6));

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
        var monitorEndpoints = (await monitor.GetAllKnownEndpoints()).Select(endpoint => endpoint.EndpointDetails.Name).Distinct().ToHashSet();

        IAsyncEnumerator<EndpointSettings> enumerator =
            store.GetAllEndpointSettings().GetAsyncEnumerator(cancellationToken);

        bool hasDefault = false;
        bool userSetTrackInstances = settings.TrackInstancesInitialValue;
        var names = new List<string>();

        // First we delete any endpoints data that no longer exists
        while (await enumerator.MoveNextAsync())
        {
            if (enumerator.Current.Name == string.Empty)
            {
                hasDefault = true;
                userSetTrackInstances = enumerator.Current.TrackInstances;
                continue;
            }

            if (!monitorEndpoints.Contains(enumerator.Current.Name))
            {
                await store.Delete(enumerator.Current.Name, cancellationToken);
            }

            names.Add(enumerator.Current.Name);
        }

        // Second we check to see if the default setting is store in the db, otherwise we set it
        if (!hasDefault)
        {
            await store.UpdateEndpointSettings(
                new EndpointSettings { Name = string.Empty, TrackInstances = userSetTrackInstances },
                cancellationToken);
        }

        // Last we initialise all endpoint settings for the ones missing in db
        foreach (string monitorEndpointsKey in monitorEndpoints)
        {
            await store.UpdateEndpointSettings(
                new EndpointSettings { Name = monitorEndpointsKey, TrackInstances = userSetTrackInstances },
                cancellationToken);
        }
    }
}