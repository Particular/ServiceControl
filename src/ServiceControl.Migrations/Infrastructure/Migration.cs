namespace ServiceControl.Migrations
{
    using System.Threading.Tasks;
    using Raven.Client;

    public abstract class Migration
    {
        public virtual void Setup(IDocumentStore documentStore)
        {
            DocumentStore = documentStore;
        }

        public abstract Task Up();

        protected IDocumentStore DocumentStore { get; private set; }
    }
}