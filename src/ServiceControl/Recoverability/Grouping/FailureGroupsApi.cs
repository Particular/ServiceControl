namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class FailureGroupsApi : BaseModule
    {
        public IBus Bus { get; set; }

        public RetryOperationManager RetryOperationManager { get; set; }

        public IEnumerable<IFailureClassifier> Classifiers { get; set; }

        public FailureGroupsApi()
        {
            Get["/recoverability/classifiers"] =
                _ => GetSupportedClassifiers();

            Post["/recoverability/groups/reclassify"] =
                _ => ReclassifyErrors();

            Get["/recoverability/groups/{classifier?Exception Type and Stack Trace}"] =
                parameters => GetAllGroups(parameters.Classifier);

            Head["/recoverability/groups/{classifier?Exception Type and Stack Trace}"] =
                parameters => GetAllGroupsCount(parameters.Classifier);

            Get["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrors(parameters.GroupId);

            Head["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrorsCount(parameters.GroupId);

            Get["/recoverability/history/"] =
            _ => GetRetryHistory();
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
            var classifiers = Classifiers.Select(c => c.Name).ToArray();

            return Negotiate.WithModel(classifiers)
                .WithTotalCount(classifiers.Length);
        }

        dynamic GetRetryHistory()
        {
            using (var session = Store.OpenSession())
            {
                var retryHistory = session.Load<RetryOperationsHistory>(RetryOperationsHistory.MakeId()) ?? RetryOperationsHistory.CreateNew();

                return Negotiate.WithModel(retryHistory);
            }
        }

        dynamic GetAllGroups(string classifier)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .Statistics(out stats)
                    .Where(v => v.Type == classifier)
                    .OrderByDescending(x => x.Last)
                    .Take(200)
                    .ToArray()
                    .Select(failureGroup =>
                    {
                        var summary = RetryOperationManager.GetStatusForRetryOperation(failureGroup.Id, RetryType.FailureGroup);

                        return new
                        {
                            Id = failureGroup.Id,
                            Title = failureGroup.Title,
                            Type = failureGroup.Type,
                            Count = failureGroup.Count,
                            First = failureGroup.First,
                            Last = failureGroup.Last,
                            RetryStatus = summary?.RetryState.ToString() ?? "None",
                            Failed = summary?.Failed,
                            RetryProgress = summary?.GetProgression() ?? 0.0
                        };
                    });

                return Negotiate.WithModel(results)
                    .WithTotalCount(stats)
                    .WithEtagAndLastModified(stats);
            }
        }

        dynamic GetAllGroupsCount(string classifier)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session
                    .Query<FailureGroupView, FailureGroupsViewIndex>()
                    .Where(v => v.Type == classifier)
                    .Statistics(out stats)
                    .Count();

                return Negotiate
                    .WithTotalCount(results)
                    .WithEtagAndLastModified(stats);
            }
        }

        dynamic GetGroupErrors(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session.Advanced
                    .LuceneQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Statistics(out stats)
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .Sort(Request)
                    .Paging(Request)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToArray();

                return Negotiate.WithModel(results)
                    .WithPagingLinksAndTotalCount(stats, Request)
                    .WithEtagAndLastModified(stats);
            }
        }

        dynamic GetGroupErrorsCount(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                var queryResult = session.Advanced
                    .LuceneQuery<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .WhereEquals(view => view.FailureGroupId, groupId)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .QueryResult;

                return Negotiate
                         .WithTotalCount(queryResult.TotalResults)
                         .WithEtagAndLastModified(queryResult.IndexEtag, queryResult.IndexTimestamp);
            }
        }
    }
}