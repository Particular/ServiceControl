﻿namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client;

    class FailedErrorImportCustomCheck : CustomCheck
    {
        public FailedErrorImportCustomCheck(IDocumentStore store)
            : base("Error Message Ingestion", "ServiceControl Health", TimeSpan.FromHours(1))
        {
            this.store = store;
        }

        public override async Task<CheckResult> PerformCheck()
        {
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<FailedErrorImport, FailedErrorImportIndex>();
                using (var ie = await session.Advanced.StreamAsync(query).ConfigureAwait(false))
                {
                    if (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        var message = @"One or more error messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.
The import of these messages could have failed for a number of reasons and ServiceControl is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/servicecontrol/import-failed-audit";

                        Logger.Warn(message);
                        return CheckResult.Failed(message);
                    }
                }
            }

            return CheckResult.Pass;
        }

        readonly IDocumentStore store;
        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedErrorImportCustomCheck));
    }
}