namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class FailureGroupsApi : BaseModule
    {
        public IBus Bus { get; set; }

        public GroupFetcher GroupFetcher { get; set; }

        public IEnumerable<IFailureClassifier> Classifiers { get; set; }

        public FailureGroupsApi()
        {
            Get["/recoverability/classifiers"] =
                _ => GetSupportedClassifiers();

            Post["/recoverability/groups/reclassify"] =
                _ => ReclassifyErrors();

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

        dynamic ReclassifyErrors()
        {
            Bus.SendLocal(new ReclassifyErrors
            {
                Force = true
            });

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
                RavenQueryStatistics stats;

                var queryResult = await session.Advanced
                                    .AsyncLuceneQuery<FailureGroupView, FailureGroupsViewIndex>()
                                    .Statistics(out stats)
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
                RavenQueryStatistics stats;

                var results = await session.Advanced
                    .AsyncLuceneQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Statistics(out stats)
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .Sort(Request)
                    .Paging(Request)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
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
                    .AsyncLuceneQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .QueryResultAsync
                    .ConfigureAwait(false);

                return Negotiate
                         .WithTotalCount(queryResult.TotalResults)
                         .WithEtag(queryResult.IndexEtag);
            }
        }
    }
}