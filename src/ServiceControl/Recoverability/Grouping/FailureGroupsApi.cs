namespace ServiceControl.Recoverability
{
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

        public FailureGroupsApi()
        {
            Post["/recoverability/groups/reclassify"] = 
                _ => ReclassifyErrors();

            Get["/recoverability/groups"] =
                _ => GetAllGroups();

            Head["/recoverability/groups"] =
                 _ => GetAllGroupsCount();

            Get["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrors(parameters.GroupId);

            Head["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrorsCount(parameters.GroupId);
        }

        dynamic ReclassifyErrors()
        {
            Bus.SendLocal(new ReclassifyErrors
            {
                Force = true
            });

            return HttpStatusCode.Accepted;
        }

        dynamic GetAllGroups()
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .Statistics(out stats)
                    .OrderByDescending(x => x.Last)
                    .Take(200)
                    .ToArray()
                    .Select(failureGroup => new
                    {
                        Id = failureGroup.Id,
                        Title = failureGroup.Title,
                        Type = failureGroup.Type,
                        Count = failureGroup.Count,
                        First = failureGroup.First,
                        Last = failureGroup.Last,
                        Status = RetryGroupSummary.GetStatusForGroup(failureGroup.Id)
                    });

                return Negotiate.WithModel(results)
                    .WithTotalCount(stats)
                    .WithEtagAndLastModified(stats);
            }
        }

        dynamic GetAllGroupsCount()
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;
                var results = session.Query<FailureGroupView, FailureGroupsViewIndex>()
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