namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Extensions;

    public class ApiModule : BaseModule
    {
        public ListSagasApi ListSagasApi { get; set; }
        public GetSagaByIdApi GetSagaByIdApi { get; set; }

        public ApiModule()
        {
            Get["/sagas/{id}", true] = (parameters, token) => GetSagaByIdApi.Execute(this, (Guid) parameters.id);

            Get["/sagas", true] = (_, token) => ListSagasApi.Execute(this, NoInput.Instance);

            Get["/sagas2"] = _ =>
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