namespace ServiceControl.Recoverability
{
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class FailureGroupsApi : BaseModule
    {
        public FailureGroupsApi()
        {
            Get["/recoverability/groups"] =
                _ => GetAllGroups();

            Get["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrors(parameters.GroupId);
        }

        dynamic GetAllGroups()
        {
            using (var session = Store.OpenSession())
            {
                var results = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .Where(x => x.Count > 1)
                    .OrderByDescending(x => x.Last)
                    .ToArray();

                return Negotiate.WithModel(results);
            }
        }

        dynamic GetGroupErrors(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var results = session.Query<FailureGroupMessageView, FailedMessages_ByGroup>()
                    .Where(x => x.FailureGroupId == groupId && x.Status == FailedMessageStatus.Unresolved)
                    .Statistics(out stats)
                    .Paging(Request)
                    .TransformWith<FailedMessageViewTransformer, FailedMessageView>()
                    .ToArray();

                return Negotiate.WithModel(results)
                    .WithPagingLinksAndTotalCount(stats, Request);
            }
        }
    }
}