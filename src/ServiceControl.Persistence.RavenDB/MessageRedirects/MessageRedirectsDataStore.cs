namespace ServiceControl.Persistence.RavenDB.MessageRedirects
{
    using System.Threading.Tasks;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsDataStore(IRavenSessionProvider sessionProvider) : IMessageRedirectsDataStore
    {
        public async Task<MessageRedirectsCollection> GetOrCreate()
        {
            using var session = await sessionProvider.OpenSession();
            var redirects = await session.LoadAsync<MessageRedirectsCollection>(DefaultId);

            if (redirects != null)
            {
                redirects.ETag = session.Advanced.GetChangeVectorFor(redirects);
                redirects.LastModified = session.Advanced.GetLastModifiedFor(redirects).Value;

                return redirects;
            }

            return new MessageRedirectsCollection();
        }

        public async Task Save(MessageRedirectsCollection redirects)
        {
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(redirects, redirects.ETag, DefaultId);
            await session.SaveChangesAsync();
        }

        const string DefaultId = "messageredirects";
    }
}