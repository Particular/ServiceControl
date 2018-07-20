namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using MessageFailures.Api;
    using MessageFailures.InternalMessages;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailureGroupsApi : BaseModule
    {
        public FailureGroupsApi()
        {
            Get["/recoverability/classifiers"] =
                _ => GetSupportedClassifiers();

            Post["/recoverability/groups/reclassify", true] = (parameters, token) => ReclassifyErrors();

            Get["/recoverability/groups/{classifier?Exception Type and Stack Trace}", true] =
                (parameters, token) =>
                {
                    var classifierFilter = Request.Query["classifierFilter"] != "undefined" ? Request.Query["classifierFilter"] : null;

                    return GetAllGroups(parameters.Classifier, classifierFilter);
                };

            Get["/recoverability/groups/{groupId}/errors", true] =
                (parameters, token) => GetGroupErrors(parameters.GroupId);

            Head["/recoverability/groups/{groupId}/errors", true] =
                (parameters, token) => GetGroupErrorsCount(parameters.GroupId);

            Get["/recoverability/history/", true] =
                (_, token) => GetRetryHistory();

            Get["/recoverability/groups/id/{groupId}", true] =
                (parameters, token) => GetGroup(parameters.GroupId);
        }

        public Lazy<IEndpointInstance> Bus { get; set; }
        public GroupFetcher GroupFetcher { get; set; }
        public IEnumerable<IFailureClassifier> Classifiers { get; set; }

        async Task<dynamic> ReclassifyErrors()
        {
            await Bus.Value.SendLocal(new ReclassifyErrors
            {
                Force = true
            }).ConfigureAwait(false);

            return HttpStatusCode.Accepted;
        }

        dynamic GetSupportedClassifiers()
        {
            var classifiers = Classifiers
                .Select(c => c.Name)
                .OrderByDescending(classifier => classifier == "Exception Type and Stack Trace")
                .ToArray();

            return Negotiate.WithModel(classifiers)
                .WithTotalCount(classifiers.Length);
        }

        async Task<dynamic> GetRetryHistory()
        {
            using (var session = Store.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false) ?? RetryHistory.CreateNew();

                return Negotiate
                    .WithDeterministicEtag(retryHistory.GetHistoryOperationsUniqueIdentifier())
                    .WithModel(retryHistory);
            }
        }

        async Task<dynamic> GetAllGroups(string classifier, string classifierFilter)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await GroupFetcher.GetGroups(session, classifier, classifierFilter).ConfigureAwait(false);
                return Negotiate.WithModel(results)
                    .WithDeterministicEtag(EtagHelper.CalculateEtag(results));
            }
        }

        async Task<dynamic> GetGroup(string groupId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupView, FailureGroupsViewIndex>()
                    .Statistics(out var stats)
                    .WhereEquals(group => group.Id, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiate
                    .WithModel(queryResult.FirstOrDefault())
                    .WithEtag(stats);
            }
        }

        async Task<dynamic> GetGroupErrors(string groupId)
        {
            using (var session = Store.OpenAsyncSession())
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

                return Negotiate.WithModel(results)
                    .WithPagingLinksAndTotalCount(stats, Request)
                    .WithEtag(stats);
            }
        }

        async Task<dynamic> GetGroupErrorsCount(string groupId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .QueryResultAsync()
                    .ConfigureAwait(false);

                return Negotiate
                    .WithTotalCount(queryResult.TotalResults)
                    .WithEtag(queryResult.IndexEtag);
            }
        }
    }
}