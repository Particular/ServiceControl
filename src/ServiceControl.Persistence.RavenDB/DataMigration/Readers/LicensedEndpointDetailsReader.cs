#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput;

/// <summary>
/// Reads the licensed endpoint details document, which becomes one Settings row. A read gives one batch holding the
/// whole document, or no rows when the document does not exist. The batch's cursor is the document's id, and a read
/// resumed after that id gives nothing.
/// </summary>
sealed class LicensedEndpointDetailsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<LicensedEndpointDetails>(lifecycle)
{
    const string DocumentId = LicensingDataStore.LicencedEndpointDetailsDocumentId;

    public override string CategoryId => MigrationCategoryIds.LicensedEndpointDetails;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        ReadLicensedEndpointDetails(resumeAfter, cancellationToken);

    async IAsyncEnumerable<MigrationBatch> ReadLicensedEndpointDetails(string? resumeAfter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (resumeAfter == DocumentId)
        {
            yield break;
        }

        if (resumeAfter is not null)
        {
            throw new InvalidOperationException($"The migration cannot resume the '{CategoryId}' category after '{resumeAfter}', because its only cursor is '{DocumentId}'. Run --migration-retry {CategoryId} to copy the category again from the start.");
        }

        using var session = Lifecycle.OpenSession(Lifecycle.Settings.ThroughputDatabaseName);
        var document = await session.LoadAsync<LicensedEndpointDetails>(DocumentId, cancellationToken);

        yield return new MigrationBatch(
            document is null ? [] : [new MigrationRow(DocumentId, document, ReadOnlyDictionary<string, object?>.Empty)],
            DocumentId);
    }
}
