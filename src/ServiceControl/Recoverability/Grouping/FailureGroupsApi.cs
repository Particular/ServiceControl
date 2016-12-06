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
            
            Delete["/recoverability/unacknowledgedgroups/{groupId}"] = 
                parameters =>
            {
                var groupId = parameters.groupId;

                using (var session = Store.OpenSession())
                {
                    var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId());

                    if (retryHistory != null)
                    {
                        retryHistory.Acknowledge(groupId, RetryType.FailureGroup);
                    }
                    session.Store(retryHistory);
                    session.SaveChanges();
                }
                
                return HttpStatusCode.OK;
            };
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

        dynamic GetRetryHistory()
        {
            using (var session = Store.OpenSession())
            {
                var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();

                return Negotiate.WithModel(retryHistory);
            }
        }

        dynamic GetAllGroups(string classifier)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var groups = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .Statistics(out stats)
                    .Where(v => v.Type == classifier)
                    .OrderByDescending(x => x.Last)
                    .Take(200)
                    .ToArray();

                var history = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();
                var unacknowledgedForThisClassifier = history.GetUnacknowledgedByClassifier(classifier);

                var groupUnacknowledgements= (from g in groups
                    join unack in unacknowledgedForThisClassifier on g.Id equals unack.RequestId
                    select unack).ToArray();
                var active = GetActiveGroups(groups, history, groupUnacknowledgements);
                
                var standaloneUnacknowledgements = unacknowledgedForThisClassifier.Except(groupUnacknowledgements).ToArray();
                var unacknowledged = GetUnacknowledgedGroups(classifier, standaloneUnacknowledgements);
                
                var results = active.Union(unacknowledged).OrderByDescending(g => g.Last);
                
                return Negotiate.WithModel(results)
                    .WithTotalCount(stats)
                    .WithEtagAndLastModified(stats);
            }
        }

        private static IEnumerable<dynamic> GetUnacknowledgedGroups(string classifier, UnacknowledgedRetryOperation[] standaloneUnacknowledgements)
        {
            return standaloneUnacknowledgements.Select(standalone =>
            {
                var unacknowledged = standaloneUnacknowledgements.First(unack => unack.RequestId == standalone.RequestId && unack.RetryType == RetryType.FailureGroup);

                return new
                {
                    Id = unacknowledged.RequestId,
                    Title = unacknowledged.Originator,
                    Type = classifier,
                    Count = unacknowledged.NumberOfMessagesProcessed,
                    unacknowledged.Last,
                    RetryStatus = RetryState.Completed,
                    RetryFailed = unacknowledged.Failed,
                    RetryStartTime = unacknowledged.StartTime,
                    NeedUserAcknowledgement = true
                };
            });
        }

        private IEnumerable<dynamic> GetActiveGroups(IEnumerable<FailureGroupView> activeGroups, RetryHistory history, UnacknowledgedRetryOperation[] groupUnacknowledgements)
        {
            return activeGroups.Select(failureGroup =>
            {
                var summary = RetryOperationManager.GetStatusForRetryOperation(failureGroup.Id, RetryType.FailureGroup);
                var historic = GetLatestHistoricOperation(history, failureGroup.Id, RetryType.FailureGroup);
                var unacknowledged = groupUnacknowledgements.FirstOrDefault(unack => unack.RequestId == failureGroup.Id && unack.RetryType == RetryType.FailureGroup);

                return (new
                {
                    failureGroup.Id,
                    failureGroup.Title,
                    failureGroup.Type,
                    failureGroup.Count,
                    failureGroup.First,
                    failureGroup.Last,
                    RetryStatus = summary?.RetryState.ToString() ?? "None",
                    RetryFailed = summary?.Failed,
                    RetryProgress = summary?.GetProgress().Percentage ?? 0.0,
                    RetryRemainingCount = summary?.GetProgress().MessagesRemaining,
                    RetryStartTime = summary?.Started,
                    LastRetryCompletionTime = unacknowledged?.CompletionTime ?? historic?.CompletionTime,
                    NeedUserAcknowledgement = unacknowledged != null
                });
            });
        }

        private HistoricRetryOperation GetLatestHistoricOperation(RetryHistory history, string requestId, RetryType retryType)
        {
            return history.HistoricOperations
                .Where(v => v.RequestId == requestId && v.RetryType == retryType)
                .OrderByDescending(v => v.CompletionTime)
                .FirstOrDefault();
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