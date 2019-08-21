﻿namespace ServiceControl.Monitoring.Http
{
    using Nancy;
    using Nancy.Responses.Negotiation;

    public abstract class BaseModule : NancyModule
    {
        protected string BaseUrl => Request.Url.SiteBase + Request.Url.BasePath;

        protected new Negotiator Negotiate
        {
            get
            {
                var negotiator = new Negotiator(Context);
                negotiator.NegotiationContext.PermissableMediaRanges.Clear();

                //We don't support xml, some issues serializing ICollections and IEnumerables
                //negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/xml"));
                //negotiator.NegotiationContext.PermissableMediaRanges.Add(MediaRange.FromString("application/vnd.particular.1+xml"));

                negotiator.NegotiationContext.PermissableMediaRanges.Add(new MediaRange("application/json"));
                negotiator.NegotiationContext.PermissableMediaRanges.Add(new MediaRange("application/vnd.particular.1+json"));

                return negotiator;
            }
        }
    }
}
