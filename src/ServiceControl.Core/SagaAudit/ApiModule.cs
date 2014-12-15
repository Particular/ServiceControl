namespace ServiceControl.SagaAudit
{
    using System;
    using Infrastructure.Extensions;
    using Raven.Client;
    using Raven.Client.Indexes;
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
                    Guid id = parameters.id;
                    var sagaHistory = session.Load<SagaHistory>(id);
                    if (sagaHistory == null)
                    {
                        return HttpStatusCode.NotFound;
                    }
                    var etag = session.Advanced.GetEtagFor(sagaHistory);
                    var metadata = session.Advanced.GetMetadataFor(sagaHistory);
                    var lastModified = metadata.Value<DateTime>("Last-Modified");
                    return Negotiate
                        .WithModel(sagaHistory)
                        .WithEtagAndLastModified(etag, lastModified);
                }
            };

            Get["/sagas"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results =
                        session.Query<SagaHistory>()
                            .Statistics(out stats)
                            .Paging(Request)
                            .TransformWith<SagaListView, SagaListView.Result>()
                            .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

        }

        public class SagaListView : AbstractTransformerCreationTask<SagaHistory>
        {
            public class Result
            {
                public Guid Id;

                public string Uri;

                public string SagaType;
            }
            public SagaListView()
            {
                TransformResults = sagas => from saga in sagas
                                               select new Result
                                               {
                                                   Id = saga.SagaId,
                                                   SagaType = saga.SagaType,
                                                   Uri = "api/sagas/" + saga.SagaId
                                               };
            }
        }


    }
}