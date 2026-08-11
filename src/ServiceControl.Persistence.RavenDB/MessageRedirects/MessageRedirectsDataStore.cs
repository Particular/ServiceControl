namespace ServiceControl.Persistence.RavenDB.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsDataStore(IRavenSessionProvider sessionProvider) : IMessageRedirectsDataStore
    {
        public const string CollectionId = "messageredirects";

        public async Task<IReadOnlyList<MessageRedirect>> GetRedirects(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var document = await session.LoadAsync<MessageRedirectsCollection>(CollectionId, cancellationToken);

            return document == null ? [] : document.ToRedirects();
        }

        public Task AddRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Mutate(document => document.Add(redirect), cancellationToken);

        public Task UpdateRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Mutate(document => document.Update(redirect), cancellationToken);

        public Task RemoveRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Mutate(document => document.Remove(redirect), cancellationToken);

        async Task Mutate(Action<MessageRedirectsCollection> mutate, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var document = await session.LoadAsync<MessageRedirectsCollection>(CollectionId, cancellationToken);
            var changeVector = document == null ? null : session.Advanced.GetChangeVectorFor(document);

            document ??= new MessageRedirectsCollection();

            mutate(document);

            await session.StoreAsync(document, changeVector, CollectionId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}
