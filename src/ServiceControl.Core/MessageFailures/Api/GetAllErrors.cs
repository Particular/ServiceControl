namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetAllErrors : BaseModule
    {
        public GetAllErrors()
        {
            Get["/errors"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;

                    var results = session.Advanced
                        .LuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .Statistics(out stats)
                        .FilterByStatusWhere(Request)
                        .Sort(Request)
                        .Paging(Request)
                        .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                        .SelectFields<FailedMessageView>()
                        .ToArray();
                        
                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/endpoints/{name}/errors"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Advanced
                        .LuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .Statistics(out stats)
                        .FilterByStatusWhere(Request)
                        .AndAlso()
                        .WhereEquals("ReceivingEndpointName", endpoint)
                        .Sort(Request)
                        .Paging(Request)
                        .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                        .SelectFields<FailedMessageView>()
                        .ToArray();
                    
                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/errors/summary"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    var facetResults = session.Query<FailedMessage, FailedMessageFacetsIndex>()
                        .ToFacets(new List<Facet>
                                    {
                                        new Facet {Name = "Name", DisplayName="Endpoints"},
                                        new Facet {Name = "Host", DisplayName = "Hosts"},
                                        new Facet {Name = "MessageType", DisplayName = "Message types"},
                                    })
                        .Results;

                    return Negotiate.WithModel(facetResults);
                }
            };
        }
    }
}