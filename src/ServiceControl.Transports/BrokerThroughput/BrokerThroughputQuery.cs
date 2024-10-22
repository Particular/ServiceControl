#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

    public void Initialize(ReadOnlyDictionary<string, string> settings)
    {
        InitialiseErrors.Clear();
        Diagnostics.Clear();

        try
        {
            InitializeCore(settings);
        }
        catch (Exception e)
        {
            InitialiseErrors.Add(e.Message);
            logger.LogError(e, $"Failed to initialise {GetType().Name}");
        }
    }

    protected abstract void InitializeCore(ReadOnlyDictionary<string, string> settings);

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

    //NOTE This was added after initial release to help with matching on sanitized name where the broker (azure) would auto lowercase all the names.
    //If the logic was added to the SanitizeEndpointName function it would only apply to new records, and not historical data, so the report and endpoint groupings would be incorrect.
    public virtual string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
}