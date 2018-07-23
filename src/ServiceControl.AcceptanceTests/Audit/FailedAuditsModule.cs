namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Nancy;
    using Raven.Client;
    using ServiceControl.Operations;

    public class FailedAuditsCountReponse
    {
        public int Count { get; set; }
    }

    public class FailedAuditsModule : BaseModule
    {
        public FailedAuditsModule()
        {
            Get["/failedaudits/count", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var query =
                        session.Query<FailedAuditImport, FailedAuditImportIndex>().Statistics(out var stats);

                    var count = await query.CountAsync();

                    return Negotiate
                        .WithModel(new FailedAuditsCountReponse
                        {
                            Count = count
                        })
                        .WithEtag(stats);
                }
            };

            Post["/failedaudits/import", true] = async (_, token) =>
            {
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                await ImportFailedAudits.Value.Run(tokenSource);
                return HttpStatusCode.OK;
            };
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public Lazy<ImportFailedAudits> ImportFailedAudits { get; set; }
    }
}