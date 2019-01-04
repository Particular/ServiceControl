namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading;
    using Infrastructure.Extensions;
    using Infrastructure.Nancy.Modules;
    using Nancy;
    using Raven.Client;
    using ServiceControl.Operations;

    public class FailedErrorsCountReponse
    {
        public int Count { get; set; }
    }

    class FailedErrorsModule : BaseModule
    {
        public FailedErrorsModule()
        {
            Get["/failederrors/count", true] = async (_, token) =>
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var query =
                        session.Query<FailedErrorImport, FailedErrorImportIndex>().Statistics(out var stats);

                    var count = await query.CountAsync();

                    return Negotiate
                        .WithModel(new FailedErrorsCountReponse
                        {
                            Count = count
                        })
                        .WithEtag(stats);
                }
            };

            Post["/failederrors/import", true] = async (_, token) =>
            {
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                await ImportFailedErrors.Value.Run(tokenSource);
                return HttpStatusCode.OK;
            };
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public Lazy<ImportFailedErrors> ImportFailedErrors { get; set; }
    }
}