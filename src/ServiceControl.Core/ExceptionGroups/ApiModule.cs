namespace ServiceControl.ExceptionGroups
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

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/exceptionGroups"] = 
                _ => GetAllExceptionGroups();

            Get["/exceptionGroups/{groupId}/errors"] = 
                parameters => GetExceptionsByGroup(parameters.groupId);
        }

        dynamic GetAllExceptionGroups()
        {
            using (var session = Store.OpenSession())
            {
                var results = session.Query<ExceptionGroup, ExceptionGroupsIndex>()
                    .ToArray();

                return Negotiate
                    .WithModel(results);
            }
        }

        dynamic GetExceptionsByGroup(string groupId)
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;

                var model = session.Query<MessageFailureHistory>()
                    .Where(m => m.ProcessingAttempts.Any(attempt => attempt.FailureDetails.Exception.ExceptionType == groupId))
                    .Statistics(out stats)
                    //.FilterByStatusWhere(Request)
                    //.Sort(Request)
                    .Paging(Request)
                    .TransformWith<FailedMessageViewTransformer, FailedMessageView>()
                    .ToArray();

                return Negotiate
                    .WithModel(model)
                    .WithPagingLinksAndTotalCount(stats, Request);
            }
        }
    }
}
