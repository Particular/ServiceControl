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
        Value("Persistence.Type", () => "RavenDB"),
        Value("Persistence.RavenServer", () => settings.UseEmbeddedServer ? "Embedded" : "External"),
        Value("Persistence.Hosting", () => Hosting().Hosting),
        Deferred("Persistence.ServerVersion", ServerVersion),
        Value("Persistence.HostingSource", () => Hosting().Source),
        Value("Persistence.FullTextSearch", () => settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
        Value("Persistence.BodyStorage.Type", () => "RavenAttachments"),
        Value("Persistence.BodyStorage.Auth", () => "NotApplicable")
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
