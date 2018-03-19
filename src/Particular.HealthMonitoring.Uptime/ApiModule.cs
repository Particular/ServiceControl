namespace ServiceControl.Monitoring
{
    using System;
    using Nancy;
    using Nancy.ModelBinding;
    using Nancy.Responses.Negotiation;

    public class EndpointUpdateModel
    {
        public bool MonitorHeartbeat { get; set; }
    }

    public class ApiModule : NancyModule
    {
        public EndpointInstanceMonitoring Monitoring { get; set; }

        public new Negotiator Negotiate
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

        public ApiModule()
        {
            Get["/heartbeats/stats"] = _ => Negotiate.WithModel(Monitoring.GetStats());

            Get["/endpoints"] = _ => Negotiate.WithModel(Monitoring.GetEndpoints());

            Patch["/endpoints/{id}"] = parameters =>
            {
                var data = this.Bind<EndpointUpdateModel>();
                var endpointId = (Guid) parameters.id;

                if (data.MonitorHeartbeat)
                {
                    Monitoring.EnableMonitoring(endpointId);
                }
                else
                {
                    Monitoring.DisableMonitoring(endpointId);
                }

                return HttpStatusCode.Accepted;
            };
        }
    }
}