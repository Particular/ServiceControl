namespace Throughput.Persistence.RavenDb;

using Raven.Client.Documents.Session;

interface IRavenSessionProvider
{
    IAsyncDocumentSession OpenSession();
}