namespace ServiceControl.Recoverability
{
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class FailureGroupsApi : BaseModule
    {
        public IBusSession BusSession { get; set; }

        public FailureGroupsApi()
        {
            Post["/recoverability/groups/reclassify", true] = 
                async (_, ct) => await ReclassifyErrors();

            Get["/recoverability/groups"] =
                _ => GetAllGroups();

            Get["/recoverability/groups/{groupId}/errors"] =
                parameters => GetGroupErrors(parameters.GroupId);
        }

        async Task<dynamic> ReclassifyErrors()
        {
            await BusSession.SendLocal(new ReclassifyErrors());

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
                    .ToArray();

                return Negotiate.WithModel(results)
                    .WithTotalCount(stats);
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
                    .AndAlso()
                    .WhereEquals( "Status",(int) FailedMessageStatus.Unresolved)
                    .Sort(Request)
                    .Paging(Request)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToArray();

                return Negotiate.WithModel(results)
                    .WithPagingLinksAndTotalCount(stats, Request);
            }
        }
    }
}