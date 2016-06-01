using System.Linq;

namespace ServiceControl.MessageRedirects.Api
{
    using Raven.Client.Indexes;
    public class MessageRedirectsViewIndex : AbstractIndexCreationTask<MessageRedirect>
    {
        public MessageRedirectsViewIndex()
        {
            Map = redirects => from redirect in redirects
                select new
                {
                    MessageRedirectId = MessageRedirect.GetMessageRedirectIdFromDocumentId(redirect.Id),
                    redirect.MatchMessageType,
                    redirect.MatchSourceEndpoint,
                    redirect.RedirectToEndpoint,
                    AsOfDateTime = redirect.AsOfDateTime.Ticks,
                    ExpiresDateTime = redirect.ExpiresDateTime.Ticks,
                    redirect.LastModified
                };
        }
    }
}
