using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace TestingTool.Smoke;

/// <summary>The name of the endpoint a handler runs in, so a check can prove which endpoint handled a message.</summary>
public sealed record SmokeEndpointIdentity(string Name);

/// <summary>
/// A real NServiceBus endpoint that lives for one smoke check, in its own host next to the load endpoint.
/// Stopping it releases its connections and stops its plugins. The queues or tables it created stay in
/// the transport.
/// </summary>
public sealed class SmokeEndpoint : IAsyncDisposable
{
    IHost? host;

    SmokeEndpoint(IHost host, string name, string mainAddress)
    {
        this.host = host;
        Name = name;
        MainAddress = mainAddress;
        Session = host.Services.GetRequiredService<IMessageSession>();
    }

    public string Name { get; }

    /// <summary>The input address the transport reports, for example with the schema and catalog of SQL Server.</summary>
    public string MainAddress { get; }

    public IMessageSession Session { get; }

    /// <param name="configureTransport">Production passes <see cref="NServiceBusEndpointExtensions.ConfigureTransport"/> so the endpoint uses the transport of the load endpoint.</param>
    public static async Task<SmokeEndpoint> Start(
        string name,
        Action<EndpointConfiguration> configureTransport,
        Action<EndpointConfiguration>? configure,
        SmokeObservations observations,
        string errorQueue,
        CancellationToken ct)
    {
        var config = new EndpointConfiguration(name);
        config.AssemblyScanner().Disable = true;
        config.AddHandler<SmokeMessageHandler>();
        config.UseSerialization<SystemJsonSerializer>();
        config.SendFailedMessagesTo(errorQueue);
        var recoverability = config.Recoverability();
        recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
        recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
        recoverability.Failed(failed => failed.HeaderCustomization(SmokeHeaders.CustomizeFailure));
        config.EnableInstallers();
        configureTransport(config);
        configure?.Invoke(config);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.Services.AddSingleton(observations);
        builder.Services.AddSingleton(new SmokeEndpointIdentity(name));
        builder.Services.AddNServiceBusEndpoint(config);

        var host = builder.Build();
        try
        {
            await host.StartAsync(ct);
            return new SmokeEndpoint(host, name, host.Services.GetRequiredService<ReceiveAddresses>().MainReceiveAddress);
        }
        catch (Exception ex)
        {
            await StopPartiallyStarted(host);
            if (ex is OperationCanceledException)
            {
                throw;
            }

            throw new SmokeCheckFailedException($"Endpoint {name} could not start: {ex.Message}");
        }
    }

    /// <summary>
    /// A host whose start failed or was canceled may have started some of its services, so it is stopped
    /// before it is disposed. The stop is bounded, and its own failure never replaces the startup error.
    /// </summary>
    static async Task StopPartiallyStarted(IHost host)
    {
        try
        {
            using var limit = new CancellationTokenSource(PartialStartStopTimeout);
            await host.StopAsync(limit.Token);
        }
        catch
        {
            // The startup error is the one to report.
        }
        finally
        {
            host.Dispose();
        }
    }

    static readonly TimeSpan PartialStartStopTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Stops and disposes the host. Calling it again does nothing.</summary>
    public async Task StopAsync(CancellationToken ct)
    {
        var current = Interlocked.Exchange(ref host, null);
        if (current is null)
        {
            return;
        }

        try
        {
            await current.StopAsync(ct);
        }
        finally
        {
            current.Dispose();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}
