namespace Throughput.Persistence.RavenDb;

using Raven.Client.Documents;

interface IRavenDocumentStoreProvider
{
    IDocumentStore GetDocumentStore();
}