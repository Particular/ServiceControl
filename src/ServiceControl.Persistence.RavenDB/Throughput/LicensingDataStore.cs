#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        var documentIds = endpointIds.Select(id => id.GenerateDocumentId());

        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var query = session.Query<EndpointDocument>()
            .Where(document => document.Id.In(documentIds))
            .Select(endpoint => new
            {
                EndpointDocument = endpoint,
                LastCollectedDate = RavenQuery.TimeSeries(endpoint, ThroughputTimeSeriesName)
                    .FromLast(timePeriod => timePeriod.Days(1))
                    .ToList()
                    .Results.Last().Timestamp
            });

        var queryResults = await query.ToListAsync(cancellationToken);

        Debug.Assert(session.Advanced.NumberOfRequests == 1, "Query is doing multiple round trips to RavenDB");

        return endpointIds.GroupJoin(queryResults,
            id => id,
            result => result.EndpointDocument.EndpointId,
            (id, resultsForId) =>
            {
                Endpoint? endpoint = null;

                var result = resultsForId.SingleOrDefault();
                if (result != null)
                {
                    endpoint = result.EndpointDocument.ToEndpoint();
                    endpoint.LastCollectedDate = DateOnly.FromDateTime(result.LastCollectedDate);
                }

                return (id, endpoint);
            });
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
        var results = queueNames.ToDictionary(queueName => queueName, _ => new List<ThroughputData>() as IEnumerable<ThroughputData>);

        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var query = session.Query<EndpointDocument>()
            .Where(document => document.SanitizedName.In(queueNames))
            .Include(builder => builder.IncludeTimeSeries(ThroughputTimeSeriesName, DateTime.UtcNow.AddMonths(-14)));

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
            .Where(document => document.SanitizedName.In(updates.Keys) || document.EndpointId.Name.In(updates.Keys));

        var documents = await query.ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            if (updates.TryGetValue(document.SanitizedName, out var newValueFromSanitizedName))
            {
                document.UserIndicator = newValueFromSanitizedName;
            }
            else if (updates.TryGetValue(document.EndpointId.Name, out var newValueFromEndpoint))
            {
                document.UserIndicator = newValueFromEndpoint;
                //update all that match this sanitized name
                var sanitizedMatchingQuery = session.Query<EndpointDocument>()
                    .Where(sanitizedDocument => sanitizedDocument.SanitizedName == document.SanitizedName && sanitizedDocument.EndpointId.Name != document.EndpointId.Name);
                var sanitizedMatchingDocuments = await sanitizedMatchingQuery.ToListAsync(cancellationToken);

                foreach (var matchingDocumentOnSanitizedName in sanitizedMatchingDocuments)
                {
                    matchingDocumentOnSanitizedName.UserIndicator = newValueFromEndpoint;
                }
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var result = await IsThereThroughputForLastXDaysInternal(session.Query<EndpointDocument>(), days, false, cancellationToken);

        return result;
    }

    public async Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken)
    {
        var store = await storeProvider.GetDocumentStore(cancellationToken);
        using IAsyncDocumentSession session = store.OpenAsyncSession(databaseConfiguration.Name);

        var baseQuery = session.Query<EndpointDocument>()
            .Where(endpoint => endpoint.EndpointId.ThroughputSource == throughputSource);

        var result = await IsThereThroughputForLastXDaysInternal(baseQuery, days, includeToday, cancellationToken);

        return result;
    }

    static async Task<bool> IsThereThroughputForLastXDaysInternal(IRavenQueryable<EndpointDocument> baseQuery, int days, bool includeToday, CancellationToken cancellationToken)
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-days).Date;
        DateTime toDate = includeToday ? DateTime.UtcNow.Date : DateTime.UtcNow.AddDays(-1).Date;

        var documents = await baseQuery
            .Select(e => RavenQuery.TimeSeries(e, ThroughputTimeSeriesName, fromDate, toDate).ToList())
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