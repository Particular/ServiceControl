namespace ServiceControl.Operations
{
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Indexes;
    using System;
    using System.Linq;

    class FailedErrorImportCustomCheck : CustomCheck
    {
        public FailedErrorImportCustomCheck(IDocumentStore store)
            : base("Error Message Ingestion", "ServiceControl Health", TimeSpan.FromHours(1))
        {
            this.store = store;
        }

        public override CheckResult PerformCheck()
        {
            using (var session = store.OpenSession())
            {
                var query = session.Query<FailedErrorImport, FailedErrorImportIndex>();
                using (var ie = session.Advanced.Stream(query))
                {
                    if (ie.MoveNext())
                    {
                        var message = @"One or more error messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.
The import of these messages could have failed for a number of reasons and ServiceControl is not able to automatically reimport them. For guidance on how to resolve this see https://docs.particular.net/search?q=import%20failed%20audit%20messages";

                        Logger.Warn(message);
                        return CheckResult.Failed(message);
                    }
                }
            }

            return CheckResult.Pass;
        }

        readonly IDocumentStore store;
        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedAuditImportCustomCheck));
    }

    class FailedErrorImportIndex : AbstractIndexCreationTask<FailedErrorImport>
    {
        public FailedErrorImportIndex()
        {
            Map = docs => from cc in docs
                          select new FailedErrorImport
                          {
                              Id = cc.Id,
                              Message = cc.Message
                          };

            DisableInMemoryIndexing = true;
        }
    }
}