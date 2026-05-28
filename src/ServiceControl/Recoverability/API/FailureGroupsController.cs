namespace ServiceControl.Recoverability.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using MessageFailures.Api;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Persistence.Infrastructure;
    using ServiceControl.Infrastructure.Auth.Rbac;
    using ServiceControl.Infrastructure.WebApi.Auth;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class FailureGroupsController(
        IEnumerable<IFailureClassifier> classifiers,
        GroupFetcher fetcher,
        IErrorMessageDataStore store,
        IRetryHistoryDataStore retryStore,
        IPermissionEvaluator permissionEvaluator)
        : ControllerBase
    {
        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
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

        /// <summary>
        /// Adds or updates a comment for the specified failure group.
        /// <para>
        /// <b>Fail-closed for scoped users:</b> failure groups span multiple queues and cannot be
        /// scope-checked against a single queue address. If the user holds only scope-restricted
        /// grants for <c>recoverabilitygroups:view</c>, access is denied with 403.
        /// </para>
        /// </summary>
        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/comment")]
        [HttpPost]
        public async Task<IActionResult> EditComment(string groupId, string comment)
        {
            var scopeDenied = await EnforceGroupScopeAsync(groupId, Permissions.RecoverabilityGroupsView);
            if (scopeDenied != null)
            {
                return scopeDenied;
            }

            await store.EditComment(groupId, comment);

            return Accepted();
        }

        /// <summary>
        /// Deletes the comment for the specified failure group.
        /// <para>
        /// <b>Fail-closed for scoped users:</b> same semantics as <see cref="EditComment"/>.
        /// </para>
        /// </summary>
        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/comment")]
        [HttpDelete]
        public async Task<IActionResult> DeleteComment(string groupId)
        {
            var scopeDenied = await EnforceGroupScopeAsync(groupId, Permissions.RecoverabilityGroupsView);
            if (scopeDenied != null)
            {
                return scopeDenied;
            }

            await store.DeleteComment(groupId);

            return Accepted();
        }

        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
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

        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors")]
        [HttpGet]
        public async Task<IList<FailedMessageView>> GetGroupErrors(string groupId, [FromQuery] SortInfo sortInfo, [FromQuery] PagingInfo pagingInfo, string status = default, string modified = default)
        {
            // R1: resolve the caller's permitted queue scope and push it into the query before
            // paging, so that Total-Count and page sizes reflect only messages in scope.
            var queueScope = permissionEvaluator.ResolveQueueScope(User, Permissions.RecoverabilityGroupsView);

            var results = await store.GetGroupErrors(groupId, status, modified, sortInfo, pagingInfo, queueScope);

            Response.WithQueryStatsAndPagingInfo(results.QueryStats, pagingInfo);
            return results.Results;
        }

        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/groups/{groupId:required:minlength(1)}/errors")]
        [HttpHead]
        public async Task GetGroupErrorsCount(string groupId, string status = default, string modified = default)
        {
            var results = await store.GetGroupErrorsCount(groupId, status, modified);

            Response.WithQueryStatsInfo(results);
        }

        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/history")]
        [HttpGet]
        public async Task<RetryHistory> GetRetryHistory()
        {
            var retryHistory = await retryStore.GetRetryHistory();

            Response.WithDeterministicEtag(retryHistory.GetHistoryOperationsUniqueIdentifier());

            return retryHistory;
        }

        [Authorize(Policy = Permissions.RecoverabilityGroupsView)]
        [Route("recoverability/groups/id/{groupId:required:minlength(1)}")]
        [HttpGet]
        public async Task<FailureGroupView> GetGroup(string groupId, string status = default, string modified = default)
        {
            var result = await store.GetGroup(groupId, status, modified);

            Response.WithEtag(result.QueryStats.ETag);

            return result.Results.FirstOrDefault();
        }

        /// <summary>
        /// Fail-closed scope check for group operations. Groups span multiple queues and cannot
        /// be scope-checked against a single queue address, so scoped users are denied.
        /// </summary>
        async Task<IActionResult> EnforceGroupScopeAsync(string groupId, string permission)
        {
            if (!permissionEvaluator.HasUnrestrictedGrant(User, permission))
            {
                await AuthorizationHelpers.WriteScopeDenied403(
                    Response,
                    permission,
                    queueAddress: groupId);
                return new EmptyResult();
            }

            return null;
        }
    }
}
