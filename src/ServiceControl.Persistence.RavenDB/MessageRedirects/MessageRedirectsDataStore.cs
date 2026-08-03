namespace ServiceControl.Persistence.RavenDB.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsDataStore(IRavenSessionProvider sessionProvider) : IMessageRedirectsDataStore
    {
        public const string CollectionId = "messageredirects";

        public async Task<IReadOnlyList<MessageRedirect>> GetRedirects()
        {
            using var session = await sessionProvider.OpenSession();
            var document = await session.LoadAsync<MessageRedirectsCollection>(CollectionId);

            return document == null ? [] : document.ToRedirects();
        }

        public Task AddRedirect(MessageRedirect redirect) => Mutate(document => document.Add(redirect));

        public Task UpdateRedirect(MessageRedirect redirect) => Mutate(document => document.Update(redirect));

        public Task RemoveRedirect(MessageRedirect redirect) => Mutate(document => document.Remove(redirect));

        async Task Mutate(Action<MessageRedirectsCollection> mutate)
        {
            using var session = await sessionProvider.OpenSession();
            var document = await session.LoadAsync<MessageRedirectsCollection>(CollectionId);
            var changeVector = document == null ? null : session.Advanced.GetChangeVectorFor(document);

            document ??= new MessageRedirectsCollection();

            mutate(document);

            await session.StoreAsync(document, changeVector, CollectionId);
            await session.SaveChangesAsync();
        }
    }
}
