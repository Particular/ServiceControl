#nullable enable
namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using NServiceBus;
using NServiceBus.Faults;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

/// <summary>
/// Rebuilds the sending and receiving endpoint of a failed message from its headers exactly as ingestion would have stored them,
/// so a message ingested with an odd header copies with the same endpoint details it would have had.
/// </summary>
static class MigrationEndpointDetailsParser
{
    public static EndpointDetails? SendingEndpoint(IReadOnlyDictionary<string, string> headers)
    {
        var endpoint = new EndpointDetails
        {
            Name = ValueOf(headers, Headers.OriginatingEndpoint) ?? string.Empty,
            Host = ValueOf(headers, Headers.OriginatingMachine) ?? string.Empty,
            HostId = Guid.TryParse(ValueOf(headers, Headers.OriginatingHostId), out var hostId) ? hostId : Guid.Empty
        };

        if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Host))
        {
            return endpoint;
        }

        if (ValueOf(headers, Headers.OriginatingAddress) is not { } address)
        {
            return null;
        }

        var (queue, machine) = ExtractQueueAndMachineName(address);
        endpoint.Name = queue;
        endpoint.Host = machine!;
        return endpoint;
    }

    public static EndpointDetails? ReceivingEndpoint(IReadOnlyDictionary<string, string> headers)
    {
        var endpoint = new EndpointDetails
        {
            Name = ValueOf(headers, Headers.ProcessingEndpoint) ?? string.Empty,
            Host = ValueOf(headers, Headers.HostDisplayName) ?? ValueOf(headers, Headers.ProcessingMachine) ?? string.Empty,
            HostId = Guid.TryParse(ValueOf(headers, Headers.HostId), out var hostId) ? hostId : Guid.Empty
        };

        if (!string.IsNullOrEmpty(endpoint.Name) && !string.IsNullOrEmpty(endpoint.Host))
        {
            return endpoint;
        }

        if (ValueOf(headers, FaultsHeaderKeys.FailedQ) is not { } address)
        {
            return null;
        }

        var (queue, machine) = ExtractQueueAndMachineName(address);

        if (string.IsNullOrEmpty(endpoint.Name))
        {
            endpoint.Name = queue;
        }

        if (string.IsNullOrEmpty(endpoint.Host))
        {
            endpoint.Host = machine ?? "unknown";
        }

        if (string.IsNullOrEmpty(endpoint.Name))
        {
            return null;
        }

        if (endpoint.HostId == Guid.Empty)
        {
            endpoint.HostId = DeterministicGuid.MakeId(endpoint.Name, endpoint.Host);
        }

        return endpoint;
    }

    static string? ValueOf(IReadOnlyDictionary<string, string> headers, string key) =>
        headers.TryGetValue(key, out var value) ? value : null;

    static (string Queue, string? Machine) ExtractQueueAndMachineName(string address)
    {
        var atIndex = address.IndexOf('@', StringComparison.InvariantCulture);

        return atIndex > -1 ? (address[..atIndex], address[(atIndex + 1)..]) : (address, null);
    }
}
