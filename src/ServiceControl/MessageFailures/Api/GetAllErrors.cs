namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Contracts.Operations;
    using Nancy;
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

                    var results = session.Query<FailedMessage>()
                        .TransformWith<FaileMessageViewTransformer, FailedMessageView>()
                        .Statistics(out stats)
                        .Where(m =>
                            m.Status != MessageStatus.Successful &&
                            m.Status != MessageStatus.RetryIssued)
                        .Sort(Request)
                        .Paging(Request)
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
                    var results = session.Query<FailedMessage>()
                         .TransformWith<FaileMessageViewTransformer, FailedMessageView>()
                        .Statistics(out stats)
                        .Where(m =>
                            m.ReceivingEndpointName == endpoint &&
                            m.Status != MessageStatus.Successful &&
                            m.Status != MessageStatus.RetryIssued)
                        .Sort(Request)
                        .Paging(Request)
                        .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            //Get["/errors/facets"] = _ =>
            //{
            //    using (var session = Store.OpenSession())
            //    {
            //        var facetResults = session.Query<FailedMessageView>()
            //         //.TransformWith<FaileMessageViewTransformer, FailedMessageView>()
            //            .ToFacets("facets/messageFailureFacets")
            //            .Results;

            //        return Negotiate.WithModel(facetResults);
            //    }
            //};
        }
    }
}