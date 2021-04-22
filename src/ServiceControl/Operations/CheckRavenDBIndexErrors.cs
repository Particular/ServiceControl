namespace ServiceControl
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Raven.Client;

    class CheckRavenDBIndexErrors : CustomCheck
    {
        public CheckRavenDBIndexErrors(IDocumentStore store)
            : base("Error Database Index Errors", "ServiceControl Health", TimeSpan.FromMinutes(5))
        {
            _store = store;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var statistics = _store.DatabaseCommands.GetStatistics();
            var indexes = statistics.Indexes.OrderBy(x => x.Name).ToArray();

            if (statistics.Errors.Length <= 0)
            {
                return CheckResult.Pass;
            }

            var text = new StringBuilder();
            text.AppendLine("Detected RavenDB index errors, please start maintenance mode and resolve the following issues:");

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