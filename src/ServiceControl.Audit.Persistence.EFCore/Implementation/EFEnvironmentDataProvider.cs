namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using System.Globalization;
using ServiceControl.Audit.Persistence;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using static ServiceControl.Audit.Persistence.EnvironmentDatum;

class EFEnvironmentDataProvider(IDatabaseHostingProbe hostingProbe, IStorageFootprintProbe footprintProbe, IFailedAuditStorage failedAuditStorage) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData()
    {
        // The hosting keys share one probe, so the database is asked once per request and they
        // stand or fall together. The footprint keys share theirs for the same reason.
        Task<DatabaseHosting>? probe = null;
        Task<DatabaseHosting> Hosting(CancellationToken cancellationToken) => probe ??= hostingProbe.Probe(cancellationToken);

        Task<StorageFootprint?>? footprint = null;
        Task<StorageFootprint?> Footprint(CancellationToken cancellationToken) => footprint ??= footprintProbe.Probe(cancellationToken);

        return
        [
            Value("Storage.Type", () => hostingProbe.StorageName),
            Deferred("Storage.Hosting", async cancellationToken => (await Hosting(cancellationToken)).Hosting),
            Deferred("Storage.ServerVersion", async cancellationToken => (await Hosting(cancellationToken)).ServerVersion),
            Deferred("Storage.HostingSource", async cancellationToken => (await Hosting(cancellationToken)).Source),
            Deferred("Storage.ServerEdition", async cancellationToken => (await Hosting(cancellationToken)).ServerEdition),
            Deferred("Storage.ServiceObjective", async cancellationToken => (await Hosting(cancellationToken)).ServiceObjective),
            Deferred("Storage.SizeGB", async cancellationToken => SizeGB((await Footprint(cancellationToken))?.SizeGB)),
            Deferred("Storage.MessageCount", async cancellationToken => Count((await Footprint(cancellationToken))?.MessageCount)),
            Deferred("Health.FailedImports", async cancellationToken => Count(await failedAuditStorage.GetFailedAuditsCount(cancellationToken))),
            // The EF persisters always index bodies for full-text search; EnableFullTextSearchOnBodies is ignored.
            Value("Storage.FullTextSearch", () => "Enabled")
        ];
    }

    static string SizeGB(double? sizeGB) =>
        sizeGB is null ? DatabaseHostClassifier.Unknown : sizeGB.Value.ToString("F1", CultureInfo.InvariantCulture);

    static string Count(long? count) =>
        count is null ? DatabaseHostClassifier.Unknown : count.Value.ToString(CultureInfo.InvariantCulture);
}
