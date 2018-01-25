namespace ServiceControl.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public class MessageRedirectsCollection
    {
        const string DefaultId = "messageredirects";

        public string Id { get; set; } = DefaultId;

        public Etag ETag { get; set; }

        public DateTime LastModified { get; set; }

        public MessageRedirect this[string from] => Redirects.SingleOrDefault(r => r.FromPhysicalAddress == from);
        public MessageRedirect this[Guid id] => Redirects.SingleOrDefault(r => r.MessageRedirectId == id);

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
        public List<MessageRedirect> Redirects { get; set; } = new List<MessageRedirect>();

        public async Task Save(IAsyncDocumentSession session)
        {
            await session.StoreAsync(this).ConfigureAwait(false);
            await session.SaveChangesAsync().ConfigureAwait(false);
        }

        public static async Task<MessageRedirectsCollection> GetOrCreate(IAsyncDocumentSession session)
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
}