namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Linq;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Transport.IBMMQ;

public class IBMMQTransportCustomization : TransportCustomization<IBMMQTransport>
{
    protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, IBMMQTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

    protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, IBMMQTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

    protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, IBMMQTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

    protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
    {
        services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
        services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
    }

    protected override IBMMQTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
    {
        if (transportSettings.ConnectionString is null)
        {
            throw new InvalidOperationException("Connection string not configured");
        }

        var connectionUri = new Uri(transportSettings.ConnectionString);
        var query = HttpUtility.ParseQueryString(connectionUri.Query);

        var transport = new IBMMQTransport
        {
            Host = connectionUri.Host,
            Port = connectionUri.Port > 0 ? connectionUri.Port : 1414,
            QueueManagerName = connectionUri.AbsolutePath.Trim('/') is { Length: > 0 } path ? Uri.UnescapeDataString(path) : "QM1",
            Channel = query["channel"] ?? "DEV.ADMIN.SVRCONN"
        };

        var userInfo = connectionUri.UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var parts = userInfo.Split(':');
            transport.User = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1)
            {
                transport.Password = Uri.UnescapeDataString(parts[1]);
            }
        }

        if (query["appname"] is { } appName)
        {
            transport.ApplicationName = appName;
        }
        if (query["sslkeyrepo"] is { } sslKeyRepo)
        {
            transport.SslKeyRepository = sslKeyRepo;
        }
        if (query["cipherspec"] is { } cipherSpec)
        {
            transport.CipherSpec = cipherSpec;
        }
        if (query["sslpeername"] is { } sslPeerName)
        {
            transport.SslPeerName = sslPeerName;
        }

        if (transportSettings.TryGet<Action<IBMMQTransport>>(out var overrides))
        {
            overrides(transport);
        }

        transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode)
            ? preferredTransactionMode
            : TransportTransactionMode.ReceiveOnly;

        return transport;
    }
}