namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Collections.Generic;
    using Nancy;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetEndpoints : BaseModule
    {
        public GetEndpoints()
        {
            Get["/endpoints"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    QueryHeaderInformation stats;

                    var query = session.Query<EndpointsView, EndpointsViewIndex>();
                    var results = new List<EndpointsView>();
                    
                    using (var ie = session.Advanced.Stream(query, out stats))
                    {
                        while (ie.MoveNext())
                        {
                            results.Add(ie.Current.Document);
                        }
                    }

                    return Negotiate.WithModel(results)
                        .WithEtagAndLastModified(stats);
                }
            };
        }


    }
}