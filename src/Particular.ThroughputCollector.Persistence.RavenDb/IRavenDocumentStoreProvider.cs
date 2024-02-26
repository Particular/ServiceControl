namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Raven.Client.Documents;

public interface IRavenDocumentStoreProvider
{
    IDocumentStore GetDocumentStore();
}