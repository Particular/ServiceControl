namespace ServiceBus.Management.Modules
{
    using global::Nancy;
    using global::Nancy.Responses.Negotiation;

    public abstract class BaseModule : NancyModule
    {
        protected string BaseUrl { get { return Request.Url.SiteBase + Request.Url.BasePath; } }

        protected new Negotiator Negotiate
        {
            get
            {
                var negotiator = new Negotiator(Context);
                negotiator.NegotiationContext.PermissableMediaRanges.Clear();

                //We don't support xml, some issues serializing ICollections and IEnumerables
                //negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/xml"));
                //negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/vnd.particular.1+xml"));

                negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/json"));
                negotiator.NegotiationContext.PermissableMediaRanges.Add(
                    MediaRange.FromString("application/vnd.particular.1+json"));

                return negotiator;
            }
        }
    }
}