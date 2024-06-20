#nullable enable
namespace ServiceControl.Transports;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1711
public class DefaultBrokerQueue(string queueName) : IBrokerQueue
#pragma warning restore CA1711
{
    public string QueueName { get; } = queueName;
    public string SanitizedName { get; set; } = queueName;
    public string? Scope { get; } = null;
    public List<string> EndpointIndicators { get; } = [];
}

public interface IBrokerThroughputQuery
{
    bool HasInitialisationErrors(out string errorMessage);
    void Initialise(FrozenDictionary<string, string> settings);
    IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        CancellationToken cancellationToken);
    IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken);
    Dictionary<string, string> Data { get; }
    string MessageTransport { get; }
    string? ScopeType { get; }
    KeyDescriptionPair[] Settings { get; }
    Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(CancellationToken cancellationToken);
    string SanitizeEndpointName(string endpointName);
}

public abstract class BrokerThroughputQuery(ILogger logger, string transport) : IBrokerThroughputQuery
{
    protected readonly List<string> InitialiseErrors = [];
    protected readonly StringBuilder Diagnostics = new();

    public bool HasInitialisationErrors(out string errorMessage)
    {
        errorMessage = string.Empty;

        if (InitialiseErrors.Count == 0)
        {
            return false;
        }

        errorMessage = string.Join('\n', InitialiseErrors);

        return true;
    }

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        InitialiseErrors.Clear();
        Diagnostics.Clear();

        try
        {
            InitialiseCore(settings);
        }
        catch (Exception e)
        {
            InitialiseErrors.Add(e.Message);
            logger.LogError(e, $"Failed to initialise {GetType().Name}");
        }
    }

    protected abstract void InitialiseCore(FrozenDictionary<string, string> settings);

    public abstract IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
        CancellationToken cancellationToken);

    public abstract IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken);

    public Dictionary<string, string> Data { get; set; } = [];
    public string MessageTransport => transport;
    public string? ScopeType { get; set; }
    public abstract KeyDescriptionPair[] Settings { get; }

    public async Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        if (InitialiseErrors.Count > 0)
        {
            sb.AppendLine($"Connection settings to {transport} have some errors:");
            InitialiseErrors.ForEach(s => sb.AppendLine(s));
            sb.AppendLine();
            sb.AppendLine("Connection attempted with the following settings:");
            sb.Append(Diagnostics.ToString());
            return (false, InitialiseErrors, sb.ToString());
        }

        try
        {
            (bool success, List<string> errors) = await TestConnectionCore(cancellationToken);

            if (success)
            {
                sb.AppendLine($"Connection test to {transport} was successful");
            }
            else
            {
                sb.AppendLine($"Connection test to {transport} failed:");
                errors.ForEach(s => sb.AppendLine(s));
            }
            sb.AppendLine();
            if (success)
            {
                sb.AppendLine("Connection settings used:");
            }
            else
            {
                sb.AppendLine("Connection attempted with the following settings:");
            }

            sb.Append(Diagnostics.ToString());

            return (success, errors, sb.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test connection failed");

            sb.AppendLine($"Connection test to {transport} failed:");
            sb.AppendLine(ex.Message);
            sb.AppendLine();
            sb.AppendLine("Connection attempted with the following settings:");
            sb.Append(Diagnostics.ToString());
            return (false, [ex.Message], sb.ToString());
        }
    }

    protected abstract Task<(bool Success, List<string> Errors)>
        TestConnectionCore(CancellationToken cancellationToken);

    public virtual string SanitizeEndpointName(string endpointName) => endpointName;
}

public readonly struct KeyDescriptionPair(string key, string description)
{
    public string Key { get; } = key;
    public string Description { get; } = description;
}

public class QueueThroughput
{
    public DateOnly DateUTC { get; set; }
    public long TotalThroughput { get; set; }
}

#pragma warning disable CA1711
public interface IBrokerQueue
#pragma warning restore CA1711
{
    public string QueueName { get; }
    public string SanitizedName { get; }
    public string? Scope { get; }
    public List<string> EndpointIndicators { get; }
}