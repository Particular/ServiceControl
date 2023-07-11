namespace ServiceControl.Persistence.RavenDb.Transactions
{
    using System.Threading.Tasks;
    using Raven.Client;

    class RavenTransaction : IDataStoreTransaction
    {
        public IAsyncDocumentSession Session { get; }

        public RavenTransaction(IDocumentStore store)
        {
            Session = store.OpenAsyncSession();
        }

        public Task SaveChanges() => Session.SaveChangesAsync();
        public void Dispose() => Session.Dispose();
    }

    public static class RavenTransactionExtensions
    {
        public static IAsyncDocumentSession GetSession(this IDataStoreTransaction transaction)
        {
            return (transaction as RavenTransaction).Session;
        }
    }
}
