namespace ServiceControl.SagaAudit
{
    using System;
    using Infrastructure.Extensions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using Nancy;
    using System.Linq;

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/sagas/{id}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    Guid sagaId = parameters.id;
                    var sagaHistory =
                        session.Query<SagaHistory, SagaDetailsIndex>()
                            .SingleOrDefault(x => x.SagaId == sagaId);

                    if (sagaHistory == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    var lastModified = sagaHistory.Changes.OrderByDescending(x => x.FinishTime)
                        .Select(y => y.FinishTime)
                        .Single();
                    return Negotiate
                        .WithModel(sagaHistory)
                        .WithLastModified(lastModified);
                }
            };


            Get["/sagas"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results =
                        session.Query<SagaListIndex.Result, SagaListIndex>()
                            .Statistics(out stats)
                            .Paging(Request)
                            .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

        }

    }
}