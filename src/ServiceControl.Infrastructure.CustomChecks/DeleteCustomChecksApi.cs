namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure.DomainEvents;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class DeleteCustomChecksApi : ScatterGatherApi<Guid, IList<CustomCheck>>
    {
        readonly IDocumentStore documentStore;
        readonly IDomainEvents domainEvents;

        public DeleteCustomChecksApi(IDocumentStore documentStore, IDomainEvents domainEvents, RemoteInstanceSettings settings, Func<HttpClient> httpClientFactory)
            : base(documentStore, settings, httpClientFactory)
        {
            this.documentStore = documentStore;
            this.domainEvents = domainEvents;
        }

        protected override async Task<QueryResult<IList<CustomCheck>>> LocalQuery(HttpRequestMessage request, Guid id)
        {
            await documentStore.AsyncDatabaseCommands.DeleteAsync(documentStore.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(id, typeof(CustomCheck), false), null)
                .ConfigureAwait(false);

            await domainEvents.Raise(new CustomCheckDeleted { Id = id })
                .ConfigureAwait(false);

            return QueryResult<IList<CustomCheck>>.Empty();
        }

        protected override IList<CustomCheck> ProcessResults(HttpRequestMessage request, QueryResult<IList<CustomCheck>>[] results)
        {
            return new List<CustomCheck>();
        }
    }
}