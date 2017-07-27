namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
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

            Get["/recoverability/groups/{classifier?Exception Type and Stack Trace}"] =
                parameters => GetAllGroups(parameters.Classifier);

            Get["/recoverability/groups/{classifier}/{endpointName}"] =
                parameters => GetAllGroups(parameters.Classifier, parameters.EndpointName);

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

                return Negotiate
                    .WithDeterministicEtag(retryHistory.GetHistoryOperationsUniqueIdentifier())
                    .WithModel(retryHistory);
            }
        }

        dynamic GetAllGroups(string classifier, string classifierFilter = null)
        {
            using (var session = Store.OpenSession())
            {
                var results = GroupFetcher.GetGroups(session, classifier, classifierFilter);
                return Negotiate.WithModel(results)
                    .WithDeterministicEtag(EtagHelper.CalculateEtag(results));
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