namespace ServiceControl.Recoverability.Groups
{
    using System;
    using System.Linq;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Recoverability.Groups.Archive;
    using ServiceControl.Recoverability.Groups.Indexes;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/recoverability/groups"] = 
                _ => GetAllGroups();

            Get["/recoverability/groups/{groupId}/errors"] = 
                parameters => GetGroupById(parameters.groupId);

            Post["/recoverability/groups/{groupId}/errors/archive"] =
                parameters => ArchiveAllInGroup(parameters.groupId);
        }

        dynamic GetAllGroups()
        {
            using (var session = Store.OpenSession())
            {
                var results = session.Query<FailureGroup, FailureGroupsIndex>()
                    .Where(x => x.Count > 1)
                    .OrderByDescending(x => x.Last)
                    .ToArray();

                return Negotiate
                    .WithModel(results);
            }
        }

        dynamic GetGroupById(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var model = session.Query<MessageFailureHistory, MessageFailuresByFailureGroupIdIndex>()
                    .Where(m => m.FailureGroups.Any(g => g.Id == groupId))
                    .Statistics(out stats)
                    .Paging(Request)
                    .TransformWith<FailedMessageViewTransformer, FailedMessageView>()
                    .ToArray();

                return Negotiate
                    .WithModel(model)
                    .WithPagingLinksAndTotalCount(stats, Request);
            }
        }

        dynamic ArchiveAllInGroup(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            Bus.SendLocal(new ArchiveAllInGroup
            {
                GroupId = groupId
            });

            return HttpStatusCode.Accepted;
        }

        public IBus Bus { get; set; }
    }
}
