namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json.Serialization;
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
    [Route("api")]
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
            if (string.IsNullOrWhiteSpace(request.FromPhysicalAddress) || string.IsNullOrWhiteSpace(request.ToPhysicalAddress))
            {
                return BadRequest();
            }

            var messageRedirect = new MessageRedirect
            {
                FromPhysicalAddress = request.FromPhysicalAddress,
                ToPhysicalAddress = request.ToPhysicalAddress,
                LastModifiedTicks = DateTime.UtcNow.Ticks
            };

            var collection = await store.GetOrCreate();

            var existing = collection[messageRedirect.MessageRedirectId];

            if (existing != null)
            {
                if (existing.ToPhysicalAddress == messageRedirect.ToPhysicalAddress)
                {
                    return StatusCode((int)HttpStatusCode.Created, messageRedirect);
                }

                // Setting the ReasonPhrase is no longer supported in HTTP/2. We could be using application/problem+json instead
                // but that would require a lot of changes in the client code. For now, we are using the X-Particular-Reason header
                Response.Headers["X-Particular-Reason"] = "Duplicate";
                return StatusCode((int)HttpStatusCode.Conflict, existing);
            }

            var dependents = collection.Redirects.Where(r => r.ToPhysicalAddress == request.FromPhysicalAddress).ToList();

            if (dependents.Any())
            {
                // Setting the ReasonPhrase is no longer supported in HTTP/2. We could be using application/problem+json instead
                // but that would require a lot of changes in the client code. For now, we are using the X-Particular-Reason header
                Response.Headers["X-Particular-Reason"] = "Dependents";
                return StatusCode((int)HttpStatusCode.Conflict, dependents);
            }

            collection.Redirects.Add(messageRedirect);

            await store.Save(collection);

            await events.Raise(new MessageRedirectCreated
            {
                MessageRedirectId = messageRedirect.MessageRedirectId,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress
            });

            if (request.RetryExisting)
            {
                await session.SendLocal(new RetryPendingMessages
                {
                    QueueAddress = messageRedirect.FromPhysicalAddress,
                    PeriodFrom = DateTime.MinValue,
                    PeriodTo = DateTime.UtcNow
                });
            }

            // not using Created here because that would be turned by the HttpNoContentOutputFormatter into a 204
            return StatusCode((int)HttpStatusCode.Created);
        }

        [Route("redirects/{messageRedirectId:guid}")]
        [HttpPut]
        public async Task<IActionResult> UpdateRedirect(Guid messageRedirectId, MessageRedirectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ToPhysicalAddress))
            {
                return BadRequest();
            }

            var redirects = await store.GetOrCreate();

            var messageRedirect = redirects[messageRedirectId];

            if (messageRedirect == null)
            {
                return NotFound();
            }

            var toMessageRedirectId = DeterministicGuid.MakeId(request.ToPhysicalAddress);

            if (redirects[toMessageRedirectId] != null)
            {
                return Conflict();
            }

            var messageRedirectChanged = new MessageRedirectChanged
            {
                MessageRedirectId = messageRedirectId,
                PreviousToPhysicalAddress = messageRedirect.ToPhysicalAddress,
                FromPhysicalAddress = messageRedirect.FromPhysicalAddress,
                ToPhysicalAddress = messageRedirect.ToPhysicalAddress = request.ToPhysicalAddress
            };

            messageRedirect.LastModifiedTicks = DateTime.UtcNow.Ticks;

            await store.Save(redirects);

            await events.Raise(messageRedirectChanged);

            return NoContent();
        }

        [Route("redirects/{messageRedirectId:guid}")]
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

        public record RedirectsQueryResult(Guid MessageRedirectId, string FromPhysicalAddress, string ToPhysicalAddress, DateTime LastModified);

        // Input models from the API are for some odd reasons not snake cased
        public class MessageRedirectRequest
        {
            [JsonPropertyName("fromphysicaladdress")]
            public string FromPhysicalAddress { get; set; }

            [JsonPropertyName("tophysicaladdress")]
            public string ToPhysicalAddress { get; set; }

            [JsonPropertyName("retryexisting")]
            public bool RetryExisting { get; set; }
        }
    }
}