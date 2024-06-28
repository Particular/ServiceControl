namespace Particular.LicensingComponent.MonitoringThroughput;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.Transport;
using ServiceControl.Transports;
using Shared;

internal class MonitoringThroughputHostedService(ITransportCustomization transportCustomization, TransportSettings transportSettings, ILogger<MonitoringThroughputHostedService> logger, MonitoringService monitoringService) : IHostedService
{
    private TransportInfrastructure? transportInfrastructure;

    private async Task Handle(MessageContext message, CancellationToken cancellationToken)
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

    public async Task StartAsync(CancellationToken cancellationToken) => transportInfrastructure = await transportCustomization.CreateTransportInfrastructure(ServiceControlSettings.ServiceControlThroughputDataQueue, transportSettings, Handle, (_, __) => Task.FromResult(ErrorHandleResult.Handled), (_, __) => Task.CompletedTask);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (transportInfrastructure != null)
        {
            await transportInfrastructure.Shutdown(cancellationToken);
        }
    }
}