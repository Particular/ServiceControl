namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Raven.Client.Documents.Session;

public interface IRavenSessionProvider
{
    IAsyncDocumentSession OpenSession();
}