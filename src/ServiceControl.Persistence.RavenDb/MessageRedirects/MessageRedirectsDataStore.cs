namespace ServiceControl.Persistence.RavenDb.MessageRedirects
{
    using System;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsDataStore : IMessageRedirectsDataStore
    {
        readonly IDocumentStore store;

        public MessageRedirectsDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task<MessageRedirectsCollection> GetOrCreate()
        {
            using (var session = store.OpenAsyncSession())
            {
                var redirects = await session.LoadAsync<MessageRedirectsCollection>(DefaultId).ConfigureAwait(false);

                if (redirects != null)
                {
                    redirects.ETag = session.Advanced.GetEtagFor(redirects);
                    redirects.LastModified = session.Advanced.GetMetadataFor(redirects).Value<DateTime>("Last-Modified");

                    return redirects;
                }

                return new MessageRedirectsCollection();
            }
        }

        public async Task Save(MessageRedirectsCollection redirects)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(redirects, redirects.ETag, DefaultId).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        const string DefaultId = "messageredirects";
    }
}
