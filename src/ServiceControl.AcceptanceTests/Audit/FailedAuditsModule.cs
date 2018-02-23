namespace ServiceBus.Management.AcceptanceTests
{
    using System.Linq;
    using System.Threading;
    using Nancy;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Operations;

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }

    public class FailedAuditsModule : BaseModule
    {
        public IBus Bus { get; set; }
        public ImportFailedAudits ImportFailedAudits { get; set; }

        public FailedAuditsModule()
        {
            Get["/failedaudits/count"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var query =
                        session.Query<FailedAuditImport, FailedAuditImportIndex>().Statistics(out stats);

                    var count = query
                        .Count();

                    return Negotiate
                        .WithModel(new FailedAuditsCountReponse
                        {
                            Count = count
                        })
                        .WithEtagAndLastModified(stats);
                }
            };

            Post["/failedaudits/import"] = _ =>
            {
                var tokenSource = new CancellationTokenSource();
                var task = ImportFailedAudits.Run(tokenSource);
                
                task.GetAwaiter().GetResult();
                return HttpStatusCode.OK;
            };
        }
    }
}