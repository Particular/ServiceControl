namespace ServiceControl.Recoverability.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.WebApi;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    class FailureGroupsController : ApiController
    {
        public FailureGroupsController(
            IEnumerable<IFailureClassifier> classifiers,
            IMessageSession bus,
            GroupFetcher groupFetcher
            IErrorMessageDataStore dataStore
            )
        {
            this.classifiers = classifiers;
            this.bus = bus;
            this.groupFetcher = groupFetcher;
            this.dataStore = dataStore;
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

        [Obsolete("Only used by legacy RavenDB35 storage engine")] // TODO: how to deal with this domain event
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
            await dataStore.EditComment(groupId, comment)
                .ConfigureAwait(false);

            return Content(HttpStatusCode.Accepted, string.Empty);
        }

        [Route("recoverability/groups/{groupid}/comment")]
        [HttpDelete]
        public async Task<IHttpActionResult> DeleteComment(string groupId)
        {
            await dataStore.DeleteComment(groupId)
                .ConfigureAwait(false);

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
                var results = await groupFetcher.GetGroups(session, classifier, classifierFilter).ConfigureAwait(false); // TODO: Analyze what to do with the GroupFetcher dependency

                return Negotiator.FromModel(Request, results)
                    .WithDeterministicEtag(EtagHelper.CalculateEtag(results));
            }
        }

        [Route("recoverability/groups/{groupId}/errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetGroupErrors(
            string groupId,
            [FromUri] string status,
            [FromUri] string modified
            )
        {
            var sortInfo = Request.GetSortInfo();
            var pagingInfo = Request.GetPagingInfo();

            var results = await dataStore.GetGroupErrors(groupId, status, modified, sortInfo, pagingInfo)
                .ConfigureAwait(false);

            return Negotiator.FromQueryResult(Request, results);
        }


        [Route("recoverability/groups/{groupId}/errors")]
        [HttpHead]
        public async Task<HttpResponseMessage> GetGroupErrorsCount(
            string groupId,
            [FromUri] string status,
            [FromUri] string modified
            )
        {
            var results = await dataStore.GetGroupErrorsCount(groupId, status, modified)
                .ConfigureAwait(false);

            return Negotiator.FromQueryStatsInfo(Request, results);
        }

        [Route("recoverability/history")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetRetryHistory()
        {
            var retryHistory = await dataStore.GetRetryHistory()
                .ConfigureAwait(false);

            return Negotiator
                .FromModel(Request, retryHistory)
                .WithDeterministicEtag(retryHistory.GetHistoryOperationsUniqueIdentifier());
        }

        [Route("recoverability/groups/id/{groupId}")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetGroup(
            string groupId,
            [FromUri] string status,
            [FromUri] string modified
            )
        {
            // TODO: Migrated as previous behavior but can be optimized as http api will return at most 1 item
            var result = await dataStore.GetGroup(groupId, status, modified).ConfigureAwait(false);

            return Negotiator
                .FromModel(Request, result.Results.FirstOrDefault())
                .WithEtag(result.QueryStats.ETag);
        }

        readonly IEnumerable<IFailureClassifier> classifiers;
        readonly IMessageSession bus;
        readonly GroupFetcher groupFetcher;
        readonly IErrorMessageDataStore dataStore;
    }
}