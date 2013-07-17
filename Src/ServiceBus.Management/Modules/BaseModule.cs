namespace ServiceBus.Management.Modules
{
    using Nancy;
    using Nancy.Responses.Negotiation;

    public abstract class BaseModule : NancyModule
    {
        public new Negotiator Negotiate
        {
            get
            {
                var negotiator = new Negotiator(Context);
                negotiator.NegotiationContext.PermissableMediaRanges.Clear();
                negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/xml"));
                negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/json"));
                negotiator.NegotiationContext.PermissableMediaRanges.Add(
                    MediaRange.FromString("application/vnd.particular.1+json"));
                negotiator.NegotiationContext.PermissableMediaRanges.Add(
                    MediaRange.FromString("application/vnd.particular.1+xml"));

                return negotiator;
            }
        }
    }
}