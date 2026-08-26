namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Particular.LicensingComponent.Contracts;
using Raven.Client.ServerWide.Operations;

class RavenEnvironmentDataProvider(RavenPersisterSettings settings, IRavenDocumentStoreProvider documentStoreProvider, ILogger<RavenEnvironmentDataProvider> logger) : IEnvironmentDataProvider
{
    public async Task<IEnumerable<(string key, string value)>> GetData(CancellationToken cancellationToken = default) =>
    [
        ("Persistence.Type", "RavenDB"),
        ("Persistence.RavenServer", settings.UseEmbeddedServer ? "Embedded" : "External"),
        ("Persistence.Hosting", Hosting()),
        ("Persistence.ServerVersion", await ServerVersion(cancellationToken)),
        ("Persistence.FullTextSearch", settings.EnableFullTextSearchOnBodies ? "Enabled" : "Disabled"),
        ("Persistence.BodyStorage.Type", "RavenAttachments"),
        ("Persistence.BodyStorage.Auth", "NotApplicable")
    ];

    string Hosting()
    {
        if (settings.UseEmbeddedServer)
        {
            return DatabaseHostClassifier.SelfHosted;
        }

        return Uri.TryCreate(settings.ConnectionString, UriKind.Absolute, out var url)
            ? DatabaseHostClassifier.Classify(url.Host)
            : DatabaseHostClassifier.Unknown;
    }

    async Task<string> ServerVersion(CancellationToken cancellationToken)
    {
        try
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var buildNumber = await documentStore.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);

            return buildNumber.ProductVersion ?? DatabaseHostClassifier.Unknown;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not read the RavenDB server version");

            return DatabaseHostClassifier.Unknown;
        }
    }
}
