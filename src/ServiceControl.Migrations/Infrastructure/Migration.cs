namespace ServiceControl.Migrations
{
    using System.Threading.Tasks;
    using Raven.Client;

    public abstract class Migration
    {
        public virtual Task Down()
        {
            return Task.FromResult(true);
        }

        public virtual void Setup(IDocumentStore documentStore)
        {
            DocumentStore = documentStore;
        }

        public abstract Task Up();

        protected Task WaitForIndexing()
        {
            return DocumentStore.WaitForIndexingAsync();
        }

        protected IDocumentStore DocumentStore { get; private set; }
    }
}