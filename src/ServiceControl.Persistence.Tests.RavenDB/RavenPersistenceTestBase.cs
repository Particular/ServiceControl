namespace ServiceControl.Persistence.Tests.RavenDB;

using Raven.Client.Documents;
using ServiceControl.Persistence.RavenDB;

public abstract class RavenPersistenceTestBase : PersistenceTestBase
{
    protected IDocumentStore DocumentStore => PersistenceTestsContext.DocumentStore;
    protected IRavenSessionProvider SessionProvider => PersistenceTestsContext.SessionProvider;
    protected void BlockToInspectDatabase() => PersistenceTestsContext.BlockToInspectDatabase();
}