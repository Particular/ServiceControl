namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class RavenEnvironmentDataProvider(RavenPersisterSettings settings, IRavenDocumentStoreProvider documentStoreProvider, IRavenSessionProvider sessionProvider) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Storage.Type", () => "RavenDB"),
        Value("Storage.RavenServer", () => settings.UseEmbeddedServer ? "Embedded" : "External"),
        Value("Storage.Hosting", () => Hosting().Hosting),
        Deferred("Storage.ServerVersion", ServerVersion),
        Value("Storage.HostingSource", () => Hosting().Source),
        Value("Storage.ServerEdition", () => "NotApplicable"),
        Value("Storage.ServiceObjective", () => "NotApplicable"),
        Deferred("Storage.SizeGB", SizeGB),
        Deferred("Storage.MessageCount", FailedMessageCount),
        Deferred("Storage.UnresolvedFailedMessages", UnresolvedFailedMessages),
        Value("Storage.FullTextSearch", () => settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
        Value("Storage.BodyStorage.Type", () => "RavenAttachments"),
        Value("Storage.LogLevel", () => settings.LogsMode),
        Value("Storage.QueryTimeoutSeconds", () => WhenConfigured(QueryTimeLimit.SettingName, () => Number((int)Math.Round(settings.QueryTimeout.TotalSeconds, MidpointRounding.AwayFromZero)))),
        Value("Storage.FreeSpaceThresholdPercent", () => WhenConfigured(RavenPersistenceConfiguration.DataSpaceRemainingThresholdKey, () => Number(settings.DataSpaceRemainingThreshold))),
        Value("Storage.MinimumFreeSpaceForIngestionPercent", () => WhenConfigured(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, () => Number(settings.MinimumStorageLeftRequiredForIngestion))),
        Value("Storage.ExpirationIntervalSeconds", () => WhenConfigured(RavenBootstrapper.ExpirationProcessTimerInSecondsKey, () => Number(settings.ExpirationProcessTimerInSeconds)))
    ];

    async ValueTask<string> SizeGB(CancellationToken cancellationToken)
    {
        var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
        var statistics = await documentStore.Maintenance.SendAsync(new GetStatisticsOperation(), cancellationToken);

        return (statistics.SizeOnDisk.SizeInBytes / BytesPerGigabyte).ToString("F1", CultureInfo.InvariantCulture);
    }

    async ValueTask<string> FailedMessageCount(CancellationToken cancellationToken)
    {
        var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
        var statistics = await documentStore.Maintenance.SendAsync(new GetCollectionStatisticsOperation(), cancellationToken);

        return statistics.Collections.TryGetValue("FailedMessages", out var count)
            ? count.ToString(CultureInfo.InvariantCulture)
            : "0";
    }

    async ValueTask<string> UnresolvedFailedMessages(CancellationToken cancellationToken)
    {
        using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

        var count = await session.Query<FailedMessage, FailedMessageViewIndex>()
            .Where(m => m.Status == FailedMessageStatus.Unresolved)
            .CountAsync(cancellationToken);

        return count.ToString(CultureInfo.InvariantCulture);
    }

    (string Hosting, string Source) Hosting()
    {
        // An embedded server runs in this process, so there is nothing to infer.
        if (settings.UseEmbeddedServer)
        {
            return (DatabaseHostClassifier.SelfHosted, DatabaseHostingSource.Configuration);
        }

        return Uri.TryCreate(settings.ConnectionString, UriKind.Absolute, out var url)
            ? (DatabaseHostClassifier.Classify(url.Host), DatabaseHostingSource.ConnectionString)
            : (DatabaseHostClassifier.Unknown, DatabaseHostingSource.None);
    }

    async ValueTask<string> ServerVersion(CancellationToken cancellationToken)
    {
        var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
        var buildNumber = await documentStore.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);

        return buildNumber.ProductVersion ?? DatabaseHostClassifier.Unknown;
    }

    static string WhenConfigured(string key, Func<string> readValue) =>
        SettingsReader.TryRead<string>(SettingsNamespace, key, out _) ? readValue() : "Default";

    static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    static readonly SettingsRootNamespace SettingsNamespace = new("ServiceControl");

    const double BytesPerGigabyte = 1024d * 1024 * 1024;
}
