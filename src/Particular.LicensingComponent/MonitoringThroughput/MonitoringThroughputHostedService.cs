namespace Particular.LicensingComponent.MonitoringThroughput;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;
using ServiceControl.Transports;
using Shared;

class MonitoringThroughputHostedService(ITransportCustomization transportCustomization, TransportSettings transportSettings, ILogger<MonitoringThroughputHostedService> logger, MonitoringService monitoringService) : IHostedService
{
    TransportInfrastructure? transportInfrastructure;

    async Task Handle(MessageContext message, CancellationToken cancellationToken)
    {
        try
        {
            await monitoringService.RecordMonitoringThroughput(message.Body.ToArray(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving throughput data from Monitoring");
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting {ServiceName}", nameof(MonitoringThroughputHostedService));

        transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(ServiceControlSettings.ServiceControlThroughputDataQueue, transportSettings, Handle, (_, __) => Task.FromResult(ErrorHandleResult.Handled), (_, __) => Task.CompletedTask);
        await transportInfrastructure.Receivers[ServiceControlSettings.ServiceControlThroughputDataQueue].StartReceive(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping {ServiceName}", nameof(MonitoringThroughputHostedService));

        if (transportInfrastructure != null)
        {
            await transportInfrastructure.Receivers[ServiceControlSettings.ServiceControlThroughputDataQueue].StopReceive(cancellationToken);
            await transportInfrastructure.Shutdown(cancellationToken);
        }
    }
}