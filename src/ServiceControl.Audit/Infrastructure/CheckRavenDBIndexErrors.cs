namespace ServiceControl
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Infrastructure.RavenDB;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class CheckRavenDbIndexErrors : CustomCheck
    {
        public CheckRavenDbIndexErrors(IDocumentStore store)
            : base("Audit Database Index Errors", "ServiceControl.Audit Health", TimeSpan.FromMinutes(5))
        {
            _store = store;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var invalidOrErroredIndexes = _store.DatabaseCommands.GetStatistics().Indexes.Where(index => index.IsInvalidIndex || index.Priority == IndexingPriority.Error).ToArray();

            var statistics = _store.DatabaseCommands.GetStatistics();
            var indexes = statistics.Indexes.OrderBy(x => x.Name).ToArray();

            if (statistics.Errors.Length == 0 && invalidOrErroredIndexes.Length == 0)
            {
                return CheckResult.Pass;
            }

            var text = new StringBuilder();
            text.AppendLine("Detected RavenDB index errors, please start maintenance mode and resolve the following issues:");

            foreach (var indexStat in invalidOrErroredIndexes)
            {
                text.AppendLine($"- Index [{indexStat.Name}] priority:{indexStat.Priority} is valid: {indexStat.IsInvalidIndex} indexing attempts: {indexStat.IndexingAttempts}, failed indexing attempts: {indexStat.IndexingErrors}");
            }

            foreach (var indexError in statistics.Errors)
            {
                text.AppendLine($"- Index [{indexError.IndexName}] error: {indexError.Error} (Action: {indexError.Action},  Doc: {indexError.Document}, At: {indexError.Timestamp})");
            }

            text.AppendLine().AppendLine("See: https://docs.particular.net/search?q=servicecontrol+troubleshooting");

            var message = text.ToString();
            _log.Error(message);
            return CheckResult.Failed(message);
        }

        static ILog _log = LogManager.GetLogger<CheckRavenDBIndexLag>();
        IDocumentStore _store;
    }
}
