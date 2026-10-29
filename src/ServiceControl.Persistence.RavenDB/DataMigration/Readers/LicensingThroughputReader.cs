#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Session.TimeSeries;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

/// <summary>
/// Reads each licensing endpoint's daily message counts out of the throughput database, one endpoint per batch,
/// with the batch's cursor on that endpoint's document id.
/// </summary>
sealed class LicensingThroughputReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<ThroughputDayRow>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.LicensingThroughput;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        ReadThroughputDays(resumeAfter, cancellationToken);

    async IAsyncEnumerable<MigrationBatch> ReadThroughputDays(string? resumeAfter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // One endpoint per batch, because the batch size counts rows and one endpoint can bring a year or more of days.
        await foreach (var endpoints in RavenDocumentStream.ByPrefix<EndpointDocument>(Lifecycle, CategoryId, Lifecycle.Settings.ThroughputDatabaseName, string.Empty, resumeAfter, 1, RavenDocumentStream.MaxDocumentsWalkedPerBatch, ThroughputDocuments.Endpoint, cancellationToken))
        {
            var days = new List<MigrationRow>();

            foreach (var endpoint in endpoints.Rows)
            {
                var id = ((Endpoint)endpoint.Document).Id;

                foreach (var point in await ReadThroughputSeries(endpoint.SourceId, cancellationToken))
                {
                    days.Add(new MigrationRow($"{endpoint.SourceId}|{point.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", new ThroughputDayRow(id, DateOnly.FromDateTime(point.Timestamp), (long)point.Value), ReadOnlyDictionary<string, object?>.Empty));
                }
            }

            yield return new MigrationBatch(days, endpoints.Cursor);
        }
    }

    // Each series read is a request, and one session refuses more than MaxNumberOfRequestsPerSession of them.
    async Task<TimeSeriesEntry[]> ReadThroughputSeries(string documentId, CancellationToken cancellationToken)
    {
        using var session = Lifecycle.OpenSession(Lifecycle.Settings.ThroughputDatabaseName);
        return await session.IncrementalTimeSeriesFor(documentId, LicensingDataStore.ThroughputTimeSeriesName).GetAsync(token: cancellationToken) ?? [];
    }
}
