namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Contracts.MessageRedirects;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.WebApi;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Raven.Client.Documents;

    public class MessageRedirectsController : ApiController
    {
        internal MessageRedirectsController(IMessageSession messageSession, IDocumentStore documentStore, IDomainEvents domainEvents)
        {
            this.documentStore = documentStore;
            this.domainEvents = domainEvents;
            this.messageSession = messageSession;
        }

        [Route("redirects")]
        [HttpPost]
        public async Task<HttpResponseMessage> NewRedirects(MessageRedirectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.fromphysicaladdress) || string.IsNullOrWhiteSpace(request.tophysicaladdress))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            var messageRedirect = new MessageRedirect
            {
                FromPhysicalAddress = request.fromphysicaladdress,
                ToPhysicalAddress = request.tophysicaladdress,
                LastModifiedTicks = DateTime.UtcNow.Ticks
            };

            using (var session = documentStore.OpenAsyncSession())
            {
                var collection = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                var existing = collection[messageRedirect.MessageRedirectId];

                if (existing != null)
                {
                    return existing.ToPhysicalAddress == messageRedirect.ToPhysicalAddress
                        ? Negotiator.FromModel(Request, messageRedirect, HttpStatusCode.Created)
                        : Negotiator.FromModel(Request, existing, HttpStatusCode.Conflict).WithReasonPhrase("Duplicate");
                }

                var dependents = collection.Redirects.Where(r => r.ToPhysicalAddress == request.fromphysicaladdress).ToList();

                if (dependents.Any())
                {
                    return Negotiator.FromModel(Request, dependents, HttpStatusCode.Conflict).WithReasonPhrase("Dependents");
                }

                collection.Redirects.Add(messageRedirect);

                await collection.Save(session).ConfigureAwait(false);
            }

            await domainEvents.Raise(new MessageRedirectCreated
            {
                MessageRedirectId = messageRedirect.MessageRedirectId,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress
            }).ConfigureAwait(false);

            if (request.retryexisting)
            {
                await messageSession.SendLocal(new RetryPendingMessages
                {
                    QueueAddress = messageRedirect.FromPhysicalAddress,
                    PeriodFrom = DateTime.MinValue,
                    PeriodTo = DateTime.UtcNow
                }).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.Created);
        }

        [Route("redirects/{messageredirectid:guid}")]
        [HttpPut]
        public async Task<HttpResponseMessage> UpdateRedirect(Guid messageRedirectId, MessageRedirectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.tophysicaladdress))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            using (var session = documentStore.OpenAsyncSession())
            {
                var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                var messageRedirect = redirects[messageRedirectId];

                if (messageRedirect == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }

                var toMessageRedirectId = DeterministicGuid.MakeId(request.tophysicaladdress);

                if (redirects[toMessageRedirectId] != null)
                {
                    return Request.CreateResponse(HttpStatusCode.Conflict);
                }

                var messageRedirectChanged = new MessageRedirectChanged
                {
                    MessageRedirectId = messageRedirectId,
                    PreviousToPhysicalAddress = messageRedirect.ToPhysicalAddress,
                    FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                    ToPhysicalAddress = messageRedirect.ToPhysicalAddress = request.tophysicaladdress
                };

                messageRedirect.LastModifiedTicks = DateTime.UtcNow.Ticks;

                await redirects.Save(session).ConfigureAwait(false);

                await domainEvents.Raise(messageRedirectChanged)
                    .ConfigureAwait(false);

                return Request.CreateResponse(HttpStatusCode.NoContent);
            }
        }

        [Route("redirects/{messageredirectid:guid}")]
        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteRedirect(Guid messageRedirectId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                var messageRedirect = redirects[messageRedirectId];

                if (messageRedirect == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NoContent);
                }

                redirects.Redirects.Remove(messageRedirect);

                await redirects.Save(session).ConfigureAwait(false);

                await domainEvents.Raise(new MessageRedirectRemoved
                {
                    MessageRedirectId = messageRedirectId,
                    FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                    ToPhysicalAddress = messageRedirect.ToPhysicalAddress
                }).ConfigureAwait(false);
            }

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        [Route("redirect")]
        [HttpHead]
        public async Task<HttpResponseMessage> CountRedirects()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                return Request.CreateResponse(HttpStatusCode.OK)
                    .WithEtag(redirects.ETag)
                    .WithTotalCount(redirects.Redirects.Count);
            }
        }

        [Route("redirects")]
        [HttpGet]
        public async Task<HttpResponseMessage> Redirects()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var redirects = await MessageRedirectsCollection.GetOrCreate(session).ConfigureAwait(false);

                var queryResult = redirects
                    .Sort(Request)
                    .Paging(Request)
                    .Select(r => new
                    {
                        r.MessageRedirectId,
                        r.FromPhysicalAddress,
                        r.ToPhysicalAddress,
                        LastModified = new DateTime(r.LastModifiedTicks)
                    });

                return Negotiator
                    .FromModel(Request, queryResult)
                    .WithEtag(redirects.ETag)
                    .WithPagingLinksAndTotalCount(redirects.Redirects.Count, Request);
            }
        }

        readonly IDomainEvents domainEvents;
        readonly IDocumentStore documentStore;
        IMessageSession messageSession;

        public class MessageRedirectRequest
        {
            public string fromphysicaladdress { get; set; }
            public string tophysicaladdress { get; set; }
            public bool retryexisting { get; set; }
        }
    }
}