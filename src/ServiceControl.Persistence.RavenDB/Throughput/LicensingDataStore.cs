﻿#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Models;
using Particular.LicensingComponent.Persistence;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;

class LicensingDataStore(
    IRavenDocumentStoreProvider storeProvider,
    ThroughputDatabaseConfiguration databaseConfiguration) : ILicensingDataStore
{
    internal const string ThroughputTimeSeriesName = "INC: throughput data";
    const string AuditServiceMetadataDocumentId = "AuditServiceMetadata";
    const string BrokerMetadataDocumentId = "BrokerMetadata";
    const string ReportMasksDocumentId = "ReportMasks";

    static readonly AuditServiceMetadata DefaultAuditServiceMetadata = new([], []);
    static readonly BrokerMetadata DefaultBrokerMetadata = new(null, []);
    static readonly ReportConfigurationDocument DefaultReportConfiguration = new();

    public async Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var baseQuery = session.Query<EndpointDocument>();

        var query = includePlatformEndpoints
            ? baseQuery
            : baseQuery.Where(document => !document.EndpointIndicators.Contains(EndpointIndicator.PlatformEndpoint.ToString()));

        var documents = await query.ToListAsync(cancellationToken);

        return documents.Select(document => document.ToEndpoint());
    }

    public async Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var documentId = id.GenerateDocumentId();

        var document = await session.LoadAsync<EndpointDocument>(
            documentId,
            builder => builder.IncludeTimeSeries(ThroughputTimeSeriesName),
            cancellationToken);

        var endpoint = document?.ToEndpoint();
        if (endpoint != null)
        {
            var timeSeries = await session
                .IncrementalTimeSeriesFor(documentId, ThroughputTimeSeriesName)
                .GetAsync(token: cancellationToken);

            endpoint.LastCollectedDate = DateOnly.FromDateTime(timeSeries.LastOrDefault()?.Timestamp ?? DateTime.MinValue);
        }

        return endpoint;
    }

    public async Task<IEnumerable<(EndpointIdentifier, Endpoint?)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken)
    {
        var endpoints = new List<(EndpointIdentifier, Endpoint?)>();

        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var documentIdLookup = endpointIds.ToDictionary(
            endpointId => endpointId,
            endpointId => endpointId.GenerateDocumentId());

        var endpointDocuments = await session.LoadAsync<EndpointDocument>(
            documentIdLookup.Values.Distinct(),
            builder => builder.IncludeTimeSeries(ThroughputTimeSeriesName),
            cancellationToken);

        foreach (var (documentId, endpointDocument) in endpointDocuments)
        {
            var id = endpointDocument == null
             ? endpointIds.First(id => id.GenerateDocumentId().Equals(documentId))
             : endpointDocument.EndpointId;

            var endpoint = endpointDocument?.ToEndpoint();
            if (endpoint != null)
            {
                var timeSeries = await session
                    .IncrementalTimeSeriesFor(documentId, ThroughputTimeSeriesName)
                    .GetAsync(token: cancellationToken);

                endpoint.LastCollectedDate = DateOnly.FromDateTime(timeSeries.LastOrDefault()?.Timestamp ?? DateTime.MinValue);
            }

            endpoints.Add((id, endpoint));
        }

        return endpoints;
    }

    public async Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken)
    {
        var document = endpoint.ToEndpointDocument();

        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        await session.StoreAsync(document, document.GenerateDocumentId(), cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken)
    {
        var results = queueNames.ToDictionary(queueName => queueName, queueNames => new List<ThroughputData>() as IEnumerable<ThroughputData>);

        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var query = session.Query<EndpointDocument>()
            .Where(document => document.SanitizedName.In(queueNames))
            .Include(builder => builder.IncludeTimeSeries(ThroughputTimeSeriesName));

        var documents = await query.ToListAsync(cancellationToken);

        foreach (var document in documents)
        {
            var timeSeries = await session
                .IncrementalTimeSeriesFor(document.GenerateDocumentId(), ThroughputTimeSeriesName)
                .GetAsync(token: cancellationToken);

            if (results.TryGetValue(document.SanitizedName, out var throughputDatas) &&
                throughputDatas is List<ThroughputData> throughputDataList)
            {
                var endpointDailyThroughputs = timeSeries.Select(entry => new EndpointDailyThroughput(DateOnly.FromDateTime(entry.Timestamp), (long)entry.Value));
                var throughputData = new ThroughputData(endpointDailyThroughputs)
                {
                    ThroughputSource = document.EndpointId.ThroughputSource
                };
                throughputDataList.Add(throughputData);
            }
        }

        return results;
    }

    public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken)
    {
        if (!throughput.Any())
        {
            return;
        }

        var id = new EndpointIdentifier(endpointName, throughputSource);
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var documentId = id.GenerateDocumentId();
        var document = await session.LoadAsync<EndpointDocument>(documentId, cancellationToken) ??
            throw new InvalidOperationException($"Endpoint {id.Name} from {id.ThroughputSource} does not exist ");

        var timeSeries = session.IncrementalTimeSeriesFor(documentId, ThroughputTimeSeriesName);

        foreach (var (date, messageCount) in throughput)
        {
            timeSeries.Increment(date.ToDateTime(TimeOnly.MinValue), messageCount);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken)
    {
        var updates = userIndicatorUpdates.ToDictionary(u => u.Name, u => u.UserIndicator);
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var query = session.Query<EndpointDocument>()
            .Where(document => document.SanitizedName.In(updates.Keys));

        var documents = await query.ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            if (updates.TryGetValue(document.SanitizedName, out var newValue))
            {
                document.UserIndicator = newValue;
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var result = await IsThereThroughputForLastXDaysInternal(session.Query<EndpointDocument>(), days, cancellationToken);

        return result;
    }

    public async Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var baseQuery = session.Query<EndpointDocument>()
            .Where(endpoint => endpoint.EndpointId.ThroughputSource == throughputSource);

        var result = await IsThereThroughputForLastXDaysInternal(baseQuery, days, cancellationToken);

        return result;
    }

    static async Task<bool> IsThereThroughputForLastXDaysInternal(IRavenQueryable<EndpointDocument> baseQuery, int days, CancellationToken cancellationToken)
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-days).Date;
        DateTime yesterday = DateTime.UtcNow.AddDays(-1).Date;

        var documents = await baseQuery
            .Select(e => RavenQuery.TimeSeries(e, ThroughputTimeSeriesName, fromDate, yesterday).ToList())
            .ToListAsync(cancellationToken);

        return documents.SelectMany(timeSeries => timeSeries.Results).Any();
    }

    public async Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        return await session.LoadAsync<BrokerMetadata>(BrokerMetadataDocumentId, cancellationToken) ?? DefaultBrokerMetadata;
    }

    public async Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        await session.StoreAsync(brokerMetadata, BrokerMetadataDocumentId, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        return await session.LoadAsync<AuditServiceMetadata>(AuditServiceMetadataDocumentId, cancellationToken) ?? DefaultAuditServiceMetadata;
    }

    public async Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        await session.StoreAsync(auditServiceMetadata, AuditServiceMetadataDocumentId, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<string>> GetReportMasks(CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var config = await session.LoadAsync<ReportConfigurationDocument>(ReportMasksDocumentId, cancellationToken) ?? DefaultReportConfiguration;

        return config.MaskedStrings;
    }

    public async Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        await session.StoreAsync(new ReportConfigurationDocument { MaskedStrings = reportMasks }, ReportMasksDocumentId, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }
}