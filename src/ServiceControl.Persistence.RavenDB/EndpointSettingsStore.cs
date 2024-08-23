namespace ServiceControl.Persistence.RavenDB;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Session;

class EndpointSettingsStore(IRavenSessionProvider sessionProvider) : IEndpointSettingsStore
{
    static string MakeDocumentId(string name) =>
        $"{EndpointSettings.CollectionName}/{DeterministicGuid.MakeId(name)}";

    public async IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings()
    {
        using IAsyncDocumentSession session = await sessionProvider.OpenSession();
        await using IAsyncEnumerator<StreamResult<EndpointSettings>> enumerator = await session
            .Advanced
            .StreamAsync<EndpointSettings>($"{EndpointSettings.CollectionName}/");

        while (await enumerator.MoveNextAsync())
        {
            yield return enumerator.Current.Document;
        }
    }

    public async Task Delete(string name, CancellationToken cancellationToken)
    {
        string docId = MakeDocumentId(name);

        using IAsyncDocumentSession session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

        session.Delete(docId);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken cancellationToken)
    {
        string docId = MakeDocumentId(settings.Name);

        using IAsyncDocumentSession session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

        await session.StoreAsync(settings, docId, cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
    }
}