namespace ServiceControl.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.Persistence.RavenDB;

    class CheckRavenDBIndexErrors(IRavenDocumentStoreProvider documentStoreProvider) : CustomCheck("Error Database Index Errors", "ServiceControl Health", TimeSpan.FromMinutes(5))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var response = await documentStore.Maintenance.SendAsync(new GetIndexErrorsOperation(), cancellationToken);

            // Filter response as RavenDB5+ will return entries without errors
            var indexErrors = response
                .Where(x => x.Errors.Any())
                .ToArray();

            if (indexErrors.Length == 0)
            {
                return CheckResult.Pass;
            }

            var text = new StringBuilder();
            text.AppendLine("Detected RavenDB index errors, please start maintenance mode and resolve the following issues:");

            foreach (var indexError in indexErrors)
            {
                foreach (var indexingError in indexError.Errors)
                {
                    text.AppendLine($"- Index [{indexError.Name}] error: {indexError.Name} (Action: {indexingError.Action},  Doc: {indexingError.Document}, At: {indexingError.Timestamp})");
                }
            }

            text.AppendLine().AppendLine("See: https://docs.particular.net/search?q=servicecontrol+troubleshooting");

            var message = text.ToString();
            Logger.Error(message);
            return CheckResult.Failed(message);
        }

        static readonly ILog Logger = LogManager.GetLogger<CheckRavenDBIndexLag>();
    }
}