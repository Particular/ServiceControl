namespace ServiceControl.Recoverability.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Extensions;
    using Infrastructure.WebApi;
    using MessageFailures;
    using MessageFailures.Api;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class FailureGroupsController : ApiController
    {
        internal FailureGroupsController(
            IEnumerable<IFailureClassifier> classifiers,
            IMessageSession bus,
            IDocumentStore store,
            GroupFetcher groupFetcher
            )
        {
            this.classifiers = classifiers;
            this.bus = bus;
            this.store = store;
            this.groupFetcher = groupFetcher;
        }

        [Route("recoverability/classifiers")]
        [HttpGet]
        public HttpResponseMessage GetSupportedClassifiers()
        {
            var result = classifiers
                .Select(c => c.Name)
                .OrderByDescending(classifier => classifier == "Exception Type and Stack Trace")
                .ToArray();

            return Negotiator.FromModel(Request, result)
                .WithTotalCount(result.Length);
        }

        [Route("recoverability/groups/reclassify")]
        [HttpPost]
        public async Task<IHttpActionResult> ReclassifyErrors()
        {
            await bus.SendLocal(new ReclassifyErrors
            {
                Force = true
            }).ConfigureAwait(false);

            return Content(HttpStatusCode.Accepted, string.Empty);
        }

        [Route("recoverability/groups/{groupid}/comment")]
        [HttpPost]
        public async Task<IHttpActionResult> EditComment(string groupId, string comment)
        {
            using (var session = store.OpenAsyncSession())
            {
                var groupComment =
                    await session.LoadAsync<GroupComment>(GroupComment.MakeId(groupId)).ConfigureAwait(false)
                    ?? new GroupComment { Id = GroupComment.MakeId(groupId) };

                groupComment.Comment = comment;

                await session.StoreAsync(groupComment).ConfigureAwait(false);

                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            return Content(HttpStatusCode.Accepted, string.Empty);
        }

        [Route("recoverability/groups/{groupid}/comment")]
        [HttpDelete]
        public async Task<IHttpActionResult> DeleteComment(string groupId)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.Delete(GroupComment.MakeId(groupId));
                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            return Content(HttpStatusCode.Accepted, string.Empty);
        }

        [Route("recoverability/groups/{classifier?}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetAllGroups([FromUri] string classifierFilter = null, string classifier = "Exception Type and Stack Trace")
        {
            if (classifierFilter == "undefined")
            {
                classifierFilter = null;
            }

            using (var session = store.OpenAsyncSession())
            {
                var results = await groupFetcher.GetGroups(session, classifier, classifierFilter).ConfigureAwait(false);

                return Negotiator.FromModel(Request, results)
                    .WithDeterministicEtag(EtagHelper.CalculateEtag(results));
            }
        }

        [Route("recoverability/groups/{groupId}/errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetGroupErrors(string groupId)
        {
            using (var session = store.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Statistics(out var stats)
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .Sort(Request)
                    .Paging(Request)
                    .SetResultTransformer(FailedMessageViewTransformer.Name)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator.FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }


        [Route("recoverability/groups/{groupId}/errors")]
        [HttpHead]
        public async Task<HttpResponseMessage> GetGroupErrorsCount(string groupId)
        {
            using (var session = store.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .QueryResultAsync()
                    .ConfigureAwait(false);

                var response = Request.CreateResponse(HttpStatusCode.OK);

                return response
                    .WithTotalCount(queryResult.TotalResults)
                    .WithEtag(queryResult.IndexEtag);
            }
        }

        [Route("recoverability/history")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetRetryHistory()
        {
            using (var session = store.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false)
                                   ?? RetryHistory.CreateNew();

                return Negotiator
                    .FromModel(Request, retryHistory)
                    .WithDeterministicEtag(retryHistory.GetHistoryOperationsUniqueIdentifier());
            }
        }

        [Route("recoverability/groups/id/{groupId}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetGroup(string groupId)
        {
            using (var session = store.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupView, FailureGroupsViewIndex>()
                    .Statistics(out var stats)
                    .WhereEquals(group => group.Id, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, queryResult.FirstOrDefault())
                    .WithEtag(stats);
            }
        }

        readonly IEnumerable<IFailureClassifier> classifiers;
        readonly IMessageSession bus;
        readonly IDocumentStore store;
        readonly GroupFetcher groupFetcher;
    }
}