namespace ServiceControl.Recoverability.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using MessageFailures.Api;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class FailureGroupsController(
        IEnumerable<IFailureClassifier> classifiers,
        GroupFetcher fetcher,
        IErrorMessageDataStore store,
        IRetryHistoryDataStore retryStore)
        : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/classifiers")]
        [HttpGet]
        public string[] GetSupportedClassifiers()
        {
            var result = classifiers
                .Select(c => c.Name)
                .OrderByDescending(classifier => classifier == "Exception Type and Stack Trace")
                .ToArray();

            Response.WithTotalCount(result.Length);

            return result;
        }

        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/comment")]
        [HttpPost]
        public async Task<IActionResult> EditComment(string groupId, string comment)
        {
            await store.EditComment(groupId, comment);

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/comment")]
        [HttpDelete]
        public async Task<IActionResult> DeleteComment(string groupId)
        {
            await store.DeleteComment(groupId);

            return Accepted();
        }

        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/groups/{classifier?}")]
        [HttpGet]
        public async Task<GroupOperation[]> GetAllGroups(string classifier = "Exception Type and Stack Trace", string classifierFilter = default)
        {
            if (classifierFilter == "undefined")
            {
                classifierFilter = null;
            }

            var results = await fetcher.GetGroups(classifier, classifierFilter);
            Response.WithDeterministicEtag(EtagHelper.CalculateEtag(results));
            return results;
        }

        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> GetGroupErrors(string groupId, [FromQuery] SortInfo sortInfo, [FromQuery] PagingInfo pagingInfo, string status = default, string modified = default)
        {
            var results = await store.GetGroupErrors(groupId, status, modified, sortInfo, pagingInfo);

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);
            return results.Results;
        }


        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors")]
        [HttpHead]
        public async Task GetGroupErrorsCount(string groupId, string status = default, string modified = default)
        {
            var results = await store.GetGroupErrorsCount(groupId, status, modified);

            Response.WithQueryStatsInfo(results);
        }

        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/history")]
        [HttpGet]
        public async Task<RetryHistory> GetRetryHistory()
        {
            var retryHistory = await retryStore.GetRetryHistory();

            Response.WithDeterministicEtag(retryHistory.GetHistoryOperationsUniqueIdentifier());

            return retryHistory;
        }

        [Authorize(Policy = Permissions.ErrorRecoverabilityGroupsView)]
        [Route("recoverability/groups/id/{groupId:required:minlength(1)}")]
        [HttpGet]
        public async Task<FailureGroupView> GetGroup(string groupId, string status = default, string modified = default)
        {
            var result = await store.GetGroup(groupId, status, modified);

            Response.WithEtag(result.QueryStats.ETag);

            return result.Results.FirstOrDefault();
        }
    }
}