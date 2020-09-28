namespace ServiceControl.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Raven.Client.Documents.Session;

    class MessageRedirectsCollection
    {
        public string Id { get; set; } = DefaultId;

        public string ETag { get; set; }

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
                redirects.ETag = session.Advanced.GetChangeVectorFor(redirects);
                //TODO:RAVEN5 missing Value extension method for methadata.
                //redirects.LastModified = session.Advanced.GetMetadataFor(redirects).Value<DateTime>("Last-Modified");

                return redirects;
            }

            return new MessageRedirectsCollection();
        }

        const string DefaultId = "messageredirects";
    }
}