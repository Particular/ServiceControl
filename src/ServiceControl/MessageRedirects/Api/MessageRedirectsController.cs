namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageRedirects;
    using Infrastructure.DomainEvents;
    using Infrastructure.WebApi;
    using MessageFailures.InternalMessages;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.MessageRedirects;

    using DeterministicGuid = Infrastructure.DeterministicGuid;

    [ApiController]
    public class MessageRedirectsController(
        IMessageSession session,
        IMessageRedirectsDataStore store,
        IDomainEvents events)
        : ControllerBase
    {
        [Route("redirects")]
        [HttpPost]
        public async Task<IActionResult> NewRedirects(MessageRedirectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.fromphysicaladdress) || string.IsNullOrWhiteSpace(request.tophysicaladdress))
            {
                return BadRequest();
            }

            var messageRedirect = new MessageRedirect
            {
                FromPhysicalAddress = request.fromphysicaladdress,
                ToPhysicalAddress = request.tophysicaladdress,
                LastModifiedTicks = DateTime.UtcNow.Ticks
            };

            var collection = await store.GetOrCreate();

            var existing = collection[messageRedirect.MessageRedirectId];

            if (existing != null)
            {
                //TODO verify both of these return the same as the previous code
                return existing.ToPhysicalAddress == messageRedirect.ToPhysicalAddress
                    ? Created()
                    : Conflict("Duplicate");
            }

            var dependents = collection.Redirects.Where(r => r.ToPhysicalAddress == request.fromphysicaladdress).ToList();

            if (dependents.Any())
            {
                //TODO verify this returns the same as the previous code
                return Conflict("Dependents");
            }

            collection.Redirects.Add(messageRedirect);

            await store.Save(collection);

            await events.Raise(new MessageRedirectCreated
            {
                MessageRedirectId = messageRedirect.MessageRedirectId,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress
            });

            if (request.retryexisting)
            {
                await session.SendLocal(new RetryPendingMessages
                {
                    QueueAddress = messageRedirect.FromPhysicalAddress,
                    PeriodFrom = DateTime.MinValue,
                    PeriodTo = DateTime.UtcNow
                });
            }

            return Accepted();
        }

        [Route("redirects/{messageredirectid:guid}")]
        [HttpPut]
        public async Task<IActionResult> UpdateRedirect(Guid messageRedirectId, MessageRedirectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.tophysicaladdress))
            {
                return BadRequest();
            }

            var redirects = await store.GetOrCreate();

            var messageRedirect = redirects[messageRedirectId];

            if (messageRedirect == null)
            {
                return NotFound();
            }

            var toMessageRedirectId = DeterministicGuid.MakeId(request.tophysicaladdress);

            if (redirects[toMessageRedirectId] != null)
            {
                return Conflict();
            }

            var messageRedirectChanged = new MessageRedirectChanged
            {
                MessageRedirectId = messageRedirectId,
                PreviousToPhysicalAddress = messageRedirect.ToPhysicalAddress,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress = request.tophysicaladdress
            };

            messageRedirect.LastModifiedTicks = DateTime.UtcNow.Ticks;

            await store.Save(redirects);

            await events.Raise(messageRedirectChanged);

            return NoContent();
        }

        [Route("redirects/{messageredirectid:guid}")]
        [HttpDelete]
        public async Task<IActionResult> DeleteRedirect(Guid messageRedirectId)
        {
            var redirects = await store.GetOrCreate();

            var messageRedirect = redirects[messageRedirectId];

            if (messageRedirect == null)
            {
                return NoContent();
            }

            redirects.Redirects.Remove(messageRedirect);

            await store.Save(redirects);

            await events.Raise(new MessageRedirectRemoved
            {
                MessageRedirectId = messageRedirectId,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress
            });

            return NoContent();
        }

        [Route("redirect")]
        [HttpHead]
        public async Task CountRedirects()
        {
            var redirects = await store.GetOrCreate();

            Response.WithEtag(redirects.ETag);
            Response.WithTotalCount(redirects.Redirects.Count);
        }

        [Route("redirects")]
        [HttpGet]
        public async Task<IEnumerable<RedirectsQueryResult>> Redirects(string sort, string direction, [FromQuery] PagingInfo pagingInfo)
        {
            var redirects = await store.GetOrCreate();

            var queryResult = redirects
                .Sort(sort, direction)
                .Paging(pagingInfo)
                .Select(r => new RedirectsQueryResult
                (
                    r.MessageRedirectId,
                    r.FromPhysicalAddress,
                    r.ToPhysicalAddress,
                    new DateTime(r.LastModifiedTicks)
                ));

            Response.WithEtag(redirects.ETag);
            Response.WithPagingLinksAndTotalCount(pagingInfo, redirects.Redirects.Count);

            return queryResult;
        }

        public record class RedirectsQueryResult(Guid MessageRedirectId, string FromPhysicalAddress, string ToPhysicalAddress, DateTime LastModified);

        public class MessageRedirectRequest
        {
#pragma warning disable IDE1006 // Naming Styles
            public string fromphysicaladdress { get; set; }
            public string tophysicaladdress { get; set; }
            public bool retryexisting { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        }
    }
}