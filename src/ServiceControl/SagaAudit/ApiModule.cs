namespace ServiceControl.SagaAudit
{
    using System;
    using System.Security.Cryptography.X509Certificates;
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
                    string id = parameters.id;
                    var sagaHistory = session.Load<SagaHistory>(id);
                    var etag = session.Advanced.GetEtagFor(sagaHistory); ;
                    var metadata = session.Advanced.GetMetadataFor(sagaHistory);
                    var lastModified = metadata.Value<DateTime>("Last-Modified");
                    return Negotiate
                        .WithModel(sagaHistory)
                        .WithEtagAndLastModified(etag, lastModified);
                }
            };

            Delete["/sagas/{id}"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    var sagaHistory = session.Load<SagaHistory>(parameters.id);

                    if (sagaHistory != null)
                    {
                        session.Delete(sagaHistory);
                        session.SaveChanges();
                    }
                }

                return HttpStatusCode.NoContent;
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
                            .Select(x => new SearchResultItem
                                {
                                    Id = x.Id, 
                                    SagaId= x.SagaId, 
                                })
                            .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
            
        }

        public class SearchResultItem
        {
            public Guid Id;

            public string Uri
            {
                get { return "api/sagas/" + Id; }
            }

            public Guid SagaId;
        }
    }
}