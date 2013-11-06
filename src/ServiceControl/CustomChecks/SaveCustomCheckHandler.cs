namespace ServiceControl.CustomChecks
{
    using System.Linq;
    using EndpointPlugin.Messages.CustomChecks;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class CustomChecksModule : BaseModule
    {
        public CustomChecksModule()
        {
            Get["/customchecks"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var results =
                        session.Query<CustomCheck>()
                            .Statistics(out stats)
                            .Where(c => c.Status == Status.Fail)
                            .Paging(Request)
                            .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };
        }

        public IDocumentStore Store { get; set; }
    }

    class SaveCustomCheckHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public IDocumentStore Store { get; set; }

        public void Handle(ReportCustomCheckResult message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var customCheck = session.Load<CustomCheck>(message.CustomCheckId) ?? new CustomCheck();

                customCheck.Id = message.CustomCheckId;
                customCheck.Category = message.Category;
                customCheck.Status = message.Result.HasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = message.ReportedAt;
                customCheck.FailureReason = message.Result.FailureReason;

                session.Store(customCheck);
                session.SaveChanges();
            }
        }
    }
}