namespace ServiceControl.ExceptionGroups
{
    using System.Linq;
    using Nancy;
    using NServiceBus;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.ExceptionGroups.Archive;
    using ServiceControl.ExceptionGroups.Retry;
    using ServiceControl.MessageFailures.Api;

    public class ApiModule : BaseModule
    {
        // TODO: Put this number somewhere, should this be a parameter sent from SP?
        const int MINIMUM_GROUP_SIZE = 5;

        public ApiModule()
        {
            // TODO: Standardize pluralization for exceptionGroup(s)

            Get["/exceptionGroups"] = 
                _ => GetAllExceptionGroups();

            Get["/exceptionGroups/{groupId}/errors"] = 
                parameters => GetExceptionsByGroup(parameters.groupId);

            Post["/exceptionGroup/{groupId}/errors/archive"] =
                parameters => ArchiveMessagesForGroup(parameters.groupId);

            Post["/exceptionGroups/{groupId}/errors/retry"] =
                parameters => RetryMessagesForGroup(parameters.groupId);
        }

        dynamic GetAllExceptionGroups()
        {
            using (var session = Store.OpenSession())
            {
                var results = session.Query<MessageFailureHistory_ByExceptionGroup.ReduceResult, MessageFailureHistory_ByExceptionGroup>()
                    .Where(result => result.Count >= MINIMUM_GROUP_SIZE) 
                    .TransformWith<ExceptionGroupResultsTransformer, ExceptionGroup>()
                    .ToArray();

                return Negotiate
                    .WithModel(results);
            }
        }

        dynamic GetExceptionsByGroup(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                var model = Enumerable.Empty<FailedMessageView>().ToArray();
                var totalCount = 0;

                var group = session.Query<MessageFailureHistory_ByExceptionGroup.ReduceResult, MessageFailureHistory_ByExceptionGroup>()
                    .FirstOrDefault(result => result.ExceptionType == groupId);

                if (group != null)
                {
                    // TODO: Apply Sorting
                    model = session.Load<FailedMessageViewTransformer, FailedMessageView>(
                        group.FailureHistoryIds.Paging(Request).ToArray()
                    ).ToArray();

                    totalCount = group.Count;
                }

                return Negotiate
                    .WithModel(model)
                    .WithPagingLinks(totalCount, Request)
                    .WithTotalCount(totalCount);
            }
        }

        dynamic ArchiveMessagesForGroup(string groupId)
        {
            Bus.SendLocal(new ArchiveAllForGroup
            {
                GroupId = groupId
            });
            return HttpStatusCode.Accepted;
        }

        dynamic RetryMessagesForGroup(string groupId)
        {
            Bus.SendLocal(new RetryAllForGroup
            {
                GroupId = groupId
            });
            return HttpStatusCode.Accepted;
        }

        public IBus Bus { get; set; }
    }
}
