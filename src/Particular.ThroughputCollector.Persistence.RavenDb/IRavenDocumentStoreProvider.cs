namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;

public interface IRavenDocumentStoreProvider : IHostedService
{
    IDocumentStore GetDocumentStore();
}