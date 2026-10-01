namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Raven.Client.ServerWide.Operations;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class RavenEnvironmentDataProvider(RavenPersisterSettings settings, IRavenDocumentStoreProvider documentStoreProvider) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Storage.Type", () => "RavenDB"),
        Value("Storage.RavenServer", () => settings.UseEmbeddedServer ? "Embedded" : "External"),
        Value("Storage.Hosting", () => Hosting().Hosting),
        Deferred("Storage.ServerVersion", ServerVersion),
        Value("Storage.HostingSource", () => Hosting().Source),
        Value("Storage.FullTextSearch", () => settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
        Value("Storage.BodyStorage.Type", () => "RavenAttachments"),
        Value("Storage.BodyStorage.Auth", () => "NotApplicable")
    ];

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
}
