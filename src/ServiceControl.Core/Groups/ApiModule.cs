namespace ServiceControl.Groups
{
    using System;
    using System.Linq;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Groups.Archive;
    using ServiceControl.Groups.Indexes;
    using ServiceControl.Infrastructure.Extensions;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/groups"] = 
                _ => GetAllGroups();

            Get["/groups/{groupId}/errors"] = 
                parameters => GetGroupById(parameters.groupId);

            Post["/groups/{groupId}/errors/archive"] =
                parameters => ArchiveAllInGroup(parameters.groupId);
        }

        dynamic GetAllGroups()
        {
            using (var session = Store.OpenSession())
            {
                var results = session.Query<FailureGroup, FailureGroupsIndex>()
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

                var model = session.Query<MessageFailureHistory, MessageFailuresByFailureGroupsIndex>()
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
