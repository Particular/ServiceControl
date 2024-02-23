namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Raven.Client.Documents.Session;

interface IRavenSessionProvider
{
    IAsyncDocumentSession OpenSession();
}